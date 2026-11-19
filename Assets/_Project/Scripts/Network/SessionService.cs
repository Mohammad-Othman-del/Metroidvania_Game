using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Metroidvania.Network
{
    /// <summary>
    /// Creates and joins online sessions through Unity Gaming Services (Lobby + Relay, via the
    /// Multiplayer Services "Sessions" API). The host gets a short join code to send to their partner;
    /// Relay means nobody needs to open ports. Creating or joining also starts Netcode
    /// (host or client) on the scene's NetworkManager automatically.
    /// </summary>
    public class SessionService : MonoBehaviour
    {
        public const int MaxPlayers = 2;

        public ISession Session { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "Offline";
        public bool InSession => Session != null;

        /// <summary>Raised when a session starts or ends.</summary>
        public event Action SessionChanged;

        public Task HostAsync() => RunAsync("Creating session...", async () =>
        {
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
            SetSession(await MultiplayerService.Instance.CreateSessionAsync(options));
            Status = $"Hosting. Join code: {Session.Code}";
        });

        public Task JoinAsync(string code) => RunAsync("Joining...", async () =>
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Enter the host's join code first.");

            SetSession(await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant()));
            Status = $"Joined session {Session.Code}";
        });

        public Task LeaveAsync() => RunAsync("Leaving...", async () =>
        {
            ISession session = Session;
            SetSession(null);
            if (session != null)
                await session.LeaveAsync(); // also shuts Netcode down; never call NetworkManager.Shutdown directly
            Status = "Offline";
        });

        async void OnApplicationQuit()
        {
            // Leave cleanly so the partner is told right away instead of timing out.
            if (Session != null)
            {
                try { await Session.LeaveAsync(); }
                catch (Exception) { /* quitting anyway */ }
            }
        }

        // ------------------------------------------------------------------ internals

        async Task RunAsync(string status, Func<Task> action)
        {
            if (Busy)
                return;

            Busy = true;
            Status = status;
            try
            {
                await EnsureSignedInAsync();
                await action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Status = $"Failed: {e.Message}";
            }
            finally
            {
                Busy = false;
            }
        }

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions().SetProfile(ProfileName());
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        void SetSession(ISession session)
        {
            if (Session != null)
            {
                Session.RemovedFromSession -= OnSessionEnded;
                Session.Deleted -= OnSessionEnded;
            }

            Session = session;

            if (Session != null)
            {
                Session.RemovedFromSession += OnSessionEnded;
                Session.Deleted += OnSessionEnded;
            }

            SessionChanged?.Invoke();
        }

        void OnSessionEnded()
        {
            SetSession(null);
            Status = "Session ended.";
        }

        /// <summary>
        /// Each running copy of the game needs its own anonymous sign-in, otherwise two copies on one
        /// PC look like the same player and the second can't join. Pass "-profile name" on the command
        /// line to choose one explicitly.
        /// </summary>
        static string ProfileName()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-profile")
                    return Sanitize(args[i + 1]);

#if UNITY_EDITOR
            // Every Multiplayer Play Mode player runs from its own project folder.
            return "editor_" + StableHash(Application.dataPath).ToString("x8");
#else
            return "build_" + System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
        }

        // Profile names: letters, digits, '-' and '_', at most 30 characters.
        static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                    chars[i] = '_';
            string clean = new string(chars);
            return clean.Length > 30 ? clean.Substring(0, 30) : clean;
        }

        static uint StableHash(string text)
        {
            uint hash = 2166136261; // FNV-1a: same result on every run, unlike string.GetHashCode
            foreach (char c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
