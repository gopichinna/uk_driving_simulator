using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// GB.POS.KEEP_LEFT (Highway Code rule 160; also covers ending a turn on the wrong side): drive on your side of
    /// the road (the left in the UK) unless you are passing a vehicle or obstruction. Serious after a second on the
    /// wrong side; dangerous if traffic is coming towards you.
    /// GB.SIG.TIMELY (rule 103): when you move out (for example to pass a parked car), signal first if anyone is
    /// around to see it.
    /// Works on any two-way lane graph; junction areas are skipped because turning paths cross the other lane.
    /// </summary>
    public sealed class KeepLeftRule
    {
        public const string KeepLeftId = "GB.POS.KEEP_LEFT";
        public const string SignalId = "GB.SIG.TIMELY";

        private readonly LaneGraph _g;
        private readonly DrivingSide _side;
        private bool _wrongSide;
        private double _since = double.NaN;
        private Severity _reported;
        private double _lastSignalOut = double.NegativeInfinity;
        private double _lastNearJunction = double.NegativeInfinity;

        private readonly bool[] _isJunction;
        private readonly List<Segment> _near = new List<Segment>();

        public KeepLeftRule(LaneGraph graph, DrivingSide side = DrivingSide.Left)
        {
            _g = graph;
            _side = side;
            _isJunction = new bool[graph.Nodes.Count];
            var connectors = new int[graph.Nodes.Count];
            foreach (var s in graph.Segments) if (s.Kind == SegmentKind.Connector && s.Node >= 0) connectors[s.Node]++;
            // A bend (one way in and out each side) is not a junction; anything else is.
            for (int i = 0; i < graph.Nodes.Count; i++) _isJunction[i] = graph.Nodes[i].Signalised || connectors[i] > 2;
        }

        /// <summary>Lanes whose source data is less certain than this are not judged.</summary>
        public double MinConfidence = 0.7;

        /// <summary>How far past the centre line (m) counts as being on the wrong side.</summary>
        public double IntoLaneM = 0.5;
        public double SeriousAfterS = 1.0;
        public double JunctionRadiusM = 13.0;
        public double OncomingWithinM = 60.0;

        public void Reset()
        {
            _wrongSide = false;
            _since = double.NaN;
            _reported = Severity.None;
            _lastSignalOut = double.NegativeInfinity;
            _lastNearJunction = double.NegativeInfinity;
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            int offsideDir = _side == DrivingSide.Left ? 1 : -1;
            if (f.Indicator == offsideDir) _lastSignalOut = f.Time;

            Vec2 me = new Vec2(f.Ego.X, f.Ego.Z);
            bool nearJunction = false;
            foreach (var n in _g.Nodes)
                if (_isJunction[n.Id] && Math.Abs(me.X - n.Centre.X) < JunctionRadiusM && (me - n.Centre).Length < JunctionRadiusM) nearJunction = true;
            if (nearJunction) _lastNearJunction = f.Time;

            bool wrong = !nearJunction && f.Ego.SpeedMps > 0.5 && OnWrongSide(me, f.Ego.Heading);

            if (wrong && !_wrongSide)
            {
                _since = f.Time;
                _reported = Severity.None;
                // Moving out: did they signal (within the last 3 s) if anyone was around?
                if (f.Time - _lastSignalOut > 3.0 && f.Time - _lastNearJunction > 2.0 && AnyoneWithin(me, users, 60))
                    output.Add(new RuleEvent(SignalId, RuleOutcome.Violation, Severity.Driving, f.Time,
                        "You moved out without signalling. Signal first so others know what you are doing.",
                        "Highway Code rule 103"));
            }
            _wrongSide = wrong;
            if (!wrong) return;

            if (Passing(me, f.Ego.Heading, users)) { _since = f.Time; return; } // overtaking or passing an obstruction is allowed

            bool oncoming = OncomingAhead(me, f.Ego.Heading, users);
            bool afterTurn = f.Time - _lastNearJunction < 4.0;
            if (oncoming && f.Time - _since > 0.3 && _reported < Severity.Dangerous)
            {
                _reported = Severity.Dangerous;
                output.Add(new RuleEvent(KeepLeftId, RuleOutcome.Violation, Severity.Dangerous, f.Time,
                    "You were on the wrong side of the road with traffic coming towards you. Keep left.",
                    "Highway Code rule 160"));
            }
            else if (f.Time - _since >= SeriousAfterS && _reported < Severity.Serious)
            {
                _reported = Severity.Serious;
                output.Add(new RuleEvent(KeepLeftId, RuleOutcome.Violation, Severity.Serious, f.Time,
                    afterTurn ? "After turning, you ended up on the wrong side of the road. In the UK, keep left."
                              : "You drove on the wrong side of the road. In the UK, keep left.",
                    "Highway Code rule 160"));
            }
        }

        /// <summary>Is the point at least <see cref="IntoLaneM"/> past the centre line into a lane going the other way?</summary>
        public bool OnWrongSide(Vec2 p, double heading)
        {
            double ownBest = double.MaxValue, oppBest = double.MaxValue;
            _g.Near(p, 6, _near);
            foreach (var s in _near)
            {
                if (s.Kind != SegmentKind.Lane || s.Confidence < MinConfidence) continue;
                var (ss, _, d) = s.Path.Project(p);
                if (d > TownLaneHalf * 2 + 1 || ss <= 0.01 || ss >= s.Length - 0.01) continue;
                double c = Math.Cos(heading - s.Path.Sample(ss).heading);
                if (c > 0.5) ownBest = Math.Min(ownBest, d);
                else if (c < -0.5) oppBest = Math.Min(oppBest, d);
            }
            if (oppBest == double.MaxValue) return false;
            // Over the centre line by IntoLaneM: closer to the opposing lane centre than (half width - IntoLaneM).
            return oppBest < TownLaneHalf - IntoLaneM && oppBest < ownBest;
        }

        private const double TownLaneHalf = 1.75;

        private static bool Passing(Vec2 me, double heading, IReadOnlyList<RoadUser> users)
        {
            Vec2 f = Vec2.FromHeading(heading);
            foreach (var u in users)
            {
                if (Math.Cos(u.Heading - heading) < 0.8 && u.SpeedMps > 0.5) continue; // moving the other way
                Vec2 d = u.Position - me;
                double along = d.Dot(f), side = d.Dot(f.LeftNormal);
                // Someone (or something stopped) on our nearside, from just behind to a little ahead.
                if (along > -7 && along < 25 && Math.Abs(side) > 1.5 && Math.Abs(side) < 5.5) return true;
            }
            return false;
        }

        private bool OncomingAhead(Vec2 me, double heading, IReadOnlyList<RoadUser> users)
        {
            Vec2 f = Vec2.FromHeading(heading);
            foreach (var u in users)
            {
                if (Math.Cos(u.Heading - heading) > -0.7 || u.SpeedMps < 0.5) continue;
                Vec2 d = u.Position - me;
                double along = d.Dot(f), side = Math.Abs(d.Dot(f.LeftNormal));
                if (along > 0 && along < OncomingWithinM && side < 2.0) return true;
            }
            return false;
        }

        private static bool AnyoneWithin(Vec2 me, IReadOnlyList<RoadUser> users, double r)
        {
            foreach (var u in users) if ((u.Position - me).Length < r) return true;
            return false;
        }
    }
}
