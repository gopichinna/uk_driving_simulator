#nullable enable
using System.IO;
using LToLocal.Game;
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
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string MaterialsDir = "Assets/Materials";
        private const string ScenePath = "Assets/Scenes/HelloCar.unity";
        public const string ApkPath = "Builds/Android/LToLocal.apk";

        [MenuItem("L to Local/Setup Project (player settings, URP, Hello Car scene)")]
        public static void SetupAll()
        {
            ConfigurePlayer();
            ConfigureUrp();
            BuildHelloCarScene();
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
                scenes = new[] { ScenePath },
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

        private static void BuildHelloCarScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material grass = Mat("Grass", new Color(0.55f, 0.75f, 0.42f));
            Material verge = Mat("Verge", new Color(0.47f, 0.66f, 0.36f));
            Material road = Mat("Road", new Color(0.36f, 0.37f, 0.40f));
            Material white = Mat("LineWhite", new Color(0.95f, 0.95f, 0.95f));
            Material barrier = Mat("Barrier", new Color(0.72f, 0.74f, 0.76f));
            Material trunk = Mat("Trunk", new Color(0.48f, 0.35f, 0.23f));
            Material leaves = Mat("Leaves", new Color(0.37f, 0.60f, 0.27f));
            Material[] buildings =
            {
                Mat("BuildingSand", new Color(0.85f, 0.78f, 0.65f)),
                Mat("BuildingBrick", new Color(0.64f, 0.36f, 0.29f)),
                Mat("BuildingGrey", new Color(0.81f, 0.84f, 0.87f)),
                Mat("BuildingCream", new Color(0.93f, 0.90f, 0.82f)),
            };
            var carParts = new CarParts
            {
                Glass = Mat("Glass", new Color(0.16f, 0.18f, 0.23f)),
                Tyre = Mat("Tyre", new Color(0.08f, 0.08f, 0.08f)),
                PlateYellow = Mat("PlateYellow", new Color(0.97f, 0.83f, 0.09f)),
                PlateWhite = Mat("PlateWhite", new Color(0.96f, 0.96f, 0.96f)),
                TailLight = Mat("LightRed", new Color(0.89f, 0.23f, 0.23f)),
                HeadLight = Mat("HeadLight", new Color(1f, 0.97f, 0.85f)),
                Indicator = Mat("IndicatorAmber", new Color(1f, 0.62f, 0.05f)),
                MirrorHousing = Mat("MirrorHousing", new Color(0.12f, 0.12f, 0.14f)),
            };
            Material[] carColours =
            {
                Mat("CarRed", new Color(0.84f, 0.27f, 0.27f)),
                Mat("CarSilver", new Color(0.76f, 0.78f, 0.80f)),
                Mat("CarBlack", new Color(0.15f, 0.16f, 0.18f)),
                Mat("CarWhite", new Color(0.93f, 0.93f, 0.93f)),
                Mat("CarGreen", new Color(0.22f, 0.50f, 0.36f)),
                Mat("CarYellow", new Color(0.93f, 0.75f, 0.20f)),
            };
            Material playerBlue = Mat("CarBlue", new Color(0.18f, 0.44f, 0.87f));

            // Lighting and atmosphere
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
            cam.transform.position = new Vector3(-1.8f, 3.2f, -7.5f);

            // ---- Dual carriageway along +Z. Ours: x -3.65..3.65 (lanes at -1.825 / +1.825).
            //      Central reservation: 3.65..6.65. Oncoming: 6.65..13.95 (lanes at 8.475 / 12.125).
            const float length = 2200f, midZ = 950f;
            var world = new GameObject("World").transform;
            Prim(PrimitiveType.Plane, "Ground", world, new Vector3(0, 0, midZ), new Vector3(240, 1, 240), grass);
            Prim(PrimitiveType.Cube, "CarriagewayNorth", world, new Vector3(0, 0.01f, midZ), new Vector3(7.3f, 0.02f, length), road);
            Prim(PrimitiveType.Cube, "CarriagewaySouth", world, new Vector3(10.3f, 0.01f, midZ), new Vector3(7.3f, 0.02f, length), road);
            Prim(PrimitiveType.Cube, "CentralReservation", world, new Vector3(5.15f, 0.05f, midZ), new Vector3(3f, 0.1f, length), verge);
            Prim(PrimitiveType.Cube, "Barrier", world, new Vector3(5.15f, 0.45f, midZ), new Vector3(0.12f, 0.35f, length), barrier);
            foreach (float x in new[] { -3.5f, 3.5f, 6.8f, 13.8f })
                Prim(PrimitiveType.Cube, "EdgeLine", world, new Vector3(x, 0.025f, midZ), new Vector3(0.15f, 0.01f, length), white);
            var dashes = new GameObject("LaneLines").transform;
            dashes.SetParent(world, false);
            for (float z = midZ - length / 2; z < midZ + length / 2; z += 9f)
            {
                Prim(PrimitiveType.Cube, "LaneDash", dashes, new Vector3(0f, 0.025f, z), new Vector3(0.1f, 0.01f, 3f), white);
                Prim(PrimitiveType.Cube, "LaneDash", dashes, new Vector3(10.3f, 0.025f, z), new Vector3(0.1f, 0.01f, 3f), white);
            }

            var rng = new System.Random(2026);
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(world, false);
            for (float z = -60; z < midZ + length / 2; z += 24f)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float edge = side < 0 ? -3.65f : 13.95f; // outer edge of the road on this side
                    if (rng.NextDouble() < 0.6)
                    {
                        float w = 8 + (float)rng.NextDouble() * 10, d = 8 + (float)rng.NextDouble() * 8, h = 6 + (float)rng.NextDouble() * 14;
                        float x = edge + side * (12 + w / 2 + (float)rng.NextDouble() * 8);
                        Prim(PrimitiveType.Cube, "Building", scenery, new Vector3(x, h / 2, z), new Vector3(w, h, d), buildings[rng.Next(buildings.Length)]);
                    }
                    else
                    {
                        float x = edge + side * (5 + (float)rng.NextDouble() * 5);
                        Prim(PrimitiveType.Cylinder, "TreeTrunk", scenery, new Vector3(x, 1.2f, z), new Vector3(0.35f, 1.2f, 0.35f), trunk);
                        Prim(PrimitiveType.Sphere, "TreeTop", scenery, new Vector3(x, 3.4f, z), new Vector3(3f, 3.2f, 3f), leaves);
                    }
                }
            }

            // ---- Player car
            var car = BuildCar("Car", null, playerBlue, carParts, withMirrors: true).transform;
            car.position = new Vector3(-1.825f, 0, 0);
            BuildInterior(car, Mat("InteriorDark", new Color(0.26f, 0.27f, 0.30f)), Mat("InteriorTrim", new Color(0.45f, 0.46f, 0.49f)));

            // ---- Traffic pool: 4 cars per colour, hidden until used
            var traffic = new GameObject("Traffic");
            for (int colour = 0; colour < carColours.Length; colour++)
                for (int i = 0; i < 4; i++)
                    BuildCar($"TrafficCar{i}_c{colour}", traffic.transform, carColours[colour], carParts, withMirrors: true);
            var trafficView = traffic.AddComponent<TrafficView>();

            // ---- Game wiring
            var game = new GameObject("Game");
            game.AddComponent<TouchControls>();
            var mirrors = game.AddComponent<MirrorRig>();
            mirrors.car = car;
            var hello = game.AddComponent<HelloCarGame>();
            hello.car = car;
            hello.chaseCamera = cam;
            hello.trafficView = trafficView;
            hello.mirrors = mirrors;
            hello.playerIndicatorLeft = FindChildren(car, "IndL");
            hello.playerIndicatorRight = FindChildren(car, "IndR");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private sealed class CarParts
        {
            public Material Glass = null!, Tyre = null!, PlateYellow = null!, PlateWhite = null!,
                TailLight = null!, HeadLight = null!, Indicator = null!, MirrorHousing = null!;
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
            // Indicator lamps (hidden; blink when signalling)
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

        /// <summary>
        /// Minimal low-poly cabin, shown only when the camera is inside the car (seat view or a shoulder check).
        /// Right-hand drive: the driver sits at x = +0.37, the front passenger at x = -0.37.
        /// </summary>
        private static void BuildInterior(Transform car, Material dark, Material trim)
        {
            var interior = new GameObject("Interior").transform;
            interior.SetParent(car, false);
            // No roof lining or dashboard: they cover too much of a phone screen. The HUD mirrors sit where the roof would be.
            Prim(PrimitiveType.Cube, "Floor", interior, new Vector3(0, 0.93f, -0.45f), new Vector3(1.6f, 0.02f, 2.0f), dark);
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "DoorTrim", interior, new Vector3(side * 0.76f, 0.96f, -0.35f), new Vector3(0.04f, 0.08f, 2.0f), trim);
                // B-pillar roughly level with the driver's head; C-pillar at the back of the rear side window.
                Prim(PrimitiveType.Cube, "BPillar", interior, new Vector3(side * 0.76f, 1.2f, -0.27f), new Vector3(0.05f, 0.32f, 0.08f), dark);
                Prim(PrimitiveType.Cube, "CPillar", interior, new Vector3(side * 0.73f, 1.2f, -1.28f), new Vector3(0.05f, 0.32f, 0.2f), dark);
                Prim(PrimitiveType.Cube, "RearHeadrest", interior, new Vector3(side * 0.4f, 1.2f, -1.12f), new Vector3(0.24f, 0.14f, 0.09f), dark);
            }
            // Rear seat back and front passenger seat (left-hand side in a UK car)
            Prim(PrimitiveType.Cube, "RearSeatBack", interior, new Vector3(0, 1.02f, -1.05f), new Vector3(1.4f, 0.18f, 0.12f), trim);
            Prim(PrimitiveType.Cube, "PassengerSeatBack", interior, new Vector3(-0.37f, 1.02f, -0.4f), new Vector3(0.48f, 0.18f, 0.1f), trim);
            Prim(PrimitiveType.Cube, "PassengerHeadrest", interior, new Vector3(-0.37f, 1.22f, -0.42f), new Vector3(0.24f, 0.15f, 0.09f), dark);
            interior.gameObject.SetActive(false);
        }

        private static GameObject[] FindChildren(Transform root, string prefix)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in root) if (c.name.StartsWith(prefix)) list.Add(c.gameObject);
            return list.ToArray();
        }
    }
}
