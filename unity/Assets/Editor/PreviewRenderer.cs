#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LToLocal.EditorTools
{
    /// <summary>Renders camera previews to PNG without a device, for quick visual checks:
    /// Unity -batchmode -projectPath unity -executeMethod LToLocal.EditorTools.PreviewRenderer.RenderViews -quit</summary>
    public static class PreviewRenderer
    {
        public static void RenderViews()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/HelloCar.unity");
            var game = Object.FindFirstObjectByType<LToLocal.Game.HelloCarGame>();
            var car = game.car;
            var cam = game.chaseCamera;
            Directory.CreateDirectory("Builds/Previews");
            var traffic = GameObject.Find("Traffic");
            if (traffic != null)
            {
                foreach (Transform t in traffic.transform) t.gameObject.SetActive(false);
                var blindSpotCar = traffic.transform.GetChild(0);
                blindSpotCar.gameObject.SetActive(true);
                blindSpotCar.position = car.position + new Vector3(3.65f, 0f, -5f); // lane 2, just behind: the blind spot
            }
            var interior = car.Find("Interior");
            if (interior != null) interior.gameObject.SetActive(true);
            foreach (Transform c in car) if (c.name.StartsWith("Mirror")) c.gameObject.SetActive(false);
            Save(cam, "Builds/Previews/_warmup.png"); // first render after load can miss shaders

            // Driver's seat
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = game.cockpitFov;
            cam.transform.SetPositionAndRotation(car.TransformPoint(game.cockpitOffset), car.rotation * Quaternion.Euler(game.cockpitPitch, 0f, 0f));
            Save(cam, "Builds/Previews/seat.png");

            // Shoulder checks (same poses as HelloCarGame)
            if (interior != null) interior.gameObject.SetActive(false);
            cam.fieldOfView = game.shoulderFov;
            cam.transform.SetPositionAndRotation(car.TransformPoint(game.lookOffsetRight), car.rotation * Quaternion.Euler(game.shoulderPitch, game.shoulderYaw, 0f));
            Save(cam, "Builds/Previews/look_right.png");
            cam.transform.SetPositionAndRotation(car.TransformPoint(game.lookOffsetLeft), car.rotation * Quaternion.Euler(game.shoulderPitch, -game.shoulderYaw, 0f));
            Save(cam, "Builds/Previews/look_left.png");
            Debug.Log("[LToLocal] Previews rendered.");
        }

        private static void Save(Camera cam, string path)
        {
            var rt = new RenderTexture(1600, 727, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
        }
    }
}
