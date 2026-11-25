#nullable enable
using System.Collections.Generic;
using System.IO;
using LToLocal.Game;
using LToLocal.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LToLocal.EditorTools
{
    /// <summary>
    /// "Wolverhampton (beta)": real streets from the OSM-derived road pack and building footprints (ODbL).
    /// Thousands of road pieces are merged into a few meshes so the phone draws them cheaply.
    /// </summary>
    public static partial class ProjectSetup
    {
        public const string WolverhamptonScene = "Assets/Scenes/Wolverhampton.unity";
        private const string PackPath = "../content/cities/wolverhampton/roadpack.json";
        private const string BuildingsPath = "../content/cities/wolverhampton/buildings.json";

        /// <summary>Collects quads and polygons into one mesh (32-bit indices).</summary>
        private sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<int> T = new List<int>();

            public void Strip(IReadOnlyList<Vec2> pts, float width, float y)
            {
                int n = pts.Count, start = V.Count;
                if (n < 2) return;
                for (int i = 0; i < n; i++)
                {
                    Vec2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(n - 1, i + 1)], dir = next - prev;
                    double len = dir.Length;
                    Vec2 left = len > 1e-6 ? new Vec2(-dir.Z / len, dir.X / len) : new Vec2(-1, 0);
                    V.Add(P(pts[i] + left * (width / 2), y));
                    V.Add(P(pts[i] - left * (width / 2), y));
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = start + i * 2, b = a + 2;
                    T.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }

            public void Quad(Vec2 a, Vec2 b, float width, float y) => Strip(new[] { a, b }, width, y);

            public void Disc(Vec2 c, float r, float y, int sides = 16)
            {
                int centre = V.Count;
                V.Add(P(c, y));
                for (int i = 0; i < sides; i++)
                {
                    double t = 2 * System.Math.PI * i / sides;
                    V.Add(P(c + new Vec2(System.Math.Sin(t), System.Math.Cos(t)) * r, y));
                }
                for (int i = 0; i < sides; i++) T.AddRange(new[] { centre, centre + 1 + i, centre + 1 + (i + 1) % sides });
            }

            /// <summary>A building: walls plus a flat roof (ear-clipped).</summary>
            public void Prism(List<Vec2> footprint, float height)
            {
                var poly = new List<Vec2>(footprint);
                if (poly.Count > 3 && (poly[0] - poly[poly.Count - 1]).Length < 0.01) poly.RemoveAt(poly.Count - 1);
                if (poly.Count < 3) return;
                if (SignedArea(poly) < 0) poly.Reverse(); // anticlockwise (seen from above)
                for (int i = 0; i < poly.Count; i++)
                {
                    Vec2 a = poly[i], b = poly[(i + 1) % poly.Count];
                    int s = V.Count;
                    V.Add(P(a, 0)); V.Add(P(b, 0)); V.Add(P(b, height)); V.Add(P(a, height));
                    T.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                }
                int roof = V.Count;
                foreach (var p in poly) V.Add(P(p, height));
                foreach (var tri in EarClip(poly)) T.AddRange(new[] { roof + tri.a, roof + tri.c, roof + tri.b });
            }

            public void Save(string name, Transform parent, Material mat)
            {
                if (V.Count == 0) return;
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(V);
                mesh.SetTriangles(T, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                Directory.CreateDirectory(GeneratedDir);
                string path = $"{GeneratedDir}/{name}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }

            private static Vector3 P(Vec2 p, float y) => new Vector3((float)p.X, y, (float)p.Z);

            private static double SignedArea(List<Vec2> p)
            {
                double a = 0;
                for (int i = 0; i < p.Count; i++) { Vec2 u = p[i], v = p[(i + 1) % p.Count]; a += u.X * v.Z - v.X * u.Z; }
                return a / 2;
            }

            private static List<(int a, int b, int c)> EarClip(List<Vec2> poly)
            {
                var result = new List<(int, int, int)>();
                var idx = new List<int>();
                for (int i = 0; i < poly.Count; i++) idx.Add(i);
                int guard = 0;
                while (idx.Count > 3 && guard++ < 10000)
                {
                    bool clipped = false;
                    for (int i = 0; i < idx.Count; i++)
                    {
                        int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                        Vec2 pa = poly[a], pb = poly[b], pc = poly[c];
                        double cross = (pb.X - pa.X) * (pc.Z - pa.Z) - (pb.Z - pa.Z) * (pc.X - pa.X);
                        if (cross <= 1e-9) continue; // reflex
                        bool inside = false;
                        foreach (int k in idx)
                        {
                            if (k == a || k == b || k == c) continue;
                            if (InTri(poly[k], pa, pb, pc)) { inside = true; break; }
                        }
                        if (inside) continue;
                        result.Add((a, b, c));
                        idx.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                    if (!clipped) break; // degenerate: give up on the rest
                }
                if (idx.Count == 3) result.Add((idx[0], idx[1], idx[2]));
                return result;
            }

            private static bool InTri(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
            {
                double d1 = (p.X - b.X) * (a.Z - b.Z) - (a.X - b.X) * (p.Z - b.Z);
                double d2 = (p.X - c.X) * (b.Z - c.Z) - (b.X - c.X) * (p.Z - c.Z);
                double d3 = (p.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (p.Z - a.Z);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                return !(neg && pos);
            }
        }

        private static void BuildWolverhamptonScene()
        {
            if (!File.Exists(PackPath)) { Debug.LogWarning("[LToLocal] Wolverhampton pack missing; scene skipped."); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var m = Palette.Create();
            var pavement = Mat("Pavement", new Color(0.72f, 0.72f, 0.70f));
            var housing = Mat("SignalHousing", new Color(0.10f, 0.10f, 0.11f));
            var lampOff = Mat("LampOff", new Color(0.13f, 0.13f, 0.14f));

            var pack = RoadPack.Parse(File.ReadAllText(PackPath));
            var built = new RoadPackGraph(pack);
            var g = built.Graph;
            var world = new GameObject("World").transform;
            Prim(PrimitiveType.Plane, "Ground", world, new Vector3(0, -0.01f, 30), new Vector3(220, 1, 220), m.Grass);

            // Roads and footways: every street in the pack (non-network streets are scenery), merged.
            var roads = new MeshBuilder();
            var footways = new MeshBuilder();
            var lines = new MeshBuilder();
            var degree = new Dictionary<string, int>();
            foreach (var l in pack.Links) foreach (var n in new[] { l.From, l.To }) degree[n] = degree.TryGetValue(n, out int d) ? d + 1 : 1;
            foreach (var l in pack.Links)
            {
                if (l.Geometry.Count < 2) continue;
                float w = (float)System.Math.Max(l.WidthM, (l.LanesForward + l.LanesBackward) * 3.25);
                roads.Strip(l.Geometry, w + 0.3f, 0.012f);
                if (l.Sidewalk == "both" || l.Sidewalk == "left") footways.Strip(Geo2.Offset(l.Geometry, w / 2 + 1.4), 2.8f, 0.006f);
                if (l.Sidewalk == "both" || l.Sidewalk == "right") footways.Strip(Geo2.Offset(l.Geometry, -(w / 2 + 1.4)), 2.8f, 0.006f);
            }
            var widest = new Dictionary<string, double>();
            foreach (var l in pack.Links)
                foreach (var n in new[] { l.From, l.To })
                    widest[n] = System.Math.Max(widest.TryGetValue(n, out var w0) ? w0 : 0, System.Math.Max(l.WidthM, (l.LanesForward + l.LanesBackward) * 3.25));
            foreach (var n in pack.Nodes.Values)
                if (degree.TryGetValue(n.Id, out int d) && d >= 3) roads.Disc(n.Pos, (float)(widest[n.Id] / 2 + 1.0), 0.011f);
            foreach (var s in g.Segments)
                if (s.Kind == SegmentKind.Connector) roads.Strip(s.Path.Points, 3.4f, 0.013f);

            // Markings: centre lines on two-way roads, lane lines on multi-lane ones, give-way and stop lines.
            foreach (var l in pack.Links)
            {
                if (!l.InMainNetwork || l.Geometry.Count < 2) continue;
                const double w = 3.25;
                var offsets = new List<(double off, bool centre)>();
                if (l.OneWay)
                    for (int k = 1; k < l.LanesForward; k++) offsets.Add(((k - l.LanesForward / 2.0) * w, false));
                else
                {
                    offsets.Add((0, true));
                    for (int k = 1; k < l.LanesForward; k++) offsets.Add((k * w, false));
                    for (int k = 1; k < l.LanesBackward; k++) offsets.Add((-k * w, false));
                }
                foreach (var (off, centre) in offsets)
                {
                    var linePath = new PathPolyline(off == 0 ? l.Geometry : Geo2.Offset(l.Geometry, off));
                    for (double s = 9; s < linePath.Length - 9; s += centre ? 6 : 9)
                    {
                        var (p, h) = linePath.Sample(s);
                        double half = centre ? 1.0 : 1.5;
                        lines.Quad(p - Vec2.FromHeading(h) * half, p + Vec2.FromHeading(h) * half, 0.12f, 0.025f);
                    }
                }
            }
            foreach (var s in g.Segments)
            {
                if (s.Kind != SegmentKind.Lane) continue;
                var (end, h) = s.Path.Sample(s.Length);
                Vec2 f = Vec2.FromHeading(h), left = f.LeftNormal;
                if (s.EndControl == LineControl.Signals)
                    lines.Quad(end + left * 1.6, end - left * 1.6, 0.3f, 0.026f);
                else if (s.EndControl == LineControl.GiveWay)
                    foreach (double back in new[] { 0.1, 0.65 })
                        for (double x = -1.3; x <= 1.31; x += 0.65)
                        {
                            Vec2 c = end - f * back + left * x;
                            lines.Quad(c - left * 0.22, c + left * 0.22, 0.2f, 0.026f);
                        }
            }
            // Zebra crossings: stripes across the road.
            var zebraWhite = new MeshBuilder();
            foreach (var c in g.CrossingAreas)
            {
                var poly = c.Polygon;
                Vec2 a0 = poly[0], a1 = poly[1], a3 = poly[3]; // corners: along the road a0→a3, across a0→a1
                Vec2 across = a1 - a0, along = a3 - a0;
                int n = Mathf.Max(3, (int)(across.Length / 0.5));
                for (int i = 0; i < n; i += 2)
                {
                    Vec2 p0 = a0 + across * ((i + 0.5) / n);
                    zebraWhite.Quad(p0, p0 + along, (float)(across.Length / n), 0.024f);
                }
            }
            roads.Save("Wolv_Roads", world, m.Road);
            footways.Save("Wolv_Footways", world, pavement);
            lines.Save("Wolv_Lines", world, m.White);
            zebraWhite.Save("Wolv_Zebras", world, m.White);

            // Buildings (merged per colour).
            var signals = new GameObject("Signals").transform;
            signals.SetParent(world, false);
            if (File.Exists(BuildingsPath))
            {
                var root = MiniJson.Obj(MiniJson.Parse(File.ReadAllText(BuildingsPath)));
                var byColour = new MeshBuilder[m.Buildings.Length];
                for (int i = 0; i < byColour.Length; i++) byColour[i] = new MeshBuilder();
                int k = 0;
                foreach (var b in MiniJson.Arr(root["buildings"]))
                {
                    var o = MiniJson.Obj(b);
                    var fp = new List<Vec2>();
                    foreach (var p in MiniJson.Arr(o["footprint"])) { var xz = MiniJson.Arr(p); fp.Add(new Vec2(MiniJson.Num(xz[0]), MiniJson.Num(xz[1]))); }
                    float h = o.TryGetValue("heightM", out var hv) && hv is double hd ? (float)hd : 9f;
                    byColour[k++ % byColour.Length].Prism(fp, Mathf.Clamp(h, 3f, 60f));
                }
                for (int i = 0; i < byColour.Length; i++) byColour[i].Save($"Wolv_Buildings{i}", world, m.Buildings[i]);
            }

            // One signal head per signalised approach (nearside lane), facing the drivers.
            var heads = new HashSet<string>();
            foreach (var (lane, group) in built.SignalApproaches)
            {
                var s = g.Segments[lane];
                if (!heads.Add(s.SourceId + ":" + s.Name.Substring(s.Name.LastIndexOf(':') + 1, 1))) continue;
                var (end, h) = s.Path.Sample(s.Length);
                SignalHead(signals, group, end + Vec2.FromHeading(h).LeftNormal * 2.6, h, housing, m.Pole, lampOff,
                    Glow("LampRed", new Color(1f, 0.12f, 0.10f)), Glow("LampAmber", new Color(1f, 0.62f, 0.05f)), Glow("LampGreen", new Color(0.15f, 1f, 0.45f)));
            }

            // Start on the longest two-way public road in the network (the mock-test route begins in a car park).
            string startLink = "";
            double bestLen = 0, startOffset = 10;
            foreach (var l in pack.Links)
            {
                if (!l.InMainNetwork || l.OneWay || l.RoadClass == "service" || !built.LinkLanes.ContainsKey(l.Id)) continue;
                double len = new PathPolyline(l.Geometry).Length;
                if (len > bestLen) { bestLen = len; startLink = l.Id; }
            }
            int startLane = startLink.Length > 0 ? built.LinkLanes[startLink].forward[0] : built.Spawns[0].lane;
            var (startPos, startHeading) = g.Segments[startLane].Path.Sample(System.Math.Min(startOffset, g.Segments[startLane].Length - 1));

            var (game, trafficView) = BuildCommon(m, V(startPos, 0), (float)(startHeading * Mathf.Rad2Deg));
            RenderSettings.fogStartDistance = 200f;
            RenderSettings.fogEndDistance = 550f;
            var course = game.AddComponent<WolverhamptonCourse>();
            course.trafficView = trafficView;
            course.signalsRoot = signals;
            course.startLink = g.Segments[startLane].SourceId;
            course.startOffset = (float)startOffset;
            SaveScene(scene, WolverhamptonScene);
        }
    }
}
