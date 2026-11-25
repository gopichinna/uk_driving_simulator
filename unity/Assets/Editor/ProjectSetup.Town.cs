#nullable enable
using System.Collections.Generic;
using LToLocal.Game;
using LToLocal.Sim;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LToLocal.EditorTools
{
    /// <summary>Town-centre scene, generated from <see cref="TownGeometry"/> so markings match the simulation exactly.</summary>
    public static partial class ProjectSetup
    {
        public const string TownScene = "Assets/Scenes/Town.unity";

        private static void BuildTownScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var m = Palette.Create();
            var g = new TownGeometry();
            var graph = g.Graph;
            var world = new GameObject("World").transform;

            var pavement = Mat("Pavement", new Color(0.72f, 0.72f, 0.70f));
            var yellow = Mat("BoxYellow", new Color(0.98f, 0.80f, 0.10f));
            var aslGreen = Mat("AslGreen", new Color(0.30f, 0.58f, 0.34f));
            var black = Mat("ZebraBlack", new Color(0.07f, 0.07f, 0.08f));
            var lampOff = Mat("LampOff", new Color(0.13f, 0.13f, 0.14f));
            var lampRed = Glow("LampRed", new Color(1f, 0.12f, 0.10f));
            var lampAmber = Glow("LampAmber", new Color(1f, 0.62f, 0.05f));
            var lampGreen = Glow("LampGreen", new Color(0.15f, 1f, 0.45f));
            var beaconOff = Mat("BeaconOff", new Color(0.55f, 0.38f, 0.10f));
            var housing = Mat("SignalHousing", new Color(0.10f, 0.10f, 0.11f));

            Prim(PrimitiveType.Plane, "Ground", world, new Vector3(0, 0, -10), new Vector3(60, 1, 60), m.Grass);

            // Pavements (wide strips under the roads), roads, and the curved paths through junctions.
            for (int i = 0; i < g.Roads.Count; i++)
            {
                AddStrip(world, $"Town_Footway{i}", g.Roads[i].Centreline, 15f, false, 0.005f, pavement);
                AddStrip(world, $"Town_Road{i}", g.Roads[i].Centreline, 7.2f, false, 0.012f, m.Road);
            }
            Prim(PrimitiveType.Cube, "BendPavement", world, V(TownGeometry.C, 0.004f), new Vector3(15f, 0.002f, 15f), pavement);
            foreach (var s in graph.Segments)
                if (s.Kind == SegmentKind.Connector)
                    AddStrip(world, $"Town_Conn{s.Id}", s.Path.Points, 3.8f, false, 0.013f, m.Road);

            var marks = new GameObject("Markings").transform;
            marks.SetParent(world, false);
            CentreLines(g, marks, m.White);
            Zebra(g, marks, m.White, black);
            YellowBox(graph.Nodes[g.NodeA].Box!, marks, yellow);

            var signals = new GameObject("Signals").transform;
            signals.SetParent(world, false);
            foreach (var lane in graph.Segments)
            {
                if (lane.Kind != SegmentKind.Lane) continue;
                if (lane.EndControl == LineControl.Signals) SignalApproach(graph, lane, marks, signals, m, aslGreen, housing, lampOff, lampRed, lampAmber, lampGreen);
                else if (lane.EndControl == LineControl.GiveWay) GiveWayMarkings(lane, marks, m.White);
            }
            Belishas(signals, m, black, beaconOff, lampAmber);
            foreach (var (pos, heading, mph) in g.SpeedSigns) SpeedSign(world, pos, heading, mph, m);

            Buildings(g, world, m);
            TownSigns(world, m, yellow, black);

            // Pedestrians (pooled; the course positions them).
            var people = new GameObject("Pedestrians").transform;
            var shirts = new[]
            {
                Mat("ShirtRed", new Color(0.80f, 0.25f, 0.25f)), Mat("ShirtBlue", new Color(0.20f, 0.40f, 0.80f)),
                Mat("ShirtGreen", new Color(0.25f, 0.60f, 0.35f)), Mat("ShirtYellow", new Color(0.92f, 0.78f, 0.25f)),
                Mat("ShirtPurple", new Color(0.50f, 0.30f, 0.65f)), Mat("ShirtWhite", new Color(0.92f, 0.92f, 0.92f)),
            };
            var skin = Mat("Skin", new Color(0.80f, 0.62f, 0.48f));
            var trousers = Mat("Trousers", new Color(0.18f, 0.20f, 0.26f));
            for (int i = 0; i < 16; i++)
            {
                var p = new GameObject($"Ped{i}").transform;
                p.SetParent(people, false);
                Prim(PrimitiveType.Cube, "Legs", p, new Vector3(0, 0.42f, 0), new Vector3(0.34f, 0.84f, 0.22f), trousers);
                Prim(PrimitiveType.Capsule, "Body", p, new Vector3(0, 1.17f, 0), new Vector3(0.46f, 0.36f, 0.30f), shirts[i % shirts.Length]);
                Prim(PrimitiveType.Sphere, "Head", p, new Vector3(0, 1.66f, 0), new Vector3(0.24f, 0.27f, 0.24f), skin);
            }

            var (startPos, startHeading) = g.AnchorPose("start");
            var (game, trafficView) = BuildCommon(m, V(startPos, 0), (float)(startHeading * Mathf.Rad2Deg));
            var course = game.AddComponent<TownCourse>();
            course.trafficView = trafficView;
            course.pedestrianRoot = people;
            course.signalsRoot = signals;
            SaveScene(scene, TownScene);
        }

        private static Material Glow(string name, Color c)
        {
            var mat = Mat(name, c);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 1.6f);
            return mat;
        }

        /// <summary>A thin painted line (or bar) between two ground points.</summary>
        private static void Line(Transform parent, Vec2 a, Vec2 b, float width, float y, Material mat, string name = "Line")
        {
            Vec2 d = b - a;
            float len = (float)d.Length;
            if (len < 0.01f) return;
            Prim(PrimitiveType.Cube, name, parent, V((a + b) * 0.5, y), new Vector3(width, 0.01f, len), mat,
                new Vector3(0, (float)(d.Heading * Mathf.Rad2Deg), 0));
        }

        private static void CentreLines(TownGeometry g, Transform parent, Material white)
        {
            foreach (var road in g.Roads)
            {
                var path = new PathPolyline(road.Centreline);
                for (double s = 2; s < path.Length - 2; s += 6)
                {
                    var (p, h) = path.Sample(s);
                    bool nearJunction = false;
                    foreach (var n in g.Graph.Nodes) if ((p - n.Centre).Length < 16) nearJunction = true;
                    bool zigZag = System.Math.Abs(p.Z) < 1 && p.X > TownGeometry.ZebraX - 18 && p.X < TownGeometry.ZebraX + 18;
                    if (nearJunction || zigZag) continue;
                    Line(parent, p - Vec2.FromHeading(h) * 1.0, p + Vec2.FromHeading(h) * 1.0, 0.12f, 0.025f, white, "CentreDash");
                }
            }
        }

        private static void SignalApproach(LaneGraph graph, Segment lane, Transform marks, Transform signals, Palette m,
            Material aslGreen, Material housing, Material off, Material red, Material amber, Material green)
        {
            var (stop, h) = lane.Path.Sample(lane.StopLineS);
            var (front, _) = lane.Path.Sample(lane.Length);
            Vec2 f = Vec2.FromHeading(h), left = f.LeftNormal;
            const double half = TownGeometry.LaneWidthM / 2;

            // First stop line, cycle box (green, with a bicycle) and the advanced line.
            Line(marks, stop + left * half, stop - left * half, 0.3f, 0.026f, m.White, "StopLine");
            Line(marks, front + left * half, front - left * half, 0.3f, 0.026f, m.White, "AdvancedStopLine");
            Line(marks, stop + left * (half - 0.08), front + left * (half - 0.08), 0.15f, 0.026f, m.White, "AslSide");
            Line(marks, stop - left * (half - 0.08), front - left * (half - 0.08), 0.15f, 0.026f, m.White, "AslSide");
            Vec2 mid = (stop + front) * 0.5;
            Prim(PrimitiveType.Cube, "AslBox", marks, V(mid, 0.02f), new Vector3((float)(half * 2 - 0.3), 0.01f, (float)(front - stop).Length - 0.3f), aslGreen,
                new Vector3(0, (float)(h * Mathf.Rad2Deg), 0));
            foreach (double side in new[] { -0.55, 0.55 })
                Prim(PrimitiveType.Cylinder, "BikeWheel", marks, V(mid + left * side, 0.027f), new Vector3(0.75f, 0.004f, 0.75f), m.White);
            Line(marks, mid + left * 0.55, mid - left * 0.55, 0.08f, 0.028f, m.White, "BikeFrame");

            // Primary signal on the nearside at the stop line; secondary across the junction on the far side.
            Vec2 centre = graph.Nodes[lane.EndNode].Centre;
            SignalHead(signals, lane.SignalGroup, stop + left * (half + 0.9), h, housing, m.Pole, off, red, amber, green);
            SignalHead(signals, lane.SignalGroup, centre + f * (TownGeometry.SignalMouthM + 1.0) - left * (TownGeometry.HalfRoadM + 0.9), h, housing, m.Pole, off, red, amber, green);
        }

        private static void SignalHead(Transform parent, int group, Vec2 at, double trafficHeading, Material housing, Material pole,
            Material off, Material red, Material amber, Material green)
        {
            var root = new GameObject($"SignalHead_g{group}").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(V(at, 0), Quaternion.Euler(0, (float)(trafficHeading * Mathf.Rad2Deg) + 180f, 0)); // +Z faces the drivers
            Prim(PrimitiveType.Cylinder, "Pole", root, new Vector3(0, 1.3f, 0), new Vector3(0.12f, 1.3f, 0.12f), pole);
            Prim(PrimitiveType.Cube, "Housing", root, new Vector3(0, 2.9f, 0), new Vector3(0.36f, 1.05f, 0.26f), housing);
            Prim(PrimitiveType.Cube, "Backboard", root, new Vector3(0, 2.9f, -0.14f), new Vector3(0.62f, 1.3f, 0.02f), housing);
            var lamps = new[] { ("Red", 3.23f, red), ("Amber", 2.9f, amber), ("Green", 2.57f, green) };
            foreach (var (name, y, onMat) in lamps)
            {
                var lamp = new GameObject(name).transform;
                lamp.SetParent(root, false);
                lamp.localPosition = new Vector3(0, y, 0.14f);
                Prim(PrimitiveType.Sphere, "Off", lamp, Vector3.zero, new Vector3(0.22f, 0.22f, 0.06f), off);
                Prim(PrimitiveType.Sphere, "On", lamp, new Vector3(0, 0, 0.01f), new Vector3(0.24f, 0.24f, 0.07f), onMat).SetActive(false);
            }
        }

        private static void GiveWayMarkings(Segment lane, Transform marks, Material white)
        {
            var (line, h) = lane.Path.Sample(lane.Length);
            Vec2 f = Vec2.FromHeading(h), left = f.LeftNormal;
            // Double broken lines across the lane at the junction.
            foreach (double back in new[] { 0.0, 0.55 })
                for (double x = -1.5; x <= 1.51; x += 0.75)
                {
                    Vec2 c = line - f * (back + 0.1) + left * x;
                    Line(marks, c - left * 0.25, c + left * 0.25, 0.22f, 0.026f, white, "GiveWayDash");
                }
            // Triangle on the approach (points back toward the driver, like the sign).
            Vec2 baseMid = line - f * 7.0, apex = line - f * 11.5;
            Vec2 bl = baseMid + left * 0.85, br = baseMid - left * 0.85;
            Line(marks, bl, br, 0.18f, 0.026f, white, "GiveWayTriangle");
            Line(marks, bl, apex, 0.18f, 0.026f, white, "GiveWayTriangle");
            Line(marks, br, apex, 0.18f, 0.026f, white, "GiveWayTriangle");
        }

        private static void Zebra(TownGeometry g, Transform marks, Material white, Material black)
        {
            double x0 = TownGeometry.ZebraX;
            int i = 0;
            for (double z = -3.5; z < 3.49; z += 0.5, i++)
                Prim(PrimitiveType.Cube, "ZebraStripe", marks, new Vector3((float)x0, 0.024f, (float)(z + 0.25)), new Vector3(3.0f, 0.01f, 0.5f), i % 2 == 0 ? white : black);
            // Studs / give-way line either side of the stripes
            foreach (double side in new[] { -1, 1 })
                for (double z = -3.4; z <= 3.41; z += 0.6)
                    Prim(PrimitiveType.Cube, "ZebraGiveWay", marks, new Vector3((float)(x0 + side * 2.0), 0.024f, (float)z), new Vector3(0.18f, 0.01f, 0.35f), white);
            // Zig-zags: along both kerbs and the centre, 15 m each side.
            foreach (var zone in g.Graph.ZigZags)
            {
                double minX = double.MaxValue, maxX = double.MinValue;
                foreach (var v in zone) { minX = System.Math.Min(minX, v.X); maxX = System.Math.Max(maxX, v.X); }
                foreach (double z in new[] { -3.25, 0.0, 3.25 })
                {
                    Vec2 prev = new Vec2(minX, z);
                    int k = 0;
                    for (double x = minX + 1.5; x <= maxX + 0.01; x += 1.5, k++)
                    {
                        var next = new Vec2(x, z + (k % 2 == 0 ? 0.22 : -0.22));
                        Line(marks, prev, next, 0.12f, 0.026f, white, "ZigZag");
                        prev = next;
                    }
                }
            }
        }

        private static void Belishas(Transform signals, Palette m, Material black, Material globeOff, Material globeOn)
        {
            foreach (double z in new[] { -4.0, 4.0 })
            {
                var root = new GameObject("BelishaBeacon").transform;
                root.SetParent(signals, false);
                root.position = new Vector3((float)(TownGeometry.ZebraX - 2.0), 0, (float)z);
                for (int i = 0; i < 8; i++)
                    Prim(PrimitiveType.Cylinder, "PoleBand", root, new Vector3(0, 0.15f + i * 0.3f, 0), new Vector3(0.12f, 0.15f, 0.12f), i % 2 == 0 ? black : m.White);
                Prim(PrimitiveType.Sphere, "Globe", root, new Vector3(0, 2.6f, 0), new Vector3(0.4f, 0.4f, 0.4f), globeOff);
                Prim(PrimitiveType.Sphere, "BelishaOn", root, new Vector3(0, 2.6f, 0), new Vector3(0.42f, 0.42f, 0.42f), globeOn).SetActive(false);
            }
        }

        private static void YellowBox(Vec2[] box, Transform marks, Material yellow)
        {
            for (int i = 0; i < box.Length; i++) Line(marks, box[i], box[(i + 1) % box.Length], 0.2f, 0.027f, yellow, "BoxEdge");
            double half = System.Math.Abs(box[0].X);
            for (double c = -2 * half + 1.6; c < 2 * half; c += 1.6)
            {
                double a = System.Math.Max(-half, c - half), b = System.Math.Min(half, c + half);
                Line(marks, new Vec2(a, a - c), new Vec2(b, b - c), 0.14f, 0.027f, yellow, "BoxHatch");   // x - z = c
                Line(marks, new Vec2(a, c - a), new Vec2(b, c - b), 0.14f, 0.027f, yellow, "BoxHatch");   // x + z = c
            }
        }

        private static void SpeedSign(Transform parent, Vec2 at, double trafficHeading, int mph, Palette m)
        {
            var root = new GameObject($"SpeedSign{mph}").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(V(at, 0), Quaternion.Euler(0, (float)(trafficHeading * Mathf.Rad2Deg), 0)); // drivers look along +Z
            Prim(PrimitiveType.Cylinder, "Pole", root, new Vector3(0, 1.1f, 0.05f), new Vector3(0.08f, 1.1f, 0.08f), m.Pole);
            Prim(PrimitiveType.Cylinder, "Ring", root, new Vector3(0, 2.4f, 0), new Vector3(0.75f, 0.02f, 0.75f), m.SignRed, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "Face", root, new Vector3(0, 2.4f, -0.02f), new Vector3(0.58f, 0.02f, 0.58f), m.White, new Vector3(90, 0, 0));
            var text = new GameObject("Limit");
            text.transform.SetParent(root, false);
            text.transform.localPosition = new Vector3(0, 2.4f, -0.05f);
            var tm = text.AddComponent<TextMesh>();
            tm.text = mph.ToString();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 80;
            tm.characterSize = 0.045f;
            tm.fontStyle = FontStyle.Bold;
            tm.color = Color.black;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ---------------- signs and destinations ----------------

        private static Font SignFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void Text(Transform parent, string text, Vector3 local, float size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = anchor;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.fontStyle = FontStyle.Bold;
            tm.color = colour;
            tm.font = SignFont;
            go.GetComponent<MeshRenderer>().sharedMaterial = SignFont.material;
        }

        /// <summary>An arrow drawn with bars, in a sign's local plane (dir: -1 left, 0 ahead, +1 right).</summary>
        private static void Arrow(Transform parent, Vector3 at, int dir, Material mat)
        {
            var a = new GameObject("Arrow").transform;
            a.SetParent(parent, false);
            a.localPosition = at;
            a.localRotation = Quaternion.Euler(0, 0, dir == 0 ? 0 : dir < 0 ? 90 : -90);
            Prim(PrimitiveType.Cube, "Shaft", a, new Vector3(0, -0.04f, 0), new Vector3(0.06f, 0.26f, 0.01f), mat);
            Prim(PrimitiveType.Cube, "HeadL", a, new Vector3(-0.05f, 0.08f, 0), new Vector3(0.06f, 0.15f, 0.01f), mat, new Vector3(0, 0, -40));
            Prim(PrimitiveType.Cube, "HeadR", a, new Vector3(0.05f, 0.08f, 0), new Vector3(0.06f, 0.15f, 0.01f), mat, new Vector3(0, 0, 40));
        }

        /// <summary>UK local direction sign: white panel, black border and text, one row per destination.</summary>
        private static void DirectionSign(Transform parent, Vec2 at, double trafficHeading, (string text, int dir)[] rows, Palette m, Material black)
        {
            var root = new GameObject("DirectionSign").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(V(at, 0), Quaternion.Euler(0, (float)(trafficHeading * Mathf.Rad2Deg), 0)); // drivers look along +Z
            float rowH = 0.42f, w = 2.7f, h = rows.Length * rowH + 0.2f, bottom = 1.9f;
            foreach (float x in new[] { -w / 2 + 0.25f, w / 2 - 0.25f })
                Prim(PrimitiveType.Cylinder, "Pole", root, new Vector3(x, bottom / 2 + h / 2 - 0.1f, 0.06f), new Vector3(0.08f, (bottom + h) / 2, 0.08f), m.Pole);
            Prim(PrimitiveType.Cube, "Border", root, new Vector3(0, bottom + h / 2, 0), new Vector3(w, h, 0.04f), black);
            Prim(PrimitiveType.Cube, "Panel", root, new Vector3(0, bottom + h / 2, -0.01f), new Vector3(w - 0.08f, h - 0.08f, 0.04f), m.White);
            for (int i = 0; i < rows.Length; i++)
            {
                float y = bottom + h - 0.1f - rowH * (i + 0.5f);
                Arrow(root, new Vector3(-w / 2 + 0.3f, y, -0.04f), rows[i].dir, black);
                Text(root, rows[i].text, new Vector3(-w / 2 + 0.55f, y, -0.04f), 0.028f, Color.black);
            }
        }

        /// <summary>Red warning triangle (children / school) with a "School" plate underneath.</summary>
        private static void SchoolSign(Transform parent, Vec2 at, double trafficHeading, Palette m, Material black)
        {
            var root = new GameObject("SchoolWarningSign").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(V(at, 0), Quaternion.Euler(0, (float)(trafficHeading * Mathf.Rad2Deg), 0));
            Prim(PrimitiveType.Cylinder, "Pole", root, new Vector3(0, 1.3f, 0.06f), new Vector3(0.08f, 1.3f, 0.08f), m.Pole);
            // Triangle: red frame from three bars, white middle, two small figures.
            float side = 0.9f, cy = 2.3f;
            var tri = new GameObject("Triangle").transform;
            tri.SetParent(root, false);
            tri.localPosition = new Vector3(0, cy, -0.02f);
            float r = side / Mathf.Sqrt(3f);
            for (int i = 0; i < 3; i++)
            {
                float ang = 90 + i * 120, mid = ang + 60;
                var c = new Vector3(Mathf.Cos(mid * Mathf.Deg2Rad), Mathf.Sin(mid * Mathf.Deg2Rad), 0) * (r * 0.5f);
                Prim(PrimitiveType.Cube, "Edge", tri, c, new Vector3(side, 0.11f, 0.03f), m.SignRed, new Vector3(0, 0, mid - 90));
            }
            Prim(PrimitiveType.Cube, "Fill", tri, new Vector3(0, -0.05f, 0.01f), new Vector3(0.45f, 0.35f, 0.02f), m.White);
            foreach (float x in new[] { -0.08f, 0.08f })
            {
                Prim(PrimitiveType.Cube, "Child", tri, new Vector3(x, -0.08f, -0.01f), new Vector3(0.06f, 0.16f, 0.01f), black);
                Prim(PrimitiveType.Sphere, "Head", tri, new Vector3(x, 0.03f, -0.01f), new Vector3(0.06f, 0.06f, 0.01f), black);
            }
            Prim(PrimitiveType.Cube, "Plate", root, new Vector3(0, 1.62f, -0.01f), new Vector3(0.8f, 0.3f, 0.03f), m.White);
            Text(root, "School", new Vector3(0, 1.62f, -0.04f), 0.022f, Color.black, TextAnchor.MiddleCenter);
        }

        /// <summary>A labelled destination building (railway station, bus station, school).</summary>
        private static void Landmark(Transform parent, string label, Vec2 at, double facing, Vector3 size, Material wall, Palette m, Material black)
        {
            var root = new GameObject($"Landmark_{label}").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(V(at, 0), Quaternion.Euler(0, (float)(facing * Mathf.Rad2Deg), 0)); // +Z faces the road
            Prim(PrimitiveType.Cube, "Building", root, new Vector3(0, size.y / 2, 0), size, wall);
            Prim(PrimitiveType.Cube, "NameBoard", root, new Vector3(0, size.y - 1.4f, size.z / 2 + 0.05f), new Vector3(Mathf.Min(size.x - 0.6f, label.Length * 0.75f + 1.2f), 1.6f, 0.1f), black);
            var t = new GameObject("Face").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(0, size.y - 1.4f, size.z / 2 + 0.11f);
            t.localRotation = Quaternion.Euler(0, 180, 0); // text readable from the road
            Text(t, label, Vector3.zero, 0.11f, Color.white, TextAnchor.MiddleCenter);
        }

        private static void TownSigns(Transform world, Palette m, Material yellow, Material black)
        {
            var signs = new GameObject("Signs").transform;
            signs.SetParent(world, false);
            const double E = System.Math.PI / 2, W = -System.Math.PI / 2, N = 0, S = System.Math.PI;

            // Direction signs on the left verge before the crossroads (left of eastbound = north side, etc.).
            DirectionSign(signs, new Vec2(-48, 6.2), E, new[] { ("Railway Station", -1), ("Bus Station", 0), ("Church St School", 1) }, m, black);
            DirectionSign(signs, new Vec2(48, -6.2), W, new[] { ("Railway Station", 1), ("Town Centre", 0), ("Church St School", -1) }, m, black);
            DirectionSign(signs, new Vec2(6.2, 48), S, new[] { ("Bus Station", -1), ("Church St School", 0), ("Mill Lane", 1) }, m, black);
            DirectionSign(signs, new Vec2(-6.2, -48), N, new[] { ("Railway Station", 0), ("Bus Station", 1), ("Town Centre", -1) }, m, black);
            DirectionSign(signs, new Vec2(-126.2, -40), N, new[] { ("Town Centre", 1), ("Bus Station", 1) }, m, black);

            // School: warning signs on both approaches, and yellow SCHOOL KEEP CLEAR zig-zags outside it.
            SchoolSign(signs, TownGeometry.C + new Vec2(TownGeometry.BendMouthM + 8, 5.4), E, m, black);
            SchoolSign(signs, TownGeometry.D + new Vec2(-TownGeometry.TMouthM - 14, -5.4), W, m, black);
            var marks = new GameObject("SchoolKeepClear").transform;
            marks.SetParent(world, false);
            for (int k = 0; k < 14; k++)
            {
                double x = -80 + k * 1.6;
                Line(marks, new Vec2(x, -123.3 + (k % 2 == 0 ? 0.25 : -0.25)), new Vec2(x + 1.6, -123.3 + (k % 2 == 0 ? -0.25 : 0.25)), 0.14f, 0.026f, yellow, "SchoolZigZag");
            }
            Landmark(signs, "St Peter's Primary School", new Vec2(-68, -135), N, new Vector3(26, 7, 12), Mat("SchoolBrick", new Color(0.70f, 0.42f, 0.32f)), m, black);
            Landmark(signs, "Railway Station", new Vec2(-24, 178), E, new Vector3(14, 9, 30), Mat("StationStone", new Color(0.86f, 0.83f, 0.74f)), m, black);
            Landmark(signs, "Bus Station", new Vec2(170, -16), N, new Vector3(30, 6, 12), Mat("BusStationGlass", new Color(0.62f, 0.72f, 0.80f)), m, black);

            // Bus stop on High Street (west): flag sign and the yellow BUS STOP box marking.
            var stop = new GameObject("BusStop").transform;
            stop.SetParent(signs, false);
            stop.SetPositionAndRotation(new Vector3(-170, 0, 4.6f), Quaternion.Euler(0, 90, 0));
            Prim(PrimitiveType.Cylinder, "Pole", stop, new Vector3(0, 1.4f, 0), new Vector3(0.08f, 1.4f, 0.08f), m.Pole);
            Prim(PrimitiveType.Cylinder, "Flag", stop, new Vector3(0, 2.7f, 0), new Vector3(0.55f, 0.02f, 0.55f), m.SignRed, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "FlagInner", stop, new Vector3(0, 2.7f, -0.02f), new Vector3(0.38f, 0.02f, 0.38f), m.White, new Vector3(90, 0, 0));
            Text(stop, "BUS", new Vector3(0, 2.7f, -0.05f), 0.02f, Color.black, TextAnchor.MiddleCenter);
            Prim(PrimitiveType.Cube, "Shelter", stop, new Vector3(-3, 1.2f, 1.2f), new Vector3(4, 2.4f, 0.1f), Mat("Glass", new Color(0.16f, 0.18f, 0.23f)));
            foreach (double z in new[] { 0.3, 3.2 }) Line(marks, new Vec2(-180, z), new Vec2(-160, z), 0.2f, 0.026f, yellow, "BusStopBox");
            foreach (double x in new[] { -180.0, -160.0 }) Line(marks, new Vec2(x, 0.3), new Vec2(x, 3.2), 0.2f, 0.026f, yellow, "BusStopBox");
        }

        private static readonly Vec2[] Landmarks = { new Vec2(-68, -135), new Vec2(-24, 178), new Vec2(170, -16) };

        private static void Buildings(TownGeometry g, Transform world, Palette m)
        {
            var roads = new List<PathPolyline>();
            foreach (var r in g.Roads) roads.Add(new PathPolyline(r.Centreline));
            double Clearance(Vec2 p)
            {
                double best = double.MaxValue;
                foreach (var r in roads) best = System.Math.Min(best, r.Project(p).distance);
                return best;
            }

            var rng = new System.Random(5);
            var scenery = new GameObject("Buildings").transform;
            scenery.SetParent(world, false);
            foreach (var road in roads)
                foreach (int side in new[] { -1, 1 })
                    for (double s = 6; s < road.Length - 6; s += 13)
                    {
                        var (p, h) = road.Sample(s);
                        Vec2 f = Vec2.FromHeading(h), left = f.LeftNormal * side;
                        double w = 10 + rng.NextDouble() * 2.5, d = 8 + rng.NextDouble() * 4, height = 6 + rng.NextDouble() * 7;
                        Vec2 c = p + left * (8.3 + d / 2);
                        bool ok = true;
                        foreach (var lm in Landmarks) if ((c - lm).Length < 26) ok = false;
                        foreach (var corner in new[] { c + f * (w / 2) + left * (d / 2), c - f * (w / 2) + left * (d / 2), c + f * (w / 2) - left * (d / 2), c - f * (w / 2) - left * (d / 2), c })
                            if (Clearance(corner) < 8.0) ok = false;
                        if (!ok) continue;
                        if (rng.NextDouble() < 0.12) { Tree(scenery, V(p + left * 11, 0), m); continue; }
                        Prim(PrimitiveType.Cube, "Building", scenery, V(c, (float)height / 2), new Vector3((float)w - 0.4f, (float)height, (float)d), m.Buildings[rng.Next(m.Buildings.Length)],
                            new Vector3(0, (float)(h * Mathf.Rad2Deg), 0));
                    }
        }
    }
}
