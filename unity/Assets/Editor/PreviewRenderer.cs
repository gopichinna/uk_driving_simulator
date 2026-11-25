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

            // Town: crossroads from above, the zebra, the driver's view at the lights and at the Mill Lane give-way line
            EditorSceneManager.OpenScene(ProjectSetup.TownScene);
            game = Object.FindAnyObjectByType<DriveGame>();
            cam = game.chaseCamera;
            Save(cam, "Builds/Previews/_warmup.png");
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.transform.SetPositionAndRotation(new Vector3(-8, 38, -30), Quaternion.Euler(55, 15, 0));
            Save(cam, "Builds/Previews/town_crossroads.png");
            cam.transform.SetPositionAndRotation(new Vector3(52, 5, -1.75f), Quaternion.Euler(14, 90, 0));
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "BelishaOn" || (t.name == "On" && t.parent != null && t.parent.name == "Red")) t.gameObject.SetActive(true);
            Save(cam, "Builds/Previews/town_zebra.png");
            cam.transform.SetPositionAndRotation(new Vector3(-22, 1.6f, 1.75f), Quaternion.Euler(4, 90, 0));
            cam.fieldOfView = 40f;
            Save(cam, "Builds/Previews/town_signal_closeup.png");
            cam.fieldOfView = 60f;
            cam.transform.SetPositionAndRotation(new Vector3(-60, 160, -60), Quaternion.Euler(90, 0, 0));
            Save(cam, "Builds/Previews/town_aerial.png");
            cam.transform.SetPositionAndRotation(new Vector3(-64, 2.2f, 1.75f), Quaternion.Euler(3, 90, 0));
            cam.fieldOfView = 45f;
            Save(cam, "Builds/Previews/town_direction_sign.png");
            cam.transform.SetPositionAndRotation(new Vector3(-100, 3f, -118.25f), Quaternion.Euler(6, 90, 0));
            Save(cam, "Builds/Previews/town_school.png");
            cam.fieldOfView = 60f;
            var town = new LToLocal.Sim.TownGeometry();
            car = game.car;
            foreach (var (anchor, name) in new[] { ("crossroads_signals", "town_seat_lights"), ("tjunction_north", "town_seat_giveway"), ("school_street", "town_seat_school") })
            {
                var (p, h) = town.AnchorPose(anchor);
                var (seg, s0) = town.Anchors[anchor];
                var lane = town.Graph.Segments[seg];
                (p, h) = lane.Path.Sample(System.Math.Min(lane.Length - 4, s0 + (anchor == "school_street" ? 0 : 42)));
                car.SetPositionAndRotation(new Vector3((float)p.X, 0, (float)p.Z), Quaternion.Euler(0, (float)(h * Mathf.Rad2Deg), 0));
                cam.fieldOfView = game.cockpitFov;
                cam.nearClipPlane = 0.05f;
                cam.transform.SetPositionAndRotation(car.TransformPoint(game.cockpitOffset), car.rotation * Quaternion.Euler(game.cockpitPitch, 0f, 0f));
                Save(cam, $"Builds/Previews/{name}.png");
            }

            // Wolverhampton (beta): from above, and the driver's view at the start of the mock-test route
            EditorSceneManager.OpenScene(ProjectSetup.WolverhamptonScene);
            game = Object.FindAnyObjectByType<DriveGame>();
            cam = game.chaseCamera;
            Save(cam, "Builds/Previews/_warmup.png");
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            RenderSettings.fog = false;
            cam.transform.SetPositionAndRotation(new Vector3(0, 900, 30), Quaternion.Euler(90, 0, 0));
            Save(cam, "Builds/Previews/wolverhampton_aerial.png");
            car = game.car;
            cam.transform.SetPositionAndRotation(car.position + new Vector3(0, 60, 0) - car.forward * 60, Quaternion.LookRotation(car.forward * 60 + Vector3.down * 60));
            Save(cam, "Builds/Previews/wolverhampton_start_oblique.png");
            // The busiest junction, close up
            var built = new LToLocal.Sim.RoadPackGraph(LToLocal.Sim.RoadPack.Parse(System.IO.File.ReadAllText("../content/cities/wolverhampton/roadpack.json")));
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var sg in built.Graph.Segments) if (sg.Kind == LToLocal.Sim.SegmentKind.Connector) counts[sg.Node] = counts.TryGetValue(sg.Node, out var cn) ? cn + 1 : 1;
            int busiest = 0, most = 0;
            foreach (var kv in counts) if (kv.Value > most) { most = kv.Value; busiest = kv.Key; }
            var bc = built.Graph.Nodes[busiest].Centre;
            cam.transform.SetPositionAndRotation(new Vector3((float)bc.X, 45, (float)bc.Z - 40), Quaternion.Euler(48, 0, 0));
            Save(cam, "Builds/Previews/wolverhampton_junction.png");
            // A second, ordinary junction near the start
            cam.transform.SetPositionAndRotation(car.position + Vector3.up * 30 - car.forward * 5, Quaternion.LookRotation(car.forward * 40 + Vector3.down * 30));
            Save(cam, "Builds/Previews/wolverhampton_ahead.png");
            // A placed direction sign, seen by a driver 22 m before it
            var signDoc = LToLocal.Sim.MiniJson.Obj(LToLocal.Sim.MiniJson.Parse(System.IO.File.ReadAllText("../content/cities/wolverhampton/direction_signs.json")));
            var sign0 = LToLocal.Sim.MiniJson.Obj(LToLocal.Sim.MiniJson.Arr(signDoc["signs"])[0]);
            float sh = (float)LToLocal.Sim.MiniJson.Num(sign0["heading"]);
            var signPos = new Vector3((float)LToLocal.Sim.MiniJson.Num(sign0["x"]), 0, (float)LToLocal.Sim.MiniJson.Num(sign0["z"]));
            var back = Quaternion.Euler(0, sh, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, sh + 90, 0) * Vector3.forward;
            cam.fieldOfView = 50f;
            cam.transform.SetPositionAndRotation(signPos - back * 22 + right * 3.5f + Vector3.up * 1.4f, Quaternion.LookRotation(signPos + Vector3.up * 2.3f - (signPos - back * 22 + right * 3.5f + Vector3.up * 1.4f)));
            Save(cam, "Builds/Previews/wolverhampton_sign.png");
            RenderSettings.fog = true;
            cam.fieldOfView = game.cockpitFov;
            cam.nearClipPlane = 0.05f;
            cam.transform.SetPositionAndRotation(car.TransformPoint(game.cockpitOffset), car.rotation * Quaternion.Euler(game.cockpitPitch, 0f, 0f));
            Save(cam, "Builds/Previews/wolverhampton_seat.png");

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
