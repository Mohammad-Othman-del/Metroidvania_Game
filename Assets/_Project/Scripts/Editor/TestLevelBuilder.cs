using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

namespace Metroidvania.EditorTools
{
    /// <summary>
    /// Generates the Phase 1 grey-box test level (Tools > Metroidvania > Build Test Level).
    /// Every piece is a regular ProBuilder mesh, so the result can be edited by hand afterwards.
    /// Gameplay happens on the plane z = 0; geometry is <see cref="Depth"/> deep, centred on that plane.
    /// </summary>
    public static class TestLevelBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Movment Testing/Movement Testing.unity";
        const string MaterialDir = "Assets/_Project/Art/Materials/Greybox";
        const string TextureDir = "Assets/_Project/Art/Textures";
        const string GridTexturePath = TextureDir + "/T_GreyboxGrid.png";

        // User layers the controller relies on. Indices are fixed so every machine gets the same setup.
        public const int GroundLayer = 6;
        public const int OneWayPlatformLayer = 7;
        public const int PlayerLayer = 8;

        const float Depth = 6f;          // z-thickness of level geometry
        const float FloorBottom = -4f;   // underside of the main floor
        const float LevelTop = 20f;      // height of the boundary walls

        struct Mats
        {
            public Material Floor, Wall, OneWay, Background;
        }

        [MenuItem("Tools/Metroidvania/Build Test Level")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog(
                    "Rebuild test level?",
                    $"{ScenePath} already exists. Rebuilding replaces it, including any hand edits.",
                    "Rebuild", "Cancel"))
                return;

            EnsureLayer(GroundLayer, "Ground");
            EnsureLayer(OneWayPlatformLayer, "OneWayPlatform");
            EnsureLayer(PlayerLayer, "Player");

            Mats mats = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var level = new GameObject("Level").transform;
            BuildLayout(level, mats);

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(3f, 0f, 0f);

            SideCameraSetup.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[TestLevelBuilder] Built {ScenePath}");
        }

        // ------------------------------------------------------------------ layout

        static void BuildLayout(Transform root, Mats m)
        {
            // --- 01 Start: flat floor, plus a low one-way platform to drop through / jump up onto.
            var start = Group(root, "01_Start");
            Box(start, "LeftBoundary", -2f, 0f, FloorBottom, LevelTop, m.Wall, GroundLayer);
            Box(start, "Floor", 0f, 24f, FloorBottom, 0f, m.Floor, GroundLayer);
            OneWay(start, "OneWay_Intro", 12f, 16f, 2.5f, m.OneWay);

            // --- 02 Gaps: 2..6 m wide, separated by 4 m landings. The pit is only 1.5 m deep,
            // so a missed jump costs a short climb rather than a respawn.
            var gaps = Group(root, "02_Gaps");
            const float pitDepth = 1.5f;
            float[] gapWidths = { 2f, 3f, 4f, 5f, 6f };
            float x = 24f;
            float gapStart = x;
            for (int i = 0; i < gapWidths.Length; i++)
            {
                x += gapWidths[i];
                if (i == gapWidths.Length - 1) break;
                Box(gaps, $"Landing_After{gapWidths[i]:0}m", x, x + 4f, -pitDepth, 0f, m.Floor, GroundLayer);
                x += 4f;
            }
            Box(gaps, "PitFloor", gapStart, x, FloorBottom, -pitDepth, m.Floor, GroundLayer);

            float mainFloorStart = x;

            // --- 03 Ledges: solid steps 1, 2, 3 and 4 m tall to measure max jump height.
            var ledges = Group(root, "03_Ledges");
            x += 3f;
            for (int h = 1; h <= 4; h++)
            {
                Box(ledges, $"Ledge_{h}m", x, x + 3f, 0f, h, m.Wall, GroundLayer);
                x += 7f;
            }

            // --- 04 Floating platforms: zig-zag climb (2.5 m per step), then a 10 m drop to feel fall speed.
            var floating = Group(root, "04_FloatingPlatforms");
            float left = x, right = x + 6f;
            float[] tops = { 2.5f, 5f, 7.5f, 10f };
            for (int i = 0; i < tops.Length; i++)
            {
                float px = (i % 2 == 0) ? left : right;
                Box(floating, $"Platform_{tops[i]}m", px, px + 3f, tops[i] - 0.5f, tops[i], m.Floor, GroundLayer);
            }
            x = right + 3f + 4f;

            // --- 05 Slopes: hills at 20 deg (easy), 35 deg (walkable), 50 deg (should be too steep).
            var slopes = Group(root, "05_Slopes");
            Hill(slopes, ref x, 20f, 2f, m);
            Hill(slopes, ref x, 35f, 3f, m);
            Hill(slopes, ref x, 50f, 3f, m);
            x = Mathf.Ceil(x);

            // --- 06 One-way tower: platforms stacked beside a 14 m wall; the only way past is up and over.
            var tower = Group(root, "06_OneWayTower");
            float towerA = x + 1f, towerB = x + 6f;
            float wallX = towerB + 3f;
            float[] owTops = { 2.5f, 5f, 7.5f, 10f, 12.5f };
            for (int i = 0; i < owTops.Length; i++)
            {
                float px = (i % 2 == 0) ? towerB : towerA;
                OneWay(tower, $"OneWay_{owTops[i]}m", px, px + 3f, owTops[i], m.OneWay);
            }
            Box(tower, "TallWall", wallX, wallX + 1.5f, 0f, 14f, m.Wall, GroundLayer);
            x = wallX + 1.5f + 4f;

            // --- 07 Low ceiling: 3.5 m clearance, for head bonks and cutting jumps short.
            var ceiling = Group(root, "07_LowCeiling");
            Box(ceiling, "Ceiling", x, x + 16f, 3.5f, 4.5f, m.Wall, GroundLayer);
            x += 16f;

            // --- 08 Run-out: long flat stretch for top-speed and stopping distance.
            x += 35f;

            var bounds = Group(root, "00_Bounds");
            Box(bounds, "MainFloor", mainFloorStart, x, FloorBottom, 0f, m.Floor, GroundLayer);
            Box(bounds, "RightBoundary", x, x + 2f, FloorBottom, LevelTop, m.Wall, GroundLayer);

            // Visual-only back wall behind the play space, gives a sense of depth and motion.
            Box(bounds, "BackWall", -2f, x + 2f, FloorBottom, LevelTop, m.Background, 0,
                depth: 0.5f, zCenter: Depth * 0.5f + 0.25f, collider: false);
        }

        static void Hill(Transform parent, ref float x, float angle, float rise, Mats m)
        {
            float run = rise / Mathf.Tan(angle * Mathf.Deg2Rad);
            const float plateau = 3f;

            Ramp(parent, $"Ramp_{angle:0}deg_Up", x, run, rise, true, m.Floor);
            x += run;
            Box(parent, $"Plateau_{angle:0}deg", x, x + plateau, 0f, rise, m.Floor, GroundLayer);
            x += plateau;
            Ramp(parent, $"Ramp_{angle:0}deg_Down", x, run, rise, false, m.Floor);
            x += run + 4f;
        }

        // ------------------------------------------------------------------ shape helpers

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void OneWay(Transform parent, string name, float minX, float maxX, float top, Material mat)
        {
            Box(parent, name, minX, maxX, top - 0.3f, top, mat, OneWayPlatformLayer);
        }

        static void Box(Transform parent, string name, float minX, float maxX, float minY, float maxY,
            Material mat, int layer, float depth = Depth, float zCenter = 0f, bool collider = true)
        {
            var size = new Vector3(maxX - minX, maxY - minY, depth);
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, zCenter);
            Finish(pb, parent, name, mat, layer, collider);
        }

        /// <summary>Wedge whose base sits at y = 0. Ascending = rises toward +X.</summary>
        static void Ramp(Transform parent, string name, float startX, float run, float rise, bool ascending, Material mat)
        {
            float lowX = ascending ? 0f : run;
            float highX = ascending ? run : 0f;
            float z0 = -Depth * 0.5f, z1 = Depth * 0.5f;

            var a0 = new Vector3(lowX, 0f, z0);
            var a1 = new Vector3(lowX, 0f, z1);
            var b0 = new Vector3(highX, 0f, z0);
            var b1 = new Vector3(highX, 0f, z1);
            var c0 = new Vector3(highX, rise, z0);
            var c1 = new Vector3(highX, rise, z1);

            ProBuilderMesh pb = BuildConvex(new List<Vector3[]>
            {
                new[] { a0, b0, b1, a1 }, // bottom
                new[] { b0, c0, c1, b1 }, // vertical back
                new[] { a0, a1, c1, c0 }, // slope
                new[] { a0, b0, c0 },     // front cap
                new[] { a1, b1, c1 },     // back cap
            });
            pb.transform.position = new Vector3(startX, 0f, 0f);
            Finish(pb, parent, name, mat, GroundLayer, true);
        }

        /// <summary>
        /// Builds a convex solid from polygons given in any winding; each face is flipped to point
        /// away from the solid's centre and gets its own vertices so shading stays flat.
        /// </summary>
        static ProBuilderMesh BuildConvex(List<Vector3[]> polygons)
        {
            Vector3 centre = Vector3.zero;
            int count = 0;
            foreach (var poly in polygons)
            foreach (var p in poly) { centre += p; count++; }
            centre /= count;

            var positions = new List<Vector3>();
            var faces = new List<Face>();
            foreach (var source in polygons)
            {
                var poly = (Vector3[])source.Clone();
                Vector3 polyCentre = Vector3.zero;
                foreach (var p in poly) polyCentre += p;
                polyCentre /= poly.Length;

                // Unity treats clockwise triangles as front-facing; Cross(b - a, c - a) is their outward normal.
                Vector3 normal = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]);
                if (Vector3.Dot(normal, polyCentre - centre) < 0f)
                    System.Array.Reverse(poly);

                int first = positions.Count;
                positions.AddRange(poly);
                var tris = new List<int>();
                for (int i = 1; i < poly.Length - 1; i++)
                {
                    tris.Add(first);
                    tris.Add(first + i);
                    tris.Add(first + i + 1);
                }
                faces.Add(new Face(tris));
            }

            return ProBuilderMesh.Create(positions, faces);
        }

        static void Finish(ProBuilderMesh pb, Transform parent, string name, Material mat, int layer, bool collider)
        {
            GameObject go = pb.gameObject;
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, true);

            // World-space UVs keep the 1 m grid aligned across every piece.
            foreach (Face face in pb.faces)
                face.uv = new AutoUnwrapSettings(face.uv) { useWorldSpace = true };

            pb.SetMaterial(pb.faces, mat);
            pb.ToMesh();
            pb.Refresh();

            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = null;
                pb.Refresh(RefreshMask.Collisions);
            }
        }

        // ------------------------------------------------------------------ assets & settings

        static Mats CreateMaterials()
        {
            Directory.CreateDirectory(MaterialDir);
            Texture2D grid = GetOrCreateGridTexture();
            return new Mats
            {
                Floor = GetOrCreateMaterial("M_Greybox_Floor", new Color(0.85f, 0.85f, 0.85f), grid),
                Wall = GetOrCreateMaterial("M_Greybox_Wall", new Color(0.70f, 0.74f, 0.82f), grid),
                OneWay = GetOrCreateMaterial("M_Greybox_OneWay", new Color(1.00f, 0.62f, 0.25f), grid),
                Background = GetOrCreateMaterial("M_Greybox_Background", new Color(0.35f, 0.37f, 0.42f), grid),
            };
        }

        static Material GetOrCreateMaterial(string name, Color tint, Texture2D grid)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", grid);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D GetOrCreateGridTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(TextureDir);

            const int size = 128;
            var fill = new Color(0.62f, 0.62f, 0.62f);
            var line = new Color(0.40f, 0.40f, 0.40f);
            var half = new Color(0.55f, 0.55f, 0.55f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;     // 1 m lines
                bool mid = x == size / 2 || y == size / 2;                        // 0.5 m lines
                pixels[y * size + x] = edge ? line : mid ? half : fill;
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(GridTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(GridTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GridTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        static void EnsureLayer(int index, string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);

            if (layer.stringValue == layerName)
                return;

            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                Debug.LogError($"[TestLevelBuilder] Layer {index} is already '{layer.stringValue}', expected '{layerName}'. Fix it in Project Settings > Tags and Layers.");
                return;
            }

            layer.stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => !File.Exists(s.path)); // drop entries for deleted or renamed scenes
            if (!scenes.Exists(s => s.path == path))
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
