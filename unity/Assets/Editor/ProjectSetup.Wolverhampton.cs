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

            /// <summary>Flat convex polygon (triangle fan), facing up.</summary>
            public void Polygon(List<Vec2> hull, float y)
            {
                if (hull.Count < 3) return;
                int s = V.Count;
                foreach (var p in hull) V.Add(P(p, y));
                for (int i = 1; i + 1 < hull.Count; i++) T.AddRange(new[] { s, s + i + 1, s + i });
            }

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

        private static List<Vec2> Mid(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
        {
            var m = new List<Vec2>();
            int n = System.Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++) m.Add((a[i] + b[i]) * 0.5);
            return m;
        }

        private static void Dashes(MeshBuilder into, List<Vec2> line, double half, double every)
        {
            if (line.Count < 2) return;
            var path = new PathPolyline(line);
            for (double s = half + 1; s < path.Length - half - 1; s += every)
            {
                var (p, h) = path.Sample(s);
                into.Quad(p - Vec2.FromHeading(h) * half, p + Vec2.FromHeading(h) * half, 0.12f, 0.025f);
            }
        }

        /// <summary>Anticlockwise convex hull (monotone chain).</summary>
        private static List<Vec2> ConvexHull(List<Vec2> pts)
        {
            var p = new List<Vec2>(pts);
            p.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
            if (p.Count < 3) return p;
            double Cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);
            var hull = new List<Vec2>();
            foreach (var v in p) { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], v) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(v); }
            int lower = hull.Count + 1;
            for (int i = p.Count - 2; i >= 0; i--) { var v = p[i]; while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], v) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(v); }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        /// <summary>
        /// Moves a point off the drawn road onto the footway: away from every lane or turning path closer than
        /// half a lane plus <paramref name="clearance"/>. Returns false if no clear spot was found (the sign is skipped).
        /// </summary>
        private static bool PushOffRoad(LaneGraph g, double laneWidth, double clearance, ref Vec2 p)
        {
            var near = new List<Segment>();
            double need = laneWidth / 2 + clearance;
            for (int iter = 0; iter < 12; iter++)
            {
                bool moved = false;
                g.Near(p, need + 4, near);
                foreach (var s in near)
                {
                    var (ss, _, d) = s.Path.Project(p);
                    if (d >= need) continue;
                    var (q, h) = s.Path.Sample(ss);
                    Vec2 away = d > 0.05 ? (p - q) * (1.0 / d) : Vec2.FromHeading(h).LeftNormal;
                    p = q + away * (need + 0.05);
                    moved = true;
                }
                if (!moved) return true;
            }
            return false;
        }

        private static void PlacedSigns(Transform world, Palette m, Material black, LaneGraph g, double laneWidth)
        {
            int skipped = 0;
            var root = new GameObject("PlacedSigns").transform;
            root.SetParent(world, false);
            const string signsPath = "../content/cities/wolverhampton/direction_signs.json", platesPath = "../content/cities/wolverhampton/street_names.json";
            if (File.Exists(signsPath))
            {
                var doc = MiniJson.Obj(MiniJson.Parse(File.ReadAllText(signsPath)));
                foreach (var so in MiniJson.Arr(doc["signs"]))
                {
                    var s = MiniJson.Obj(so);
                    var rows = new List<(string, int)>();
                    foreach (var ro in MiniJson.Arr(s["rows"]))
                    {
                        var r = MiniJson.Obj(ro);
                        string text = r["text"] as string ?? "";
                        if (r.TryGetValue("ref", out var rf) && rf is string refText && refText.Length > 0) text += $" ({refText})";
                        int dir = (r["direction"] as string) switch { "left" => -1, "right" => 1, "ahead_left" => -2, "ahead_right" => 2, _ => 0 };
                        rows.Add((text, dir));
                    }
                    double heading = MiniJson.Num(s["heading"]) * System.Math.PI / 180;
                    var at = new Vec2(MiniJson.Num(s["x"]), MiniJson.Num(s["z"]));
                    int longest = 0;
                    foreach (var (t, _) in rows) longest = System.Math.Max(longest, t.Length);
                    double halfPanel = System.Math.Max(2.7, 0.75 + longest * 0.15) / 2;
                    var original = at;
                    // Keep the whole board (not just the post) clear of the road.
                    if (!PushOffRoad(g, laneWidth, 1.2 + halfPanel, ref at) || (at - original).Length > 10) { skipped++; continue; }
                    DirectionSign(root, at, heading, rows.ToArray(), m, black, (s["style"] as string) == "primary");
                }
            }
            if (File.Exists(platesPath))
            {
                var doc = MiniJson.Obj(MiniJson.Parse(File.ReadAllText(platesPath)));
                foreach (var po in MiniJson.Arr(doc["plates"]))
                {
                    var p = MiniJson.Obj(po);
                    string name = p["name"] as string ?? "";
                    if (name.Length == 0) continue;
                    var plate = new GameObject($"StreetName {name}").transform;
                    plate.SetParent(root, false);
                    var at = new Vec2(MiniJson.Num(p["x"]), MiniJson.Num(p["z"]));
                    var original = at;
                    float pw = 0.6f + name.Length * 0.13f;
                    // Moved far (e.g. onto a roundabout island) means there's no sensible spot: leave it out.
                    if (!PushOffRoad(g, laneWidth, 1.4 + pw / 2, ref at) || (at - original).Length > 8) { skipped++; Object.DestroyImmediate(plate.gameObject); continue; }
                    plate.SetPositionAndRotation(V(at, 0), Quaternion.Euler(0, (float)MiniJson.Num(p["heading"]), 0));
                    float w = 0.6f + name.Length * 0.13f;
                    foreach (float x in new[] { -w / 2 + 0.1f, w / 2 - 0.1f }) Prim(PrimitiveType.Cube, "Leg", plate, new Vector3(x, 0.45f, 0.03f), new Vector3(0.06f, 0.9f, 0.06f), black);
                    Prim(PrimitiveType.Cube, "Border", plate, new Vector3(0, 1.05f, 0), new Vector3(w, 0.42f, 0.04f), black);
                    Prim(PrimitiveType.Cube, "Plate", plate, new Vector3(0, 1.05f, -0.01f), new Vector3(w - 0.06f, 0.36f, 0.04f), m.White);
                    Text(plate, name, new Vector3(0, 1.05f, -0.04f), 0.024f, Color.black, TextAnchor.MiddleCenter);
                }
            }
            Debug.Log($"[LToLocal] Placed signs: {skipped} skipped (no clear spot off the road).");
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

            // The road surface is drawn from the same lanes the traffic and rules use, so markings, kerbs and
            // driving lines always agree. Streets outside the network are scenery (car-park aisles are skipped).
            var roads = new MeshBuilder();
            var footways = new MeshBuilder();
            var lines = new MeshBuilder();
            float w = (float)built.LaneWidthM;
            foreach (var l in pack.Links)
            {
                if (l.InMainNetwork || l.Geometry.Count < 2 || l.RoadClass == "service") continue;
                float rw = (float)System.Math.Max(l.WidthM, (l.LanesForward + l.LanesBackward) * 3.0);
                roads.Strip(l.Geometry, rw, 0.010f);
                if (l.Sidewalk != "none") { footways.Strip(Geo2.Offset(l.Geometry, rw / 2 + 1.4), 2.8f, 0.006f); footways.Strip(Geo2.Offset(l.Geometry, -(rw / 2 + 1.4)), 2.8f, 0.006f); }
            }

            foreach (var kv in built.LinkLanes)
            {
                var (fwd, back) = kv.Value;
                var link = pack.Links.Find(x => x.Id == kv.Key)!;
                foreach (int id in fwd) roads.Strip(g.Segments[id].Path.Points, w + 0.3f, 0.012f);
                foreach (int id in back) roads.Strip(g.Segments[id].Path.Points, w + 0.3f, 0.012f);

                // Footways along the outer edges.
                if (link.Sidewalk != "none")
                {
                    footways.Strip(Geo2.Offset(g.Segments[fwd[0]].Path.Points, w / 2 + 1.5), 2.8f, 0.006f);
                    if (back.Count > 0) footways.Strip(Geo2.Offset(g.Segments[back[0]].Path.Points, w / 2 + 1.5), 2.8f, 0.006f);
                    else if (link.Sidewalk == "both" && link.RoadClass != "trunk" && link.RoadClass != "primary")
                        footways.Strip(Geo2.Offset(g.Segments[fwd[fwd.Count - 1]].Path.Points, -(w / 2 + 1.5)), 2.8f, 0.006f); // not down a central reservation
                }

                // Lane lines between lanes going the same way, and the centre line between directions.
                for (int k = 0; k + 1 < fwd.Count; k++) Dashes(lines, Mid(g.Segments[fwd[k]].Path.Points, g.Segments[fwd[k + 1]].Path.Points), 1.5, 9);
                for (int k = 0; k + 1 < back.Count; k++) Dashes(lines, Mid(g.Segments[back[k]].Path.Points, g.Segments[back[k + 1]].Path.Points), 1.5, 9);
                if (back.Count > 0)
                    Dashes(lines, Mid(g.Segments[fwd[fwd.Count - 1]].Path.Points, Geo2.Reverse(g.Segments[back[back.Count - 1]].Path.Points)), 1.0, 6);
            }

            // Turning paths, and one surface per junction joining all its lane ends (convex hull).
            var atNode = new Dictionary<int, List<Vec2>>();
            foreach (var s in g.Segments)
            {
                if (s.Kind != SegmentKind.Connector) continue;
                roads.Strip(s.Path.Points, w + 0.2f, 0.013f);
                if (!atNode.TryGetValue(s.Node, out var pts)) atNode[s.Node] = pts = new List<Vec2>();
                var path = s.Path;
                for (double d = 0; d <= path.Length + 0.01; d += System.Math.Max(0.5, path.Length / 6))
                {
                    var (p, h) = path.Sample(d);
                    Vec2 left = Vec2.FromHeading(h).LeftNormal * (w / 2);
                    pts.Add(p + left);
                    pts.Add(p - left);
                }
            }
            foreach (var kv in atNode)
            {
                int connectors = 0;
                foreach (var s in g.Segments) if (s.Kind == SegmentKind.Connector && s.Node == kv.Key) connectors++;
                if (connectors > 2) roads.Polygon(ConvexHull(kv.Value), 0.0115f);
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

            // Direction signs and street-name plates placed by the content pipeline (real destinations from OSM).
            PlacedSigns(world, m, Mat("ZebraBlack", new Color(0.07f, 0.07f, 0.08f)), g, built.LaneWidthM);

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
