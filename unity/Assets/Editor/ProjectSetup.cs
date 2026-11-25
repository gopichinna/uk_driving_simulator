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
            Material road = Mat("Road", new Color(0.36f, 0.37f, 0.40f));
            Material white = Mat("LineWhite", new Color(0.95f, 0.95f, 0.95f));
            Material carBlue = Mat("CarBlue", new Color(0.18f, 0.44f, 0.87f));
            Material glass = Mat("Glass", new Color(0.16f, 0.18f, 0.23f));
            Material tyre = Mat("Tyre", new Color(0.08f, 0.08f, 0.08f));
            Material plateYellow = Mat("PlateYellow", new Color(0.97f, 0.83f, 0.09f));
            Material lightRed = Mat("LightRed", new Color(0.89f, 0.23f, 0.23f));
            Material trunk = Mat("Trunk", new Color(0.48f, 0.35f, 0.23f));
            Material leaves = Mat("Leaves", new Color(0.37f, 0.60f, 0.27f));
            Material[] buildings =
            {
                Mat("BuildingSand", new Color(0.85f, 0.78f, 0.65f)),
                Mat("BuildingBrick", new Color(0.64f, 0.36f, 0.29f)),
                Mat("BuildingGrey", new Color(0.81f, 0.84f, 0.87f)),
                Mat("BuildingCream", new Color(0.93f, 0.90f, 0.82f)),
            };

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

            // World
            var world = new GameObject("World").transform;
            Prim(PrimitiveType.Plane, "Ground", world, new Vector3(0, 0, 450), new Vector3(200, 1, 200), grass);
            Prim(PrimitiveType.Cube, "Road", world, new Vector3(0, 0.01f, 480), new Vector3(7.3f, 0.02f, 1100), road);
            Prim(PrimitiveType.Cube, "EdgeLineLeft", world, new Vector3(-3.45f, 0.025f, 480), new Vector3(0.1f, 0.01f, 1100), white);
            Prim(PrimitiveType.Cube, "EdgeLineRight", world, new Vector3(3.45f, 0.025f, 480), new Vector3(0.1f, 0.01f, 1100), white);
            var dashes = new GameObject("CentreDashes").transform;
            dashes.SetParent(world, false);
            for (float z = -60; z < 1020; z += 12f)
                Prim(PrimitiveType.Cube, "Dash", dashes, new Vector3(0, 0.025f, z), new Vector3(0.1f, 0.01f, 4f), white);

            var rng = new System.Random(2026);
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(world, false);
            for (float z = -40; z < 1000; z += 22f)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    if (rng.NextDouble() < 0.65)
                    {
                        float w = 8 + (float)rng.NextDouble() * 10, d = 8 + (float)rng.NextDouble() * 8, h = 6 + (float)rng.NextDouble() * 14;
                        float x = side * (14 + w / 2 + (float)rng.NextDouble() * 6);
                        Prim(PrimitiveType.Cube, "Building", scenery, new Vector3(x, h / 2, z), new Vector3(w, h, d), buildings[rng.Next(buildings.Length)]);
                    }
                    else
                    {
                        float x = side * (8 + (float)rng.NextDouble() * 5);
                        Prim(PrimitiveType.Cylinder, "TreeTrunk", scenery, new Vector3(x, 1.2f, z), new Vector3(0.35f, 1.2f, 0.35f), trunk);
                        Prim(PrimitiveType.Sphere, "TreeTop", scenery, new Vector3(x, 3.4f, z), new Vector3(3f, 3.2f, 3f), leaves);
                    }
                }
            }

            // Car (low-poly placeholder): forward is +Z
            var car = new GameObject("Car").transform;
            car.position = new Vector3(-1.8f, 0, 0);
            Prim(PrimitiveType.Cube, "Body", car, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.6f, 4.2f), carBlue);
            Prim(PrimitiveType.Cube, "Cabin", car, new Vector3(0, 1.12f, -0.25f), new Vector3(1.55f, 0.48f, 2.2f), glass);
            Prim(PrimitiveType.Cube, "RearPlate", car, new Vector3(0, 0.55f, -2.11f), new Vector3(0.55f, 0.13f, 0.02f), plateYellow);
            Prim(PrimitiveType.Cube, "TailLightL", car, new Vector3(-0.68f, 0.72f, -2.11f), new Vector3(0.32f, 0.14f, 0.02f), lightRed);
            Prim(PrimitiveType.Cube, "TailLightR", car, new Vector3(0.68f, 0.72f, -2.11f), new Vector3(0.32f, 0.14f, 0.02f), lightRed);
            foreach (var (x, z) in new[] { (-0.88f, 1.35f), (0.88f, 1.35f), (-0.88f, -1.35f), (0.88f, -1.35f) })
                Prim(PrimitiveType.Cylinder, "Wheel", car, new Vector3(x, 0.34f, z), new Vector3(0.66f, 0.12f, 0.66f), tyre, new Vector3(0, 0, 90));

            // Game object wiring
            var game = new GameObject("Game");
            game.AddComponent<TouchControls>();
            var hello = game.AddComponent<HelloCarGame>();
            hello.car = car;
            hello.chaseCamera = cam;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
