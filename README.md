# Metroidvania Game (Working Title)

A 2.5D online co-op Metroidvania built in Unity. Two robots with complementary abilities, an agile one and a tank, work together to explore an industrial mega-factory, solve co-op puzzles and fight bosses. A solo player gets an AI companion.

Final-year Software Engineering capstone, Azrieli College of Engineering Jerusalem (JCE).

- **Team:** Munir Atallah, Mohammad Othman
- **Advisor:** Daniel Ben Zvi

## Requirements

| Tool | Version |
| --- | --- |
| Unity | **6000.0.85f2** (Unity 6 LTS), install through Unity Hub |
| Render pipeline | URP |
| Git | Any recent version |
| Git LFS | Required, see setup below |

Both of us must use exactly the same Unity version. Do not upgrade the project without agreeing first.

## Packages

Installed through the Unity Package Manager. Exact versions are locked in `Packages/manifest.json`, so Unity installs the right ones automatically when you open the project.

- Input System
- Cinemachine
- ProBuilder
- TextMeshPro

## Setup

```bash
git lfs install
git clone https://github.com/Mohammad-Othman-del/Metroidvania_Game.git
```

1. Open Unity Hub, click **Add**, and pick the cloned folder.
2. Open it with Unity 6000.0.85f2. The first open takes a few minutes while Unity rebuilds the `Library` folder.
3. Open `Assets/_Project/Scenes/Test/Test.unity` and press Play.

## Folder structure

```text
Assets/
  _Project/
    Art/
    Audio/
    Prefabs/
    Scenes/
    ScriptableObjects/
    Scripts/
      Core/  Input/  Player/  Abilities/  Combat/
      Enemies/  Bosses/  World/  Network/  UI/
  ThirdParty/
```

- Everything we make goes under `Assets/_Project`.
- Downloaded packs go under `Assets/ThirdParty`.

## Git rules

**Branches**

- `main`: always playable. Merge here only at the end of a phase.
- `dev`: shared work branch. Merge here about once a week.
- `feature/<name>`: one branch per task, for example `feature/player-controller`.

**Daily routine**

1. `git pull` before you start.
2. Commit small and often, with a clear message.
3. Push at the end of every session.

**Avoiding conflicts**

- Only one person edits a given scene at a time. Say so in the chat before you open it.
- Put reusable things in prefabs, not directly in scenes.
- Never commit `Library`, `Temp`, `Logs` or `UserSettings`. The `.gitignore` already blocks them.
- Always commit `.meta` files together with their asset.

**Large files**

Git LFS tracks `png`, `psd`, `fbx`, `wav` and `mp3`. If you add another large binary type, add it to `.gitattributes` before committing the file.

**Third-party assets**

This repository must not contain paid Unity Asset Store packs. Use CC0 assets, or keep paid packs out of Git.

## Naming

- `PascalCase` for scripts, prefabs and scenes.
- `camelCase` for variables.
- `SO_` prefix for ScriptableObject assets.

## Project settings

- Asset Serialization: Force Text
- Version Control: Visible Meta Files
