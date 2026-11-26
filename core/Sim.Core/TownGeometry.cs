using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Hand-authored GB town centre (left-hand traffic), one lane each way, 30 mph with a 20 mph school street:
    /// <code>
    ///                     Station Road (N)
    ///                           |
    ///  High St W ---- B ---------A----- zebra ----- High St E
    ///                 |          |
    ///            Mill Lane   Station Road
    ///                 |          |
    ///                 C-- Church St (20) --D
    ///                            |
    ///                     Station Road (S)
    /// </code>
    /// A = crossroads with traffic lights, advanced stop lines (cycle boxes) and a yellow box.
    /// B = T-junction: Mill Lane gives way to High Street. D = T-junction: Church Street gives way to Station Road.
    /// C = a bend. Pedestrians cross at the zebra and at the mouths of Mill Lane and Church Street.
    /// Lane paths, stop / give-way lines, conflict points and crossings all come from this one geometry.
    /// </summary>
    public sealed class TownGeometry
    {
        public const double LaneWidthM = 3.5;
        public const double HalfRoadM = 3.5;
        public const double FootwayCentreM = 5.5;
        public const double AslDepthM = 5.0;
        public const double SignalMouthM = 8.0;   // advanced (cyclist) stop line, from the crossroads centre
        public const double TMouthM = 6.5;        // give-way line / major-road lane ends, from the other road's centreline
        public const double BendMouthM = 10.0;
        public const double ZebraX = 70.0;
        public const double ZigZagLengthM = 15.0;
        public const int GroupEastWest = 0, GroupNorthSouth = 1;

        public static readonly Vec2 A = new Vec2(0, 0), B = new Vec2(-120, 0), C = new Vec2(-120, -120), D = new Vec2(0, -120);

        public sealed class Road
        {
            public string Name = "";
            public List<Vec2> Centreline = new List<Vec2>();
            public double SpeedLimitMph;
            public int Forward = -1, Backward = -1;
        }

        public LaneGraph Graph { get; } = new LaneGraph(leftHandTraffic: true);
        public List<Road> Roads { get; } = new List<Road>();
        public int NodeA { get; }
        public int NodeB { get; }
        public int NodeC { get; }
        public int NodeD { get; }
        public int Zebra { get; private set; } = -1;
        public int MillLaneMouth { get; private set; } = -1;
        public int ChurchStMouth { get; private set; } = -1;
        /// <summary>Named starting points for lessons: segment id and distance along it.</summary>
        public Dictionary<string, (int segment, double s)> Anchors { get; } = new Dictionary<string, (int, double)>();
        /// <summary>Pedestrian walking routes (footways, with the crossings they use).</summary>
        public List<PathPolyline> PedestrianRoutes { get; } = new List<PathPolyline>();
        /// <summary>Speed-limit signs: position, the heading of traffic that reads it, and the limit.</summary>
        public List<(Vec2 pos, double heading, int mph)> SpeedSigns { get; } = new List<(Vec2, double, int)>();

        private readonly Dictionary<int, List<int>> _in = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> _out = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, int> _roadOf = new Dictionary<int, int>();

        public TownGeometry()
        {
            var g = Graph;
            const double box = 7.0;
            NodeA = g.AddNode("Station Road crossroads", A, signalised: true,
                box: new[] { new Vec2(-box, box), new Vec2(box, box), new Vec2(box, -box), new Vec2(-box, -box) });
            NodeB = g.AddNode("Mill Lane junction", B);
            NodeC = g.AddNode("Mill Lane bend", C);
            NodeD = g.AddNode("Church Street junction", D);


            var none = (LineControl.None, -1);
            var giveWay = (LineControl.GiveWay, -1);

            // name, centreline, start node + mouth, end node + mouth, limit, control at the forward end, control at the backward end
            int highW = AddRoad("High Street", new[] { new Vec2(-220, 0), B }, -1, 0, NodeB, TMouthM, 30, none, none);
            int highMid = AddRoad("High Street", new[] { B, A }, NodeB, TMouthM, NodeA, SignalMouthM, 30,
                (LineControl.Signals, GroupEastWest), none);
            int highE = AddRoad("High Street", new[] { A, new Vec2(220, 0) }, NodeA, SignalMouthM, -1, 0, 30,
                none, (LineControl.Signals, GroupEastWest));
            AddRoad("Station Road", new[] { A, new Vec2(0, 200) }, NodeA, SignalMouthM, -1, 0, 30,
                none, (LineControl.Signals, GroupNorthSouth));
            int stationMid = AddRoad("Station Road", new[] { A, D }, NodeA, SignalMouthM, NodeD, TMouthM, 30,
                none, (LineControl.Signals, GroupNorthSouth));
            AddRoad("Station Road", new[] { D, new Vec2(0, -220) }, NodeD, TMouthM, -1, 0, 30, none, none);
            int mill = AddRoad("Mill Lane", new[] { B, C }, NodeB, TMouthM, NodeC, BendMouthM, 30, none, giveWay);
            int church = AddRoad("Church Street", new[] { C, D }, NodeC, BendMouthM, NodeD, TMouthM, 20, giveWay, none);

            foreach (var node in new[] { NodeA, NodeB, NodeC, NodeD })
                foreach (int from in _in[node])
                    foreach (int to in _out[node])
                        if (_roadOf[from] != _roadOf[to]) g.AddConnector(from, to, node);

            // Zebra crossing on High Street east of the crossroads, with zig-zags either side.
            var zebraCentre = new Vec2(ZebraX, 0);
            Zebra = g.AddCrossing(CrossingKind.Zebra, "High Street zebra", Geo2.Rect(zebraCentre, Math.PI / 2, 1.5, HalfRoadM + 0.1));
            g.ZigZags.Add(Geo2.Rect(new Vec2(ZebraX - 1.5 - ZigZagLengthM / 2, 0), Math.PI / 2, ZigZagLengthM / 2, HalfRoadM));
            g.ZigZags.Add(Geo2.Rect(new Vec2(ZebraX + 1.5 + ZigZagLengthM / 2, 0), Math.PI / 2, ZigZagLengthM / 2, HalfRoadM));

            // People cross the side roads a few metres back from the junction (Highway Code rule H2).
            MillLaneMouth = g.AddCrossing(CrossingKind.JunctionMouth, "Mill Lane mouth",
                Geo2.Rect(B + new Vec2(0, -10), Math.PI, 1.5, HalfRoadM + 0.1));
            ChurchStMouth = g.AddCrossing(CrossingKind.JunctionMouth, "Church Street mouth",
                Geo2.Rect(D + new Vec2(-10, 0), Math.PI / 2, 1.5, HalfRoadM + 0.1));

            g.Finalise();

            // Footway routes (both directions).
            AddWalk(new Vec2(-200, -5.5), new Vec2(-125.5, -5.5), new Vec2(-125, -10), new Vec2(-115, -10), new Vec2(-114.5, -5.5), new Vec2(-40, -5.5));
            AddWalk(new Vec2(-5.5, -40), new Vec2(-5.5, -114.5), new Vec2(-10, -115), new Vec2(-10, -125), new Vec2(-5.5, -125.5), new Vec2(-5.5, -200));
            AddWalk(new Vec2(40, 5.5), new Vec2(ZebraX, 5.5), new Vec2(ZebraX, -5.5), new Vec2(110, -5.5));
            AddWalk(new Vec2(40, -5.5), new Vec2(ZebraX - 0.8, -5.5), new Vec2(ZebraX - 0.8, 5.5), new Vec2(110, 5.5));

            // Speed-limit signs on the driver's left verge: 30 where traffic enters the town, 20 into Church Street.
            SpeedSigns.Add((C + new Vec2(BendMouthM + 3, 5.2), Math.PI / 2, 20));         // Church St eastbound, from the bend
            SpeedSigns.Add((D + new Vec2(-TMouthM - 8, -5.2), -Math.PI / 2, 20));         // Church St westbound, from Station Road
            SpeedSigns.Add((C + new Vec2(-5.2, BendMouthM + 3), 0, 30));                  // Mill Lane northbound, leaving the 20 zone
            SpeedSigns.Add((new Vec2(-205, 5.2), Math.PI / 2, 30));                       // High Street eastbound
            SpeedSigns.Add((new Vec2(205, -5.2), -Math.PI / 2, 30));                      // High Street westbound
            SpeedSigns.Add((new Vec2(5.2, 190), Math.PI, 30));                            // Station Road southbound
            SpeedSigns.Add((new Vec2(-5.2, -205), 0, 30));                                // Station Road northbound

            Anchors["start"] = (Roads[highW].Forward, 25);
            var hm = Graph.Segments[Roads[highMid].Forward];
            Anchors["crossroads_signals"] = (hm.Id, Math.Max(0, hm.StopLineS - 60));
            var millUp = Graph.Segments[Roads[mill].Backward];
            Anchors["tjunction_north"] = (millUp.Id, Math.Max(0, millUp.Length - 50));
            var churchEast = Graph.Segments[Roads[church].Forward];
            Anchors["tjunction_south"] = (churchEast.Id, Math.Max(0, churchEast.Length - 50));
            Anchors["zebra"] = (Roads[highE].Forward, 10);
            var stationNorthbound = Graph.Segments[Roads[stationMid].Backward];
            Anchors["box_junction"] = (stationNorthbound.Id, Math.Max(0, stationNorthbound.StopLineS - 60));
            Anchors["school_street"] = (churchEast.Id, 5);
        }

        /// <summary>Speed limit at a point: the nearest lane's limit.</summary>
        public double SpeedLimitAt(Vec2 p)
        {
            var seg = Graph.Nearest(p, out _, out _, lanesOnly: true);
            return seg?.SpeedLimitMph ?? 30;
        }

        /// <summary>Pose at a named anchor.</summary>
        public (Vec2 pos, double heading) AnchorPose(string name)
        {
            var (seg, s) = Anchors[name];
            return Graph.Segments[seg].Path.Sample(s);
        }

        private int AddRoad(string name, Vec2[] centre, int startNode, double startMouth, int endNode, double endMouth,
            double limit, (LineControl control, int group) forwardEnd, (LineControl control, int group) backwardEnd)
        {
            var road = new Road { Name = name, Centreline = new List<Vec2>(centre), SpeedLimitMph = limit };
            int index = Roads.Count;
            Roads.Add(road);
            var line = new PathPolyline(centre);
            double from = startNode >= 0 ? startMouth : 0, to = line.Length - (endNode >= 0 ? endMouth : 0);

            var fwdPts = Geo2.Offset(Geo2.Trim(centre, from, to), LaneWidthM / 2);
            var backPts = Geo2.Offset(Geo2.Reverse(Geo2.Trim(centre, from, to)), LaneWidthM / 2);
            road.Forward = Graph.AddLane($"{name} ({Dir(centre[0], centre[centre.Length - 1])})", fwdPts, limit, endNode,
                forwardEnd.control, forwardEnd.control == LineControl.Signals ? forwardEnd.group : -1,
                forwardEnd.control == LineControl.Signals ? AslDepthM : 0);
            road.Backward = Graph.AddLane($"{name} ({Dir(centre[centre.Length - 1], centre[0])})", backPts, limit, startNode,
                backwardEnd.control, backwardEnd.control == LineControl.Signals ? backwardEnd.group : -1,
                backwardEnd.control == LineControl.Signals ? AslDepthM : 0);
            _roadOf[road.Forward] = index;
            _roadOf[road.Backward] = index;
            if (endNode >= 0) { List(_in, endNode).Add(road.Forward); List(_out, endNode).Add(road.Backward); }
            if (startNode >= 0) { List(_out, startNode).Add(road.Forward); List(_in, startNode).Add(road.Backward); }
            return index;
        }

        private void AddWalk(params Vec2[] pts)
        {
            PedestrianRoutes.Add(new PathPolyline(pts));
            PedestrianRoutes.Add(new PathPolyline(Geo2.Reverse(pts)));
        }

        private static List<int> List(Dictionary<int, List<int>> d, int k)
        {
            if (!d.TryGetValue(k, out var l)) d[k] = l = new List<int>();
            return l;
        }

        private static string Dir(Vec2 a, Vec2 b)
        {
            Vec2 d = b - a;
            if (Math.Abs(d.X) > Math.Abs(d.Z)) return d.X > 0 ? "eastbound" : "westbound";
            return d.Z > 0 ? "northbound" : "southbound";
        }

        /// <summary>Signal plan for the crossroads: east–west, then north–south.</summary>
        public static TrafficSignals CreateSignals(double greenEastWestS = 18, double greenNorthSouthS = 14) =>
            new TrafficSignals(new[] { new[] { GroupEastWest }, new[] { GroupNorthSouth } }, new[] { greenEastWestS, greenNorthSouthS });
    }
}
