#nullable enable
using System.IO;
using LToLocal.Game;
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
            Directory.CreateDirectory("Builds/Previews");

            // Roundabout: aerial view and the driver's view from the start position
            EditorSceneManager.OpenScene(ProjectSetup.RoundaboutScene);
            var game = Object.FindAnyObjectByType<DriveGame>();
            var cam = game.chaseCamera;
            Save(cam, "Builds/Previews/_warmup.png"); // first render after load can miss shaders

            cam.fieldOfView = 60f;
            cam.transform.SetPositionAndRotation(new Vector3(0, 110, -25), Quaternion.Euler(80, 0, 0));
            Save(cam, "Builds/Previews/roundabout_aerial.png");

            var car = game.car;
            cam.transform.SetPositionAndRotation(car.TransformPoint(new Vector3(0, 3.2f, -7.5f)), car.rotation * Quaternion.Euler(12, 0, 0));
            Save(cam, "Builds/Previews/roundabout_chase.png");

            // Move the car close to the give-way line and look from the driver's seat
            car.position += car.forward * 95f;
            cam.fieldOfView = game.cockpitFov;
            cam.nearClipPlane = 0.05f;
            cam.transform.SetPositionAndRotation(car.TransformPoint(game.cockpitOffset), car.rotation * Quaternion.Euler(game.cockpitPitch, 0f, 0f));
            Save(cam, "Builds/Previews/roundabout_seat.png");

            // Two-lane roundabout: aerial, close-up of markings, and the driver's view on the approach
            EditorSceneManager.OpenScene(ProjectSetup.TwoLaneRoundaboutScene);
            game = Object.FindAnyObjectByType<DriveGame>();
            cam = game.chaseCamera;
            Save(cam, "Builds/Previews/_warmup.png");
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.transform.SetPositionAndRotation(new Vector3(0, 75, -20), Quaternion.Euler(78, 0, 0));
            Save(cam, "Builds/Previews/twolane_aerial.png");
            cam.transform.SetPositionAndRotation(new Vector3(3, 22, -55), Quaternion.Euler(55, 0, 0));
            Save(cam, "Builds/Previews/twolane_approach_markings.png");
            car = game.car;
            car.position += car.forward * 90f;
            cam.fieldOfView = game.cockpitFov;
            cam.nearClipPlane = 0.05f;
            cam.transform.SetPositionAndRotation(car.TransformPoint(game.cockpitOffset), car.rotation * Quaternion.Euler(game.cockpitPitch, 0f, 0f));
            Save(cam, "Builds/Previews/twolane_seat.png");

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
