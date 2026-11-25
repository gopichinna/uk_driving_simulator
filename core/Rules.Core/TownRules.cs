using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>A painted line across a lane, crossed by traffic travelling along <see cref="Heading"/>.</summary>
    public sealed class LaneLine
    {
        public LaneLine(Vec2 a, Vec2 b, double heading)
        {
            A = a;
            B = b;
            Heading = heading;
        }

        public Vec2 A { get; }
        public Vec2 B { get; }
        public double Heading { get; }
        public Vec2 Mid => (A + B) * 0.5;

        /// <summary>Line across a lane at a point, for traffic heading along the lane.</summary>
        public static LaneLine Across(Vec2 p, double heading, double halfWidth = 1.8)
        {
            Vec2 l = Vec2.FromHeading(heading).LeftNormal;
            return new LaneLine(p + l * halfWidth, p - l * halfWidth, heading);
        }

        /// <summary>Did a point move across this line (forwards) between two frames?</summary>
        public bool CrossedBy(Vec2 from, Vec2 to) =>
            (to - from).Dot(Vec2.FromHeading(Heading)) > 0 && Geo2.SegmentIntersect(from, to, A, B, out _, out _);

        /// <summary>Distance to the line along its heading (+ = still before it), and the sideways offset.</summary>
        public (double along, double lateral) Measure(Vec2 p)
        {
            Vec2 f = Vec2.FromHeading(Heading), d = Mid - p;
            return (d.Dot(f), Math.Abs(d.Dot(f.LeftNormal)));
        }
    }

    /// <summary>The player car's footprint (4.3 m × 1.8 m), centred on its state position.</summary>
    public static class CarBody
    {
        public const double HalfLength = 2.15, HalfWidth = 0.9;
        public static Vec2 Front(in CarState s) => new Vec2(s.X, s.Z) + Vec2.FromHeading(s.Heading) * HalfLength;
        public static Vec2 Centre(in CarState s) => new Vec2(s.X, s.Z);

        /// <summary>Gap between a point and the car's body (0 if touching).</summary>
        public static double Clearance(in CarState s, Vec2 p)
        {
            Vec2 f = Vec2.FromHeading(s.Heading), c = Centre(s), d = p - c;
            double along = Math.Max(-HalfLength, Math.Min(HalfLength, d.Dot(f)));
            Vec2 nearest = c + f * along;
            return Math.Max(0, (p - nearest).Length - HalfWidth);
        }
    }

    /// <summary>
    /// GB.TL.RED (rules 175–176), GB.TL.RED_AMBER, GB.TL.AMBER (rule 175) and GB.TL.ASL (rule 178).
    /// With an advanced stop line, drivers must stop at the first line on amber or red; if they were already
    /// past it when the light turned red, they must stop at the second.
    /// </summary>
    public sealed class TrafficLightRule
    {
        public const string RedId = "GB.TL.RED", RedAmberId = "GB.TL.RED_AMBER", AmberId = "GB.TL.AMBER", AslId = "GB.TL.ASL";

        public sealed class Approach
        {
            public Approach(int group, LaneLine stopLine, LaneLine? advancedLine, Vec2 junctionCentre)
            {
                Group = group;
                StopLine = stopLine;
                AdvancedLine = advancedLine;
                JunctionCentre = junctionCentre;
            }

            public int Group { get; }
            /// <summary>Where motor vehicles stop.</summary>
            public LaneLine StopLine { get; }
            /// <summary>The second line, at the front of the cycle box (or null).</summary>
            public LaneLine? AdvancedLine { get; }
            public Vec2 JunctionCentre { get; }
            internal bool CouldStopAtAmber;
            internal bool PraisedThisRed;
        }

        private readonly List<Approach> _approaches;
        private readonly Dictionary<int, LightState> _last = new Dictionary<int, LightState>();
        private Vec2 _prevFront;
        private bool _hasPrev;

        public TrafficLightRule(List<Approach> approaches) => _approaches = approaches;

        public double ComfortDecelMps2 = 3.0;

        public void Reset()
        {
            _last.Clear();
            _hasPrev = false;
            foreach (var a in _approaches) { a.CouldStopAtAmber = false; a.PraisedThisRed = false; }
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<LightState> lights, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            Vec2 front = CarBody.Front(f.Ego);
            foreach (var a in _approaches)
            {
                var light = lights[a.Group];
                _last.TryGetValue(a.Group, out var before);
                if (light == LightState.Amber && before == LightState.Green)
                {
                    var (along, lateral) = a.StopLine.Measure(front);
                    double v = f.Ego.SpeedMps;
                    a.CouldStopAtAmber = along > 0 && along < 90 && lateral < 2.5 && Aligned(f.Ego, a.StopLine)
                                         && v > 1.0 && along >= v * v / (2 * ComfortDecelMps2) + 0.5;
                }
                if (light == LightState.Green) a.CouldStopAtAmber = false;
                if (light != LightState.Red) a.PraisedThisRed = false;

                if (_hasPrev && a.StopLine.CrossedBy(_prevFront, front))
                {
                    if (light == LightState.Red || light == LightState.RedAmber)
                    {
                        if (a.AdvancedLine != null)
                            output.Add(new RuleEvent(AslId, RuleOutcome.Violation, Severity.Serious, f.Time,
                                "You crossed the first stop line on a red light. The box ahead is for cyclists. Stop at the first line.",
                                "Highway Code rule 178"));
                        else ReportRed(a, light, f, users, output);
                    }
                    else if (light == LightState.Amber && a.CouldStopAtAmber)
                    {
                        output.Add(new RuleEvent(AmberId, RuleOutcome.Violation, Severity.Driving, f.Time,
                            "The light turned amber when you could still stop safely. Amber means stop.",
                            "Highway Code rule 175"));
                        a.CouldStopAtAmber = false;
                    }
                }

                if (_hasPrev && a.AdvancedLine != null && a.AdvancedLine.CrossedBy(_prevFront, front)
                    && (light == LightState.Red || light == LightState.RedAmber))
                    ReportRed(a, light, f, users, output);

                if (light == LightState.Red && !a.PraisedThisRed && f.Ego.SpeedMps < 0.3)
                {
                    var (along, lateral) = a.StopLine.Measure(front);
                    if (along >= -0.2 && along < 6 && lateral < 2.0 && Aligned(f.Ego, a.StopLine))
                    {
                        a.PraisedThisRed = true;
                        output.Add(new RuleEvent(RedId, RuleOutcome.GoodPractice, Severity.None, f.Time,
                            a.AdvancedLine != null ? "Good: you stopped at the first line and left the cycle box clear." : "Good: you stopped at the line on red.",
                            a.AdvancedLine != null ? "Highway Code rule 178" : "Highway Code rule 175"));
                    }
                }
            }
            foreach (var a in _approaches) _last[a.Group] = lights[a.Group];
            _prevFront = front;
            _hasPrev = true;
        }

        private static void ReportRed(Approach a, LightState light, in DriverFrame f, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            bool crossTraffic = false;
            foreach (var u in users)
            {
                if (u.SpeedMps < 1.0 || (u.Position - a.JunctionCentre).Length > 18) continue;
                double c = Math.Abs(Math.Cos(u.Heading - a.StopLine.Heading));
                if (c < 0.7) crossTraffic = true;
            }
            if (light == LightState.RedAmber)
                output.Add(new RuleEvent(RedAmberId, RuleOutcome.Violation, crossTraffic ? Severity.Dangerous : Severity.Serious, f.Time,
                    "You went on red and amber. Red and amber together still mean stop. Wait for green.",
                    "Highway Code: light signals controlling traffic"));
            else
                output.Add(new RuleEvent(RedId, RuleOutcome.Violation, crossTraffic ? Severity.Dangerous : Severity.Serious, f.Time,
                    crossTraffic ? "You drove through a red light into crossing traffic. Red means stop behind the white line."
                                 : "You drove through a red light. Red means stop behind the white line.",
                    "Highway Code rules 175 and 176"));
        }

        private static bool Aligned(in CarState s, LaneLine l) => Math.Cos(s.Heading - l.Heading) > 0.7;
    }

    /// <summary>
    /// GB.JCT.GIVE_WAY (rule 172): at a give-way line, give way to traffic on the major road. Judged by the outcome:
    /// after you cross the line, did anyone with priority have to slow down or brake hard for you?
    /// </summary>
    public sealed class GiveWayRule
    {
        public const string RuleId = "GB.JCT.GIVE_WAY";
        private const string Source = "Highway Code rule 172";

        public sealed class Line
        {
            public Line(LaneLine line, Vec2 junctionCentre)
            {
                Paint = line;
                JunctionCentre = junctionCentre;
            }

            public LaneLine Paint { get; }
            public Vec2 JunctionCentre { get; }
        }

        private readonly List<Line> _lines;
        private Vec2 _prevFront;
        private bool _hasPrev;
        private Line? _active;
        private double _since;
        private Severity _reported;
        private double _maxDecel;

        public GiveWayRule(List<Line> lines) => _lines = lines;

        public double MinorDecelMps2 = 1.5, SeriousDecelMps2 = 3.0, DangerousDecelMps2 = 6.0;
        public double WindowS = 10;

        public void Reset()
        {
            _hasPrev = false;
            _active = null;
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            Vec2 front = CarBody.Front(f.Ego);
            if (_hasPrev && _active == null)
                foreach (var l in _lines)
                    if (l.Paint.CrossedBy(_prevFront, front))
                    {
                        _active = l;
                        _since = f.Time;
                        _reported = Severity.None;
                        _maxDecel = 0;
                    }
            _prevFront = front;
            _hasPrev = true;
            if (_active == null) return;

            bool contact = false;
            string who = "a car";
            foreach (var u in users)
            {
                if (u.BrakingForPlayer && -u.LastAccelMps2 > _maxDecel) { _maxDecel = -u.LastAccelMps2; who = u.IsCyclist ? "a cyclist" : "a car"; }
                if (CarBody.Clearance(f.Ego, u.Position) < 0.3 && (u.Position - _active.JunctionCentre).Length < 25) contact = true;
            }

            if ((contact || _maxDecel >= DangerousDecelMps2) && _reported < Severity.Dangerous)
            {
                _reported = Severity.Dangerous;
                output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Dangerous, f.Time,
                    $"You pulled out in front of {who} and it nearly hit you. At a give-way line, wait for a safe gap.", Source));
            }
            else if (_maxDecel >= SeriousDecelMps2 && _reported < Severity.Serious)
            {
                _reported = Severity.Serious;
                output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Serious, f.Time,
                    $"You pulled out and {who} had to brake hard. Traffic on the main road goes first.", Source));
            }

            bool over = (CarBody.Centre(f.Ego) - _active.JunctionCentre).Length > 30 || f.Time - _since > WindowS;
            if (over)
            {
                if (_reported == Severity.None && _maxDecel >= MinorDecelMps2)
                    output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Driving, f.Time,
                        $"You pulled out and {who} had to slow down for you. Wait for a bigger gap.", Source));
                _active = null;
            }
        }
    }

    /// <summary>
    /// GB.XNG.ZEBRA (rule 195) and GB.JCT.H2_PEDESTRIANS (rules H2, 170): give way to people waiting at a zebra or
    /// waiting to cross a side road you turn into or out of; you must stop once someone is on a zebra.
    /// </summary>
    public sealed class PedestrianCrossingRule
    {
        public const string ZebraId = "GB.XNG.ZEBRA", H2Id = "GB.JCT.H2_PEDESTRIANS";

        private readonly List<CrossingArea> _areas;
        private readonly HashSet<int> _inside = new HashSet<int>();
        private readonly HashSet<int> _dangerReported = new HashSet<int>();
        private readonly HashSet<int> _praised = new HashSet<int>();

        public PedestrianCrossingRule(List<CrossingArea> areas) => _areas = areas;

        public void Reset()
        {
            _inside.Clear();
            _dangerReported.Clear();
            _praised.Clear();
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<Pedestrian> people, List<RuleEvent> output)
        {
            Vec2 front = CarBody.Front(f.Ego);
            foreach (var area in _areas)
            {
                bool zebra = area.Kind == CrossingKind.Zebra;
                bool on = false, waiting = false;
                double minClear = double.MaxValue;
                foreach (var p in people)
                {
                    if (p.Crossing != area.Id) continue;
                    if (p.State == PedState.Crossing) { on = true; minClear = Math.Min(minClear, CarBody.Clearance(f.Ego, p.Position)); }
                    else if (p.State == PedState.Waiting) waiting = true;
                }

                bool inside = Geo2.PointInPolygon(front, area.Polygon) || Geo2.PointInPolygon(CarBody.Centre(f.Ego), area.Polygon);
                bool entered = inside && !_inside.Contains(area.Id);
                if (inside) _inside.Add(area.Id);
                else if (_inside.Remove(area.Id)) _dangerReported.Remove(area.Id);

                if (entered)
                {
                    _praised.Remove(area.Id);
                    if (on)
                        output.Add(new RuleEvent(zebra ? ZebraId : H2Id, RuleOutcome.Violation, Severity.Serious, f.Time,
                            zebra ? "Someone was on the zebra crossing. You must stop and let them cross."
                                  : "Someone was crossing the side road. Give way to people crossing where you turn.",
                            zebra ? "Highway Code rule 195" : "Highway Code rules H2 and 170"));
                    else if (waiting && f.Ego.SpeedMph > 5)
                        output.Add(new RuleEvent(zebra ? ZebraId : H2Id, RuleOutcome.Violation, Severity.Driving, f.Time,
                            zebra ? "Someone was waiting at the zebra crossing. Slow down and let them cross."
                                  : "Someone was waiting to cross the side road. Give way to them when you turn.",
                            zebra ? "Highway Code rule 195" : "Highway Code rules H2 and 170"));
                }

                if (inside && on && minClear < 1.0 && f.Ego.SpeedMph > 10 && _dangerReported.Add(area.Id))
                    output.Add(new RuleEvent(zebra ? ZebraId : H2Id, RuleOutcome.Violation, Severity.Dangerous, f.Time,
                        "You drove very close to a person who was crossing. Stop and wait until they are across.",
                        zebra ? "Highway Code rule 195" : "Highway Code rules H2 and 170"));

                // Praise for stopping before the crossing while someone is on it or waiting.
                if (!inside && (on || waiting) && f.Ego.SpeedMps < 0.5 && !_praised.Contains(area.Id) && Near(front, area.Polygon, 10))
                {
                    _praised.Add(area.Id);
                    output.Add(new RuleEvent(zebra ? ZebraId : H2Id, RuleOutcome.GoodPractice, Severity.None, f.Time,
                        "Good: you stopped and let people cross.", zebra ? "Highway Code rule 195" : "Highway Code rule H2"));
                }
            }
        }

        private static bool Near(Vec2 p, Vec2[] poly, double within)
        {
            Vec2 c = new Vec2(0, 0);
            foreach (var v in poly) c = c + v;
            c = c * (1.0 / poly.Length);
            return (p - c).Length < within;
        }
    }

    /// <summary>GB.XNG.ZIGZAG_OVERTAKE (rule 191): don't overtake the moving vehicle nearest a crossing, or one stopped to give way, on the zig-zags.</summary>
    public sealed class ZigZagOvertakeRule
    {
        public const string RuleId = "GB.XNG.ZIGZAG_OVERTAKE";
        private readonly List<Vec2[]> _zones;
        private readonly Dictionary<int, double> _along = new Dictionary<int, double>();
        private readonly HashSet<int> _reported = new HashSet<int>();

        public ZigZagOvertakeRule(List<Vec2[]> zones) => _zones = zones;

        public void Reset()
        {
            _along.Clear();
            _reported.Clear();
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            Vec2 me = CarBody.Centre(f.Ego), fwd = Vec2.FromHeading(f.Ego.Heading);
            bool inZone = false;
            foreach (var z in _zones) if (Geo2.PointInPolygon(me, z)) inZone = true;

            foreach (var u in users)
            {
                Vec2 d = u.Position - me;
                double along = d.Dot(fwd), lateral = Math.Abs(d.Dot(fwd.LeftNormal));
                bool sameWay = Math.Cos(u.Heading - f.Ego.Heading) > 0.8;
                if (!sameWay || lateral > 6 || Math.Abs(along) > 30) { _along.Remove(u.Id); continue; }

                if (_along.TryGetValue(u.Id, out double before) && before > 0.5 && along <= 0.5)
                {
                    bool theirZone = false;
                    foreach (var z in _zones) if (Geo2.PointInPolygon(u.Position, z)) theirZone = true;
                    if ((inZone || theirZone) && _reported.Add(u.Id))
                        output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Serious, f.Time,
                            $"You overtook {(u.IsCyclist ? "a cyclist" : "a car")} on the zig-zag lines by a crossing. Never overtake here.",
                            "Highway Code rule 191"));
                }
                _along[u.Id] = along;
            }
        }
    }

    /// <summary>
    /// GB.JCT.BOX (rule 174): don't enter a yellow box unless your exit is clear. You may wait in it when turning
    /// across traffic (right in the UK) and only oncoming traffic, or others waiting to turn, stop you.
    /// </summary>
    public sealed class BoxJunctionRule
    {
        public const string RuleId = "GB.JCT.BOX";
        private readonly List<Vec2[]> _boxes;
        private readonly DrivingSide _side;
        private int _inBox = -1;
        private double _entryHeading, _stoppedSince = double.NaN;
        private bool _reported;

        public BoxJunctionRule(List<Vec2[]> boxes, DrivingSide side)
        {
            _boxes = boxes;
            _side = side;
        }

        public double StoppedForS = 2.0;

        public void Reset()
        {
            _inBox = -1;
            _stoppedSince = double.NaN;
            _reported = false;
        }

        public void OnFrame(in DriverFrame f, List<RuleEvent> output)
        {
            Vec2 me = CarBody.Centre(f.Ego);
            int box = -1;
            for (int i = 0; i < _boxes.Count; i++) if (Geo2.PointInPolygon(me, _boxes[i])) box = i;

            if (box != _inBox)
            {
                _inBox = box;
                _entryHeading = f.Ego.Heading;
                _stoppedSince = double.NaN;
                _reported = false;
            }
            if (box < 0) return;

            if (f.Ego.SpeedMps > 0.5) { _stoppedSince = double.NaN; return; }
            if (double.IsNaN(_stoppedSince)) _stoppedSince = f.Time;
            if (_reported || f.Time - _stoppedSince < StoppedForS) return;

            // Far turn: right in left-hand traffic. Indicator +1 = right.
            int farDir = _side == DrivingSide.Left ? 1 : -1;
            double turned = LaneGraph.Angle(f.Ego.Heading - _entryHeading) * farDir; // > 0 = turned toward the far side
            bool turningFar = f.Indicator == farDir || turned > 0.35;
            if (turningFar) return;

            _reported = true;
            output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Serious, f.Time,
                "You stopped in the yellow box. Only go in when your exit is clear.", "Highway Code rule 174"));
        }
    }

    /// <summary>All town-junction rules, built from a lane graph.</summary>
    public sealed class TownRules
    {
        public TownRules(LaneGraph g, DrivingSide side = DrivingSide.Left)
        {
            var approaches = new List<TrafficLightRule.Approach>();
            var giveWays = new List<GiveWayRule.Line>();
            foreach (var s in g.Segments)
            {
                if (s.Kind != SegmentKind.Lane || s.EndNode < 0) continue;
                Vec2 centre = g.Nodes[s.EndNode].Centre;
                var (pEnd, hEnd) = s.Path.Sample(s.Length);
                if (s.EndControl == LineControl.Signals)
                {
                    var (pStop, hStop) = s.Path.Sample(s.StopLineS);
                    approaches.Add(new TrafficLightRule.Approach(s.SignalGroup, LaneLine.Across(pStop, hStop),
                        s.HasAdvancedStopLine ? LaneLine.Across(pEnd, hEnd) : null, centre));
                }
                else if (s.EndControl == LineControl.GiveWay)
                    giveWays.Add(new GiveWayRule.Line(LaneLine.Across(pEnd, hEnd), centre));
            }
            var boxes = new List<Vec2[]>();
            foreach (var n in g.Nodes) if (n.Box != null) boxes.Add(n.Box);

            Lights = new TrafficLightRule(approaches);
            GiveWay = new GiveWayRule(giveWays);
            Crossings = new PedestrianCrossingRule(g.CrossingAreas);
            ZigZags = new ZigZagOvertakeRule(g.ZigZags);
            Box = new BoxJunctionRule(boxes, side);
        }

        public TrafficLightRule Lights { get; }
        public GiveWayRule GiveWay { get; }
        public PedestrianCrossingRule Crossings { get; }
        public ZigZagOvertakeRule ZigZags { get; }
        public BoxJunctionRule Box { get; }

        public void Reset()
        {
            Lights.Reset();
            GiveWay.Reset();
            Crossings.Reset();
            ZigZags.Reset();
            Box.Reset();
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<LightState> lights, IReadOnlyList<Pedestrian> people,
            IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            Lights.OnFrame(f, lights, users, output);
            GiveWay.OnFrame(f, users, output);
            Crossings.OnFrame(f, people, output);
            ZigZags.OnFrame(f, users, output);
            Box.OnFrame(f, output);
        }
    }
}
