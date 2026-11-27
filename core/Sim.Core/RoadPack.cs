using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// A RoadPack (content/schemas/roadpack.v0.schema.json, format 0.1–0.4): a link-level road graph in local metres
    /// (x = east, z = north), produced by the content pipeline from OpenStreetMap (ODbL).
    /// Only what the game uses is read; unknown fields are ignored so newer additive versions still load.
    /// </summary>
    public sealed class RoadPack
    {
        public sealed class Node
        {
            public string Id = "";
            public Vec2 Pos;
            public string Kind = "";
            public bool TrafficSignals;
        }

        public sealed class Link
        {
            public string Id = "", From = "", To = "", Name = "", RoadClass = "";
            public bool OneWay, Roundabout, InMainNetwork = true;
            public List<Vec2> Geometry = new List<Vec2>();
            public int SpeedLimitMph = 30, LanesForward = 1, LanesBackward = 1;
            public double WidthM, Confidence = 1;
            public string Sidewalk = "";
            /// <summary>Motorways (pack v0.7): slip roads carry the junction number; some sections have a hard shoulder.</summary>
            public string JunctionNumber = "";
            public bool HardShoulder;
        }

        public sealed class Crossing
        {
            public string Id = "", Type = "", LinkId = "";
            public Vec2 Pos;
            public double Confidence;
        }

        public sealed class Restriction
        {
            public string Type = "", FromLink = "", ViaNode = "", ToLink = "";
        }

        public sealed class Priority
        {
            public string NodeId = "", ControlledBy = "";
            public HashSet<string> MajorLinks = new HashSet<string>();
            public double Confidence;
        }

        public sealed class SignalPlan
        {
            public string NodeId = "";
            public List<List<string>> Stages = new List<List<string>>();
            public List<double> GreenS = new List<double>();
            public double AmberS = 3, AllRedS = 2, RedAmberS = 2, Confidence;
        }

        public sealed class SpawnPoint
        {
            public string Id = "", LinkId = "";
            public double OffsetM;
        }

        public string FormatVersion = "", PackId = "", DisplayName = "", Attribution = "", Licence = "";
        public bool LeftHandTraffic = true;
        public readonly Dictionary<string, Node> Nodes = new Dictionary<string, Node>();
        public readonly List<Link> Links = new List<Link>();
        public readonly List<Crossing> Crossings = new List<Crossing>();
        public readonly List<Restriction> Restrictions = new List<Restriction>();
        public readonly Dictionary<string, Priority> Priorities = new Dictionary<string, Priority>();
        public readonly Dictionary<string, SignalPlan> SignalPlans = new Dictionary<string, SignalPlan>();
        public readonly List<SpawnPoint> SpawnPoints = new List<SpawnPoint>();
        /// <summary>Motorway emergency areas (pack v0.7), as positions.</summary>
        public readonly List<Vec2> EmergencyAreas = new List<Vec2>();
        /// <summary>Motorway gantries (pack v0.7): position, heading of the traffic under it (radians) and lane count.</summary>
        public readonly List<(Vec2 pos, double heading, int lanes)> Gantries = new List<(Vec2, double, int)>();

        public static RoadPack Parse(string json)
        {
            var root = MiniJson.Obj(MiniJson.Parse(json));
            var pack = new RoadPack
            {
                FormatVersion = root.Get("packFormatVersion") as string ?? "",
                PackId = root.Get("packId") as string ?? "",
                DisplayName = root.Get("displayName") as string ?? "",
                LeftHandTraffic = (root.Get("drivingSide") as string ?? "left") == "left",
            };
            if (root.Get("source") is Dictionary<string, object?> src)
            {
                pack.Attribution = src.Get("attribution") as string ?? "";
                pack.Licence = src.Get("licence") as string ?? "";
            }

            foreach (var o in Objects(root, "nodes"))
            {
                var n = new Node
                {
                    Id = Str(o, "id"), Pos = new Vec2(Num(o, "x"), Num(o, "z")), Kind = Str(o, "kind"),
                    TrafficSignals = o.Get("trafficSignals") is bool b && b,
                };
                pack.Nodes[n.Id] = n;
            }

            foreach (var o in Objects(root, "links"))
            {
                var l = new Link
                {
                    Id = Str(o, "id"), From = Str(o, "from"), To = Str(o, "to"), Name = Str(o, "name"), RoadClass = Str(o, "roadClass"),
                    OneWay = o.Get("oneway") is bool ow && ow,
                    Roundabout = o.Get("junctionRoundabout") is bool jr && jr,
                    InMainNetwork = !(o.Get("inMainNetwork") is bool m) || m,
                    SpeedLimitMph = (int)Num(o, "speedLimitMph", 30),
                    WidthM = Num(o, "widthM", 0),
                    Confidence = Num(o, "confidence", 1),
                    Sidewalk = Str(o, "sidewalk"),
                    JunctionNumber = Str(o, "junctionNumber"),
                    HardShoulder = o.Get("hardShoulder") is bool hs && hs,
                };
                bool hasF = o.Get("lanesForward") is double, hasB = o.Get("lanesBackward") is double;
                l.LanesForward = Math.Max(1, (int)Num(o, "lanesForward", 1));
                l.LanesBackward = l.OneWay ? 0 : Math.Max(1, (int)Num(o, "lanesBackward", 1));
                if (!hasF && !hasB && !l.OneWay) { l.LanesForward = 1; l.LanesBackward = 1; }
                if (o.Get("geometry") is List<object?> g)
                    foreach (var p in g)
                    {
                        var xz = MiniJson.Arr(p);
                        l.Geometry.Add(new Vec2(MiniJson.Num(xz[0]), MiniJson.Num(xz[1])));
                    }
                pack.Links.Add(l);
            }

            foreach (var o in Objects(root, "crossings"))
                pack.Crossings.Add(new Crossing
                {
                    Id = Str(o, "id"), Type = Str(o, "type"), LinkId = Str(o, "linkId"),
                    Pos = new Vec2(Num(o, "x"), Num(o, "z")), Confidence = Num(o, "confidence", 0),
                });

            foreach (var o in Objects(root, "restrictions"))
                pack.Restrictions.Add(new Restriction { Type = Str(o, "type"), FromLink = Str(o, "fromLink"), ViaNode = Str(o, "viaNode"), ToLink = Str(o, "toLink") });

            foreach (var o in Objects(root, "junctionPriority"))
            {
                var p = new Priority { NodeId = Str(o, "nodeId"), ControlledBy = Str(o, "controlledBy"), Confidence = Num(o, "confidence", 0) };
                if (o.Get("majorLinks") is List<object?> ml) foreach (var id in ml) if (id is string s) p.MajorLinks.Add(s);
                pack.Priorities[p.NodeId] = p;
            }

            foreach (var o in Objects(root, "signalPlans"))
            {
                var sp = new SignalPlan
                {
                    NodeId = Str(o, "nodeId"), AmberS = Num(o, "amberS", 3), AllRedS = Num(o, "allRedS", 2),
                    RedAmberS = Num(o, "redAmberS", 2), Confidence = Num(o, "confidence", 0),
                };
                if (o.Get("stages") is List<object?> stages)
                    foreach (var st in stages)
                    {
                        var ids = new List<string>();
                        foreach (var id in MiniJson.Arr(st)) if (id is string s) ids.Add(s);
                        sp.Stages.Add(ids);
                    }
                if (o.Get("greenS") is List<object?> gs) foreach (var v in gs) sp.GreenS.Add(MiniJson.Num(v));
                pack.SignalPlans[sp.NodeId] = sp;
            }

            foreach (var o in Objects(root, "spawnPoints"))
                pack.SpawnPoints.Add(new SpawnPoint { Id = Str(o, "id"), LinkId = Str(o, "linkId"), OffsetM = Num(o, "offsetM", 0) });

            foreach (var o in Objects(root, "emergencyAreas"))
                if (o.Get("x") is double ex && o.Get("z") is double ez) pack.EmergencyAreas.Add(new Vec2(ex, ez));
            foreach (var o in Objects(root, "gantries"))
                if (o.Get("x") is double gx && o.Get("z") is double gz)
                    pack.Gantries.Add((new Vec2(gx, gz), Num(o, "heading") * Math.PI / 180, (int)Num(o, "lanes", 2)));
            return pack;
        }

        private static IEnumerable<Dictionary<string, object?>> Objects(Dictionary<string, object?> root, string key)
        {
            if (root.Get(key) is List<object?> list)
                foreach (var o in list)
                    if (o is Dictionary<string, object?> d) yield return d;
        }

        private static string Str(Dictionary<string, object?> o, string k) => o.Get(k) as string ?? "";
        private static double Num(Dictionary<string, object?> o, string k, double fallback = 0) => o.Get(k) is double d ? d : fallback;
    }

    /// <summary>
    /// Builds a drivable <see cref="LaneGraph"/> from a <see cref="RoadPack"/>: lanes offset from each link's centreline
    /// (lane 0 = nearside), trimmed back from junctions, connected through junctions with turn-appropriate lanes;
    /// one-way streets, turn restrictions, give-way priority and per-junction signal plans are respected.
    /// </summary>
    public sealed class RoadPackGraph
    {
        public LaneGraph Graph { get; }
        public SignalNetwork Signals { get; } = new SignalNetwork();
        public RoadPack Pack { get; }
        /// <summary>Lane segment ids per link: forward and backward, nearside first.</summary>
        public Dictionary<string, (List<int> forward, List<int> backward)> LinkLanes { get; } = new Dictionary<string, (List<int>, List<int>)>();
        /// <summary>Graph node index per pack node id.</summary>
        public Dictionary<string, int> NodeIndex { get; } = new Dictionary<string, int>();
        /// <summary>Lanes where AI traffic may be created (from the pack's spawn points, else every lane).</summary>
        public List<(int lane, double s)> Spawns { get; } = new List<(int, double)>();
        /// <summary>Signalised junction approaches: lane, global signal group, node centre (for drawing heads).</summary>
        public List<(int lane, int group)> SignalApproaches { get; } = new List<(int, int)>();

        public double LaneWidthM { get; }
        private int _groups;

        private sealed class Arm
        {
            public RoadPack.Link Link = null!;
            public bool Forward;          // travelling along the link's geometry
            public List<int> Lanes = null!; // nearside first
            public double Heading;        // travel heading at the node
        }

        public RoadPackGraph(RoadPack pack, bool mainNetworkOnly = true, double laneWidthM = 3.25)
        {
            Pack = pack;
            LaneWidthM = laneWidthM;
            Graph = new LaneGraph(pack.LeftHandTraffic);
            var links = new List<RoadPack.Link>();
            foreach (var l in pack.Links)
                if ((!mainNetworkOnly || l.InMainNetwork) && l.Geometry.Count >= 2 && pack.Nodes.ContainsKey(l.From) && pack.Nodes.ContainsKey(l.To))
                    links.Add(l);

            // Degree and the widest road at each node decide how far lanes stop short of it.
            var degree = new Dictionary<string, int>();
            var widest = new Dictionary<string, double>();
            foreach (var l in links)
                foreach (var n in new[] { l.From, l.To })
                {
                    degree[n] = degree.TryGetValue(n, out int d) ? d + 1 : 1;
                    widest[n] = Math.Max(widest.TryGetValue(n, out double w) ? w : 0, Width(l));
                }

            foreach (var kv in degree)
            {
                var n = pack.Nodes[kv.Key];
                bool sig = pack.SignalPlans.ContainsKey(n.Id);
                NodeIndex[n.Id] = Graph.AddNode(n.Id, n.Pos, sig);
            }

            var arrivals = new Dictionary<string, List<Arm>>();
            var departures = new Dictionary<string, List<Arm>>();
            var groupOf = new Dictionary<(string node, string link), int>();
            foreach (var kv in pack.SignalPlans)
            {
                if (!NodeIndex.ContainsKey(kv.Key)) continue;
                var sp = kv.Value;
                var stages = new List<int[]>();
                var greens = new List<double>();
                for (int i = 0; i < sp.Stages.Count; i++) { stages.Add(new[] { i }); greens.Add(i < sp.GreenS.Count ? sp.GreenS[i] : 15); }
                if (sp.Stages.Count == 1) { stages.Add(Array.Empty<int>()); greens.Add(8); } // crossing on one road: a stage for people
                int first = Signals.Add(new TrafficSignals(stages.ToArray(), greens.ToArray(), sp.AmberS, Math.Max(sp.AllRedS, sp.RedAmberS), sp.RedAmberS), sp.Stages.Count);
                for (int i = 0; i < sp.Stages.Count; i++) foreach (var id in sp.Stages[i]) groupOf[(kv.Key, id)] = first + i;
            }

            foreach (var l in links)
            {
                double len = new PathPolyline(l.Geometry).Length;
                double m0 = Mouth(l.From, degree, widest, len), m1 = Mouth(l.To, degree, widest, len);
                var centre = Geo2.Trim(l.Geometry, m0, len - m1);
                var fwd = new List<int>();
                var back = new List<int>();
                for (int k = 0; k < l.LanesForward; k++)
                    fwd.Add(AddLane(l, centre, true, k, l.LanesForward, l.To, groupOf));
                if (!l.OneWay)
                {
                    var rev = Geo2.Reverse(centre);
                    for (int k = 0; k < l.LanesBackward; k++)
                        back.Add(AddLane(l, rev, false, k, l.LanesBackward, l.From, groupOf));
                }
                LinkLanes[l.Id] = (fwd, back);
                int gf = _groups++, gb = _groups++;
                foreach (int id in fwd) Graph.Segments[id].Group = gf;
                foreach (int id in back) Graph.Segments[id].Group = gb;

                Add(arrivals, l.To, new Arm { Link = l, Forward = true, Lanes = fwd, Heading = Graph.Segments[fwd[0]].Path.Sample(Graph.Segments[fwd[0]].Length).heading });
                Add(departures, l.From, new Arm { Link = l, Forward = true, Lanes = fwd, Heading = Graph.Segments[fwd[0]].Path.Sample(0).heading });
                if (back.Count > 0)
                {
                    Add(arrivals, l.From, new Arm { Link = l, Forward = false, Lanes = back, Heading = Graph.Segments[back[0]].Path.Sample(Graph.Segments[back[0]].Length).heading });
                    Add(departures, l.To, new Arm { Link = l, Forward = false, Lanes = back, Heading = Graph.Segments[back[0]].Path.Sample(0).heading });
                }
            }

            foreach (var kv in arrivals)
            {
                if (!departures.TryGetValue(kv.Key, out var outs)) continue;
                int node = NodeIndex[kv.Key];
                foreach (var a in kv.Value)
                    foreach (var b in outs)
                    {
                        if (b.Link == a.Link) continue; // no U-turns
                        if (Restricted(a.Link.Id, kv.Key, b.Link.Id)) continue;
                        double turn = LaneGraph.Angle(b.Heading - a.Heading);
                        bool ahead = Math.Abs(turn) < 0.6;
                        bool near = !ahead && (turn < 0) == pack.LeftHandTraffic;
                        int nIn = a.Lanes.Count, nOut = b.Lanes.Count;
                        if (nIn == 1 || degree[kv.Key] <= 2)
                        {
                            for (int k = 0; k < nIn; k++)
                                Graph.AddConnector(a.Lanes[k], b.Lanes[ahead ? Math.Min(k, nOut - 1) : near ? 0 : nOut - 1], node);
                        }
                        else if (ahead)
                            for (int k = 0; k < nIn; k++) Graph.AddConnector(a.Lanes[k], b.Lanes[Math.Min(k, nOut - 1)], node);
                        else if (near) Graph.AddConnector(a.Lanes[0], b.Lanes[0], node);
                        else Graph.AddConnector(a.Lanes[nIn - 1], b.Lanes[nOut - 1], node);
                    }
            }

            foreach (var c in pack.Crossings)
            {
                if (!LinkLanes.ContainsKey(c.LinkId)) continue;
                var link = links.Find(x => x.Id == c.LinkId)!;
                var (s, _, _) = new PathPolyline(link.Geometry).Project(c.Pos);
                var (p, h) = new PathPolyline(link.Geometry).Sample(s);
                Graph.AddCrossing(c.Type == "zebra" ? CrossingKind.Zebra : CrossingKind.Other, c.Id, Geo2.Rect(p, h, 1.5, Width(link) / 2 + 0.1));
            }

            Graph.Finalise();

            foreach (var sp in pack.SpawnPoints)
                if (LinkLanes.TryGetValue(sp.LinkId, out var lanes))
                {
                    var lane = Graph.Segments[lanes.forward[0]];
                    Spawns.Add((lane.Id, Math.Max(0, Math.Min(lane.Length - 1, sp.OffsetM))));
                }
        }

        /// <summary>
        /// A named place to start a lesson drive: "start" (a long main road, preferring the motorway main carriageway),
        /// "join" (a motorway slip road) or "leave" (the main carriageway shortly before a slip road leaves it).
        /// </summary>
        public (int lane, double s)? Anchor(string name)
        {
            RoadPack.Link? LinkOf(Segment seg) => Pack.Links.Find(l => l.Id == seg.SourceId);
            bool Motorway(RoadPack.Link? l) => l != null && l.RoadClass == "motorway";
            bool Slip(RoadPack.Link? l) => Motorway(l) && l!.JunctionNumber.Length > 0 && l.LanesForward + l.LanesBackward <= 2 && l.OneWay;
            Segment? best = null;
            double bestLen = 0;
            foreach (var seg in Graph.Segments)
            {
                if (seg.Kind != SegmentKind.Lane || seg.Next.Count == 0) continue;
                var link = LinkOf(seg);
                bool fits = name switch
                {
                    "join" => Slip(link) && seg.Length > 60,
                    "leave" => Motorway(link) && !Slip(link) && seg.Length > 150 && seg.Next.Exists(c => Slip(LinkOf(Graph.Segments[Graph.Segments[c].ToLane]))),
                    _ => Motorway(link) && !Slip(link),
                };
                if (fits && seg.Length > bestLen) { best = seg; bestLen = seg.Length; }
            }
            if (best == null) return name == "start" && Spawns.Count > 0 ? Spawns[0] : ((int, double)?)null;
            double s = name == "leave" ? System.Math.Max(5, best.Length - 250) : System.Math.Min(30, best.Length / 3);
            return (best.Id, s);
        }

        /// <summary>
        /// Walking routes for a city (content/cities/&lt;city&gt;/pedestrians.json, pedroutes.v0). Side-road mouths the
        /// routes cross become H2 crossing areas, so drivers turning there must give way.
        /// </summary>
        public List<PathPolyline> LoadPedestrianRoutes(string json)
        {
            var routes = new List<PathPolyline>();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            if (!(root.Get("routes") is List<object?> list)) return routes;
            foreach (var ro in list)
            {
                var r = MiniJson.Obj(ro);
                var pts = new List<Vec2>();
                if (r.Get("points") is List<object?> ps)
                    foreach (var p in ps) { var xz = MiniJson.Arr(p); pts.Add(new Vec2(MiniJson.Num(xz[0]), MiniJson.Num(xz[1]))); }
                if (pts.Count < 2) continue;
                routes.Add(new PathPolyline(pts));
                if (r.Get("mouthCrossings") is List<object?> mouths)
                    foreach (var mo in mouths)
                    {
                        var m = MiniJson.Obj(mo);
                        int a = (int)MiniJson.Num(m["fromIndex"]), b = (int)MiniJson.Num(m["toIndex"]);
                        if (a < 0 || b >= pts.Count || a >= b) continue;
                        Vec2 from = pts[a], to = pts[b], mid = (from + to) * 0.5;
                        Graph.AddCrossing(CrossingKind.JunctionMouth, $"mouth_{m.Get("nodeId")}", Geo2.Rect(mid, (to - from).Heading, (to - from).Length / 2, 1.5));
                    }
            }
            Graph.Finalise();
            return routes;
        }

        private double Width(RoadPack.Link l) =>
            l.WidthM > 0 ? l.WidthM : (l.LanesForward + l.LanesBackward) * LaneWidthM;

        private double Mouth(string node, Dictionary<string, int> degree, Dictionary<string, double> widest, double linkLen)
        {
            double m = degree[node] <= 2 && !Pack.SignalPlans.ContainsKey(node) ? 1.0 : widest[node] / 2 + 2.0;
            return Math.Min(m, linkLen * 0.35);
        }

        private int AddLane(RoadPack.Link l, List<Vec2> centre, bool forward, int k, int n, string endNode, Dictionary<(string, string), int> groupOf)
        {
            // Two-way: this direction's lanes sit on the near side of the centreline; one-way: lanes centred on it.
            double offset = l.OneWay ? ((n - 1) / 2.0 - k) * LaneWidthM : (n - k - 0.5) * LaneWidthM;
            if (!Pack.LeftHandTraffic) offset = -offset;
            var pts = Geo2.Offset(centre, offset);

            LineControl control = LineControl.None;
            int group = -1;
            double confidence = l.Confidence;
            bool junction = Pack.Priorities.TryGetValue(endNode, out var pr);
            if (groupOf.TryGetValue((endNode, l.Id), out int g)) { control = LineControl.Signals; group = g; }
            else if (junction && pr!.ControlledBy == "roundabout") { control = l.Roundabout ? LineControl.None : LineControl.GiveWay; confidence = Math.Min(confidence, pr.Confidence); }
            else if (junction && pr!.ControlledBy != "signals" && pr.MajorLinks.Count > 0 && !pr.MajorLinks.Contains(l.Id))
            {
                control = pr.ControlledBy == "stop" ? LineControl.Stop : LineControl.GiveWay;
                confidence = Math.Min(confidence, pr.Confidence);
            }

            string dir = forward ? "f" : "b";
            int id = Graph.AddLane($"{(l.Name.Length > 0 ? l.Name : l.RoadClass)} [{l.Id}:{dir}{k}]", pts, l.SpeedLimitMph,
                NodeIndex.TryGetValue(endNode, out int ni) ? ni : -1, control, group);
            var seg = Graph.Segments[id];
            seg.Confidence = confidence;
            seg.SourceId = l.Id;
            if (control == LineControl.Signals) SignalApproaches.Add((id, group));
            return id;
        }

        private bool Restricted(string from, string via, string to)
        {
            bool onlyExists = false, onlyMatch = false;
            foreach (var r in Pack.Restrictions)
            {
                if (r.FromLink != from || r.ViaNode != via) continue;
                if (r.Type.StartsWith("only_")) { onlyExists = true; if (r.ToLink == to) onlyMatch = true; }
                else if (r.ToLink == to) return true;
            }
            return onlyExists && !onlyMatch;
        }

        private static void Add(Dictionary<string, List<Arm>> d, string k, Arm a)
        {
            if (!d.TryGetValue(k, out var l)) d[k] = l = new List<Arm>();
            l.Add(a);
        }
    }
}
