#nullable enable
using System.Collections.Generic;
using System.IO;
using LToLocal.Game;
using LToLocal.Sim;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LToLocal.EditorTools
{
    /// <summary>
    /// Reproducible project setup, runnable from the menu or from the command line:
    ///   Unity -batchmode -projectPath unity -executeMethod LToLocal.EditorTools.ProjectSetup.SetupAll -quit
    ///   Unity -batchmode -projectPath unity -buildTarget Android -executeMethod LToLocal.EditorTools.ProjectSetup.BuildAndroid -quit
    /// Every scene is generated from code, so the scenes never drift from the simulation's geometry.
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string MaterialsDir = "Assets/Materials";
        private const string GeneratedDir = "Assets/Generated";
        public const string RoundaboutScene = "Assets/Scenes/Roundabout.unity";
        public const string DualCarriagewayScene = "Assets/Scenes/DualCarriageway.unity";
        public const string ApkPath = "Builds/Android/LToLocal.apk";

        /// <summary>Build order = order of the "Course" button. The first one opens when the app starts.</summary>
        private static readonly string[] Scenes = { RoundaboutScene, DualCarriagewayScene };

        [MenuItem("L to Local/Setup Project (player settings, URP, all scenes)")]
        public static void SetupAll()
        {
            ConfigurePlayer();
            ConfigureUrp();
            BuildDualCarriagewayScene();
            BuildRoundaboutScene();
            if (File.Exists("Assets/Scenes/HelloCar.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/HelloCar.unity");
            var list = new List<EditorBuildSettingsScene>();
            foreach (var s in Scenes) list.Add(new EditorBuildSettingsScene(s, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[LToLocal] Setup complete.");
        }

        [MenuItem("L to Local/Build Android APK")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath)!);
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[LToLocal] Android build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // =====================================================================
        // Project settings
        // =====================================================================

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "L to Local";
            PlayerSettings.productName = "L to Local";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ltolocal.app");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ltolocal.app");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }

        private static void ConfigureUrp()
        {
            Directory.CreateDirectory(SettingsDir);
            string assetPath = $"{SettingsDir}/URP_Mobile.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, $"{SettingsDir}/URP_Mobile_Renderer.asset");
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, assetPath);
            }

            urp.renderScale = 1f;
            urp.msaaSampleCount = 2;
            urp.shadowDistance = 60f;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
        }

        // =====================================================================
        // Shared building blocks
        // =====================================================================

        private static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // simulation does its own maths; no PhysX
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private sealed class Palette
        {
            public Material Grass = null!, Verge = null!, Road = null!, White = null!, Barrier = null!, Trunk = null!, Leaves = null!,
                Kerb = null!, SignBlue = null!, SignRed = null!, Pole = null!;
            public Material[] Buildings = null!;
            public CarParts Parts = null!;
            public Material[] CarColours = null!;
            public Material PlayerBlue = null!;

            public static Palette Create() => new Palette
            {
                Grass = Mat("Grass", new Color(0.55f, 0.75f, 0.42f)),
                Verge = Mat("Verge", new Color(0.47f, 0.66f, 0.36f)),
                Road = Mat("Road", new Color(0.36f, 0.37f, 0.40f)),
                White = Mat("LineWhite", new Color(0.95f, 0.95f, 0.95f)),
                Barrier = Mat("Barrier", new Color(0.72f, 0.74f, 0.76f)),
                Kerb = Mat("Kerb", new Color(0.80f, 0.80f, 0.78f)),
                Trunk = Mat("Trunk", new Color(0.48f, 0.35f, 0.23f)),
                Leaves = Mat("Leaves", new Color(0.37f, 0.60f, 0.27f)),
                SignBlue = Mat("SignBlue", new Color(0.10f, 0.35f, 0.75f)),
                SignRed = Mat("SignRed", new Color(0.80f, 0.12f, 0.12f)),
                Pole = Mat("Pole", new Color(0.45f, 0.46f, 0.48f)),
                Buildings = new[]
                {
                    Mat("BuildingSand", new Color(0.85f, 0.78f, 0.65f)),
                    Mat("BuildingBrick", new Color(0.64f, 0.36f, 0.29f)),
                    Mat("BuildingGrey", new Color(0.81f, 0.84f, 0.87f)),
                    Mat("BuildingCream", new Color(0.93f, 0.90f, 0.82f)),
                },
                Parts = new CarParts
                {
                    Glass = Mat("Glass", new Color(0.16f, 0.18f, 0.23f)),
                    Tyre = Mat("Tyre", new Color(0.08f, 0.08f, 0.08f)),
                    PlateYellow = Mat("PlateYellow", new Color(0.97f, 0.83f, 0.09f)),
                    PlateWhite = Mat("PlateWhite", new Color(0.96f, 0.96f, 0.96f)),
                    TailLight = Mat("LightRed", new Color(0.89f, 0.23f, 0.23f)),
                    HeadLight = Mat("HeadLight", new Color(1f, 0.97f, 0.85f)),
                    Indicator = Mat("IndicatorAmber", new Color(1f, 0.62f, 0.05f)),
                    MirrorHousing = Mat("MirrorHousing", new Color(0.12f, 0.12f, 0.14f)),
                },
                CarColours = new[]
                {
                    Mat("CarRed", new Color(0.84f, 0.27f, 0.27f)),
                    Mat("CarSilver", new Color(0.76f, 0.78f, 0.80f)),
                    Mat("CarBlack", new Color(0.15f, 0.16f, 0.18f)),
                    Mat("CarWhite", new Color(0.93f, 0.93f, 0.93f)),
                    Mat("CarGreen", new Color(0.22f, 0.50f, 0.36f)),
                    Mat("CarYellow", new Color(0.93f, 0.75f, 0.20f)),
                },
                PlayerBlue = Mat("CarBlue", new Color(0.18f, 0.44f, 0.87f)),
            };
        }

        private sealed class CarParts
        {
            public Material Glass = null!, Tyre = null!, PlateYellow = null!, PlateWhite = null!,
                TailLight = null!, HeadLight = null!, Indicator = null!, MirrorHousing = null!;
        }

        /// <summary>
        /// Lighting, camera, the player's car, the traffic pool and the Game object with the shared components.
        /// The caller adds the course component and wires <c>trafficView</c>.
        /// </summary>
        private static (GameObject game, TrafficView trafficView) BuildCommon(Palette m, Vector3 carStart, float carYawDeg)
        {
            var light = GameObject.Find("Directional Light");
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 650f;
            RenderSettings.fogColor = new Color(0.80f, 0.89f, 0.98f);

            var cam = Camera.main!;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.80f, 0.89f, 0.98f);
            cam.fieldOfView = 60f;
            cam.farClipPlane = 700f;

            var car = BuildCar("Car", null, m.PlayerBlue, m.Parts, withMirrors: true).transform;
            car.SetPositionAndRotation(carStart, Quaternion.Euler(0, carYawDeg, 0));
            BuildInterior(car, Mat("InteriorDark", new Color(0.26f, 0.27f, 0.30f)), Mat("InteriorTrim", new Color(0.45f, 0.46f, 0.49f)));
            cam.transform.position = car.TransformPoint(new Vector3(0, 3.2f, -7.5f));

            var traffic = new GameObject("Traffic");
            for (int colour = 0; colour < m.CarColours.Length; colour++)
                for (int i = 0; i < 4; i++)
                    BuildCar($"TrafficCar{i}_c{colour}", traffic.transform, m.CarColours[colour], m.Parts, withMirrors: true);
            var trafficView = traffic.AddComponent<TrafficView>();

            var game = new GameObject("Game");
            game.AddComponent<TouchControls>();
            var mirrors = game.AddComponent<MirrorRig>();
            mirrors.car = car;
            var feedback = game.AddComponent<FeedbackView>();
            var drive = game.AddComponent<DriveGame>();
            drive.car = car;
            drive.chaseCamera = cam;
            drive.mirrors = mirrors;
            drive.feedback = feedback;
            drive.playerIndicatorLeft = FindChildren(car, "IndL");
            drive.playerIndicatorRight = FindChildren(car, "IndR");
            return (game, trafficView);
        }

        /// <summary>Low-poly placeholder car, forward = +Z. UK plates: white front, yellow rear.</summary>
        private static GameObject BuildCar(string name, Transform? parent, Material paint, CarParts m, bool withMirrors)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            var t = root.transform;
            Prim(PrimitiveType.Cube, "Body", t, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.6f, 4.2f), paint);
            Prim(PrimitiveType.Cube, "Cabin", t, new Vector3(0, 1.12f, -0.25f), new Vector3(1.55f, 0.48f, 2.2f), m.Glass);
            Prim(PrimitiveType.Cube, "RearPlate", t, new Vector3(0, 0.55f, -2.11f), new Vector3(0.55f, 0.13f, 0.02f), m.PlateYellow);
            Prim(PrimitiveType.Cube, "FrontPlate", t, new Vector3(0, 0.5f, 2.11f), new Vector3(0.55f, 0.13f, 0.02f), m.PlateWhite);
            Prim(PrimitiveType.Cube, "TailLightL", t, new Vector3(-0.6f, 0.72f, -2.11f), new Vector3(0.3f, 0.14f, 0.02f), m.TailLight);
            Prim(PrimitiveType.Cube, "TailLightR", t, new Vector3(0.6f, 0.72f, -2.11f), new Vector3(0.3f, 0.14f, 0.02f), m.TailLight);
            Prim(PrimitiveType.Cube, "HeadLightL", t, new Vector3(-0.6f, 0.68f, 2.11f), new Vector3(0.32f, 0.12f, 0.02f), m.HeadLight);
            Prim(PrimitiveType.Cube, "HeadLightR", t, new Vector3(0.6f, 0.68f, 2.11f), new Vector3(0.32f, 0.12f, 0.02f), m.HeadLight);
            foreach (var (side, x) in new[] { ("L", -1f), ("R", 1f) })
            {
                Prim(PrimitiveType.Cube, $"Ind{side}Rear", t, new Vector3(x * 0.84f, 0.72f, -2.12f), new Vector3(0.14f, 0.14f, 0.03f), m.Indicator).SetActive(false);
                Prim(PrimitiveType.Cube, $"Ind{side}Front", t, new Vector3(x * 0.84f, 0.68f, 2.12f), new Vector3(0.14f, 0.12f, 0.03f), m.Indicator).SetActive(false);
                Prim(PrimitiveType.Cube, $"Ind{side}Side", t, new Vector3(x * 0.91f, 0.75f, 1.5f), new Vector3(0.03f, 0.08f, 0.2f), m.Indicator).SetActive(false);
                if (withMirrors)
                    Prim(PrimitiveType.Cube, $"Mirror{side}", t, new Vector3(x * 1.0f, 0.98f, 0.92f), new Vector3(0.18f, 0.14f, 0.1f), m.MirrorHousing);
            }
            foreach (var (x, z) in new[] { (-0.88f, 1.35f), (0.88f, 1.35f), (-0.88f, -1.35f), (0.88f, -1.35f) })
                Prim(PrimitiveType.Cylinder, "Wheel", t, new Vector3(x, 0.34f, z), new Vector3(0.66f, 0.12f, 0.66f), m.Tyre, new Vector3(0, 0, 90));
            return root;
        }

        /// <summary>Dashboard edge and door lines for the driver's-seat view (a proper interior asset replaces this later).</summary>
        private static void BuildInterior(Transform car, Material dark, Material trim)
        {
            var interior = new GameObject("Interior").transform;
            interior.SetParent(car, false);
            Prim(PrimitiveType.Cube, "Floor", interior, new Vector3(0, 0.93f, -0.45f), new Vector3(1.6f, 0.02f, 2.0f), dark);
            foreach (float side in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "DoorTrim", interior, new Vector3(side * 0.76f, 0.96f, -0.35f), new Vector3(0.04f, 0.08f, 2.0f), trim);
            interior.gameObject.SetActive(false);
        }

        private static GameObject[] FindChildren(Transform root, string prefix)
        {
            var list = new List<GameObject>();
            foreach (Transform c in root) if (c.name.StartsWith(prefix)) list.Add(c.gameObject);
            return list.ToArray();
        }

        private static void Tree(Transform parent, Vector3 at, Palette m)
        {
            Prim(PrimitiveType.Cylinder, "TreeTrunk", parent, at + new Vector3(0, 1.2f, 0), new Vector3(0.35f, 1.2f, 0.35f), m.Trunk);
            Prim(PrimitiveType.Sphere, "TreeTop", parent, at + new Vector3(0, 3.4f, 0), new Vector3(3f, 3.2f, 3f), m.Leaves);
        }

        private static void SaveScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            EditorSceneManager.SaveScene(scene, path);
        }

        // =====================================================================
        // Course: dual carriageway
        // =====================================================================

        private static void BuildDualCarriagewayScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var m = Palette.Create();

            // Ours: x -3.65..3.65 (lanes at -1.825 / +1.825). Central reservation: 3.65..6.65. Oncoming: 6.65..13.95.
            const float length = 2200f, midZ = 950f;
            var world = new GameObject("World").transform;
            Prim(PrimitiveType.Plane, "Ground", world, new Vector3(0, 0, midZ), new Vector3(240, 1, 240), m.Grass);
            Prim(PrimitiveType.Cube, "CarriagewayNorth", world, new Vector3(0, 0.01f, midZ), new Vector3(7.3f, 0.02f, length), m.Road);
            Prim(PrimitiveType.Cube, "CarriagewaySouth", world, new Vector3(10.3f, 0.01f, midZ), new Vector3(7.3f, 0.02f, length), m.Road);
            Prim(PrimitiveType.Cube, "CentralReservation", world, new Vector3(5.15f, 0.05f, midZ), new Vector3(3f, 0.1f, length), m.Verge);
            Prim(PrimitiveType.Cube, "Barrier", world, new Vector3(5.15f, 0.45f, midZ), new Vector3(0.12f, 0.35f, length), m.Barrier);
            foreach (float x in new[] { -3.5f, 3.5f, 6.8f, 13.8f })
                Prim(PrimitiveType.Cube, "EdgeLine", world, new Vector3(x, 0.025f, midZ), new Vector3(0.15f, 0.01f, length), m.White);
            var dashes = new GameObject("LaneLines").transform;
            dashes.SetParent(world, false);
            for (float z = midZ - length / 2; z < midZ + length / 2; z += 9f)
            {
                Prim(PrimitiveType.Cube, "LaneDash", dashes, new Vector3(0f, 0.025f, z), new Vector3(0.1f, 0.01f, 3f), m.White);
                Prim(PrimitiveType.Cube, "LaneDash", dashes, new Vector3(10.3f, 0.025f, z), new Vector3(0.1f, 0.01f, 3f), m.White);
            }

            var rng = new System.Random(2026);
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(world, false);
            for (float z = -60; z < midZ + length / 2; z += 24f)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float edge = side < 0 ? -3.65f : 13.95f;
                    if (rng.NextDouble() < 0.6)
                    {
                        float w = 8 + (float)rng.NextDouble() * 10, d = 8 + (float)rng.NextDouble() * 8, h = 6 + (float)rng.NextDouble() * 14;
                        float x = edge + side * (12 + w / 2 + (float)rng.NextDouble() * 8);
                        Prim(PrimitiveType.Cube, "Building", scenery, new Vector3(x, h / 2, z), new Vector3(w, h, d), m.Buildings[rng.Next(m.Buildings.Length)]);
                    }
                    else
                    {
                        Tree(scenery, new Vector3(edge + side * (5 + (float)rng.NextDouble() * 5), 0, z), m);
                    }
                }
            }

            var (game, trafficView) = BuildCommon(m, new Vector3(-1.825f, 0, 0), 0f);
            game.AddComponent<DualCarriagewayCourse>().trafficView = trafficView;
            SaveScene(scene, DualCarriagewayScene);
        }

        // =====================================================================
        // Course: roundabout (built from the same RoundaboutGeometry the simulation uses)
        // =====================================================================

        private static void BuildRoundaboutScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var m = Palette.Create();
            var g = new RoundaboutGeometry(RoundaboutCourse.Centre);
            var world = new GameObject("World").transform;
            float R = (float)g.RingRadius;
            Vector3 C = V(g.Centre, 0);

            Prim(PrimitiveType.Plane, "Ground", world, C, new Vector3(50, 1, 50), m.Grass);

            // Circulatory carriageway and entry/exit lanes: meshes along the lane centrelines.
            AddStrip(world, "Ring", g.Ring.Points, (float)RoundaboutGeometry.RingHalfWidthM * 2 + 0.4f, closed: true, y: 0.012f, m.Road);
            for (int k = 0; k < g.ArmCount; k++)
            {
                AddStrip(world, $"Entry{k}", g.Entry[k].Points, 4.6f, closed: false, y: 0.014f, m.Road);
                AddStrip(world, $"Exit{k}", g.Exit[k].Points, 4.6f, closed: false, y: 0.014f, m.Road);
            }

            // Central island with a kerb ring and a couple of trees
            Prim(PrimitiveType.Cylinder, "IslandKerb", world, C + Vector3.up * 0.05f, new Vector3((R - 2.6f) * 2, 0.05f, (R - 2.6f) * 2), m.Kerb);
            Prim(PrimitiveType.Cylinder, "Island", world, C + Vector3.up * 0.12f, new Vector3((R - 3.0f) * 2, 0.08f, (R - 3.0f) * 2), m.Verge);
            Tree(world, C + new Vector3(2.5f, 0.2f, 1.5f), m);
            Tree(world, C + new Vector3(-2.5f, 0.2f, -2f), m);

            for (int k = 0; k < g.ArmCount; k++)
            {
                float b = (float)RoundaboutGeometry.ArmBearingsDeg[k];
                var arm = new GameObject($"Arm{k}").transform;
                arm.SetParent(world, false);
                arm.SetPositionAndRotation(C, Quaternion.Euler(0, b, 0)); // local +Z points out along the arm
                float start = R + 3.0f, end = (float)g.ArmLength + 10f, mid = (start + end) / 2;

                Prim(PrimitiveType.Cube, "Carriageway", arm, new Vector3(0, 0.01f, mid), new Vector3(7.3f, 0.02f, end - start), m.Road);
                // Edge lines and centre line (dashed), stopping short of the splitter island
                foreach (float x in new[] { -3.5f, 3.5f })
                    Prim(PrimitiveType.Cube, "EdgeLine", arm, new Vector3(x, 0.025f, (R + 9f + end) / 2), new Vector3(0.15f, 0.01f, end - (R + 9f)), m.White);
                for (float z = R + 14f; z < end; z += 9f)
                    Prim(PrimitiveType.Cube, "CentreDash", arm, new Vector3(0, 0.025f, z), new Vector3(0.12f, 0.01f, 3f), m.White);
                // Splitter island between the entry and exit lanes
                Prim(PrimitiveType.Cube, "Splitter", arm, new Vector3(0, 0.08f, R + 7.5f), new Vector3(1.1f, 0.16f, 8f), m.Kerb);

                // Blue "roundabout" sign on the approach's left verge. Inbound traffic drives toward the centre
                // (local -Z), so its left-hand side is local +X.
                Prim(PrimitiveType.Cylinder, "SignPole", arm, new Vector3(5.0f, 1.1f, R + 45f), new Vector3(0.1f, 1.1f, 0.1f), m.Pole);
                Prim(PrimitiveType.Cylinder, "RoundaboutSign", arm, new Vector3(5.0f, 2.5f, R + 44.95f), new Vector3(1.1f, 0.03f, 1.1f), m.SignBlue, new Vector3(90, 0, 0));
            }

            // Give-way markings: double broken white lines across each entry lane
            for (int k = 0; k < g.ArmCount; k++)
            {
                var (p, h) = g.Entry[k].Sample(g.GiveWayS[k]);
                var lines = new GameObject($"GiveWay{k}").transform;
                lines.SetParent(world, false);
                lines.SetPositionAndRotation(V(p, 0.03f), Quaternion.Euler(0, (float)(h * Mathf.Rad2Deg), 0));
                foreach (float dz in new[] { 0f, -0.45f })
                    for (float x = -1.6f; x <= 1.61f; x += 0.8f)
                        Prim(PrimitiveType.Cube, "Dash", lines, new Vector3(x, 0, dz), new Vector3(0.5f, 0.01f, 0.2f), m.White);
            }

            // Buildings and trees in the four quadrants, away from the roads
            var rng = new System.Random(7);
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(world, false);
            for (int i = 0; i < 140; i++)
            {
                float x = (float)(rng.NextDouble() * 320 - 160), z = (float)(rng.NextDouble() * 320 - 160);
                if (Mathf.Abs(x) < 16 || Mathf.Abs(z) < 16 || x * x + z * z < 40 * 40) continue; // keep clear of arms and ring
                if (rng.NextDouble() < 0.55)
                {
                    float w = 8 + (float)rng.NextDouble() * 10, d = 8 + (float)rng.NextDouble() * 10, h = 5 + (float)rng.NextDouble() * 12;
                    if (Mathf.Abs(x) - w / 2 < 14 || Mathf.Abs(z) - d / 2 < 14) continue;
                    Prim(PrimitiveType.Cube, "Building", scenery, C + new Vector3(x, h / 2, z), new Vector3(w, h, d), m.Buildings[rng.Next(m.Buildings.Length)]);
                }
                else
                {
                    Tree(scenery, C + new Vector3(x, 0, z), m);
                }
            }

            var (startPos, startHeading) = g.Entry[RoundaboutCourse.StartArm].Sample(25);
            var (game, trafficView) = BuildCommon(m, V(startPos, 0), (float)(startHeading * Mathf.Rad2Deg));
            game.AddComponent<RoundaboutCourse>().trafficView = trafficView;
            SaveScene(scene, RoundaboutScene);
        }

        private static Vector3 V(Vec2 p, float y) => new Vector3((float)p.X, y, (float)p.Z);

        /// <summary>Flat road surface following a centreline polyline (saved as a mesh asset).</summary>
        private static void AddStrip(Transform parent, string name, IReadOnlyList<Vec2> pts, float width, bool closed, float y, Material mat)
        {
            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                Vec2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(n - 1, i + 1)];
                if (closed) { prev = pts[(i - 1 + n) % n]; next = pts[(i + 1) % n]; }
                Vec2 dir = next - prev;
                double len = dir.Length;
                Vec2 left = len > 1e-6 ? new Vec2(-dir.Z / len, dir.X / len) : new Vec2(-1, 0);
                verts[i * 2] = V(pts[i] + left * (width / 2), y);
                verts[i * 2 + 1] = V(pts[i] - left * (width / 2), y);
                uvs[i * 2] = new Vector2(0, i);
                uvs[i * 2 + 1] = new Vector2(1, i);
            }

            var tris = new List<int>();
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2, b = ((i + 1) % n) * 2;
                // Two triangles, wound so the top faces up (+Y)
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }

            var mesh = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Ensure the surface faces up; flip winding if needed
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0)
            {
                for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                mesh.triangles = tris.ToArray();
                mesh.RecalculateNormals();
            }

            Directory.CreateDirectory(GeneratedDir);
            string path = $"{GeneratedDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
