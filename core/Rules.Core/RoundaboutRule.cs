using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// Roundabout rules for the player (docs/02 catalogue, section F), for single- and two-lane roundabouts:
    ///  GB.RBT.GIVE_WAY_RIGHT  – give priority to traffic approaching from the right (Highway Code rule 185)
    ///  GB.RBT.SIGNAL_APPROACH – left for a left exit, right for exits past 12 o'clock, usually none for ahead (rule 186)
    ///  GB.RBT.SIGNAL_EXIT     – signal left after passing the exit before the one you want (rule 186)
    ///  GB.RBT.APPROACH_LANE   – use the lane the arrows show for your exit (rule 186)
    ///  GB.RBT.STAY_IN_LANE    – move to the outer lane before you exit; don't change lanes into someone's path
    ///  GB.RBT.WRONG_WAY       – circulate clockwise in the UK (rules 185–186)
    /// </summary>
    public sealed class RoundaboutRule
    {
        public const string RuleGiveWay = "GB.RBT.GIVE_WAY_RIGHT";
        public const string RuleSignalApproach = "GB.RBT.SIGNAL_APPROACH";
        public const string RuleSignalExit = "GB.RBT.SIGNAL_EXIT";
        public const string RuleApproachLane = "GB.RBT.APPROACH_LANE";
        public const string RuleStayInLane = "GB.RBT.STAY_IN_LANE";
        public const string RuleWrongWay = "GB.RBT.WRONG_WAY";
        private const string Src185 = "Highway Code rule 185";
        private const string Src186 = "Highway Code rule 186";

        private enum Phase { Away, Approaching, OnRing, Exiting }

        private readonly IRoundaboutView _v;
        private readonly List<RingVehicle> _ring = new List<RingVehicle>();
        private readonly double[] _passTime;
        private readonly int[] _passLane;
        private readonly List<double> _leftOnRing = new List<double>();

        private Phase _phase = Phase.Away;
        private int _approachArm = -1, _approachLane = -1, _entryArm = -1, _entryLane = -1;
        private double _approachLeftS, _approachRightS;
        private double _entryTime;
        private double _prevBearing = double.NaN;
        private double _wrongWaySince = double.NaN;
        private bool _wrongWayReported, _giveWayReported, _visitFault;
        private int _ringLane = -1;
        private double _innerLaneLastSeen = double.NegativeInfinity;
        private double _laneChangeTime = double.NegativeInfinity;
        private int _laneChangeTo = -1;
        private bool _laneChangeReported;

        public RoundaboutRule(IRoundaboutView view)
        {
            _v = view;
            _passTime = new double[view.ArmCount];
            _passLane = new int[view.ArmCount];
        }

        public RoundaboutRule(RoundaboutTraffic traffic) : this(new SingleLaneRoundaboutView(traffic)) { }

        public void Reset()
        {
            _phase = Phase.Away;
            _approachArm = _entryArm = -1;
            _prevBearing = double.NaN;
        }

        public void OnFrame(in DriverFrame f, double dt, List<RuleEvent> output)
        {
            var pos = new Vec2(f.Ego.X, f.Ego.Z);
            bool onRing = _v.OnRing(pos);
            int approach = _v.ApproachArm(pos, f.Ego.Heading, out int lane);
            int exiting = onRing ? -1 : _v.ExitArm(pos);
            _v.RingVehicles(_ring);

            switch (_phase)
            {
                case Phase.Away:
                case Phase.Exiting:
                    if (approach >= 0) StartApproach(approach, lane);
                    else if (onRing) { StartVisit(f, -1, -1); EnterRing(f, output); }
                    else if (_phase == Phase.Exiting && f.Time - _entryTime > 15) _phase = Phase.Away;
                    break;

                case Phase.Approaching:
                    if (onRing)
                    {
                        StartVisit(f, _approachArm, _approachLane);
                        EnterRing(f, output);
                    }
                    else if (approach < 0) _phase = Phase.Away;
                    else
                    {
                        _approachLane = lane; // the lane you're in when you reach the line is the one that counts
                        if (f.Indicator < 0) _approachLeftS += dt;
                        if (f.Indicator > 0) _approachRightS += dt;
                    }
                    break;

                case Phase.OnRing:
                    if (onRing) OnRingFrame(f, output);
                    else if (exiting >= 0) Leave(f, exiting, output);
                    else if (approach < 0) _phase = Phase.Away;
                    break;
            }

            if (_phase == Phase.OnRing || (_phase == Phase.Exiting && f.Time - _entryTime < 4))
                CheckGiveWay(f, output);
        }

        // ---------------------------------------------------------------

        private void StartApproach(int arm, int lane)
        {
            _phase = Phase.Approaching;
            _approachArm = arm;
            _approachLane = lane;
            _approachLeftS = _approachRightS = 0;
        }

        private void StartVisit(in DriverFrame f, int entryArm, int entryLane)
        {
            _entryArm = entryArm;
            _entryLane = entryLane;
            _entryTime = f.Time;
            _prevBearing = double.NaN;
            for (int i = 0; i < _passTime.Length; i++) { _passTime[i] = double.NaN; _passLane[i] = -1; }
            _leftOnRing.Clear();
            _wrongWaySince = double.NaN;
            _wrongWayReported = _giveWayReported = _visitFault = _laneChangeReported = false;
            _ringLane = -1;
            _innerLaneLastSeen = double.NegativeInfinity;
            _laneChangeTime = double.NegativeInfinity;
        }

        /// <summary>Distance from a ring vehicle's front, going clockwise, to our position (how far behind us it is).</summary>
        private static double Upstream(RingVehicle v, double ourBearing) =>
            TwoLaneRoundaboutGeometry.Cw(v.Bearing, ourBearing) * v.Radius;

        private void EnterRing(in DriverFrame f, List<RuleEvent> output)
        {
            _phase = Phase.OnRing;
            double b = _v.BearingOf(new Vec2(f.Ego.X, f.Ego.Z));
            foreach (var v in _ring)
                if (Upstream(v, b) < 6 && v.SpeedMps > 2)
                {
                    ReportGiveWay(f, Severity.Dangerous, output);
                    return;
                }
        }

        private void CheckGiveWay(in DriverFrame f, List<RuleEvent> output)
        {
            if (_giveWayReported || f.Time - _entryTime > 3.0) return;
            double b = _v.BearingOf(new Vec2(f.Ego.X, f.Ego.Z));
            foreach (var v in _ring)
            {
                if (Upstream(v, b) > 35) continue;
                if (v.LastAccelMps2 <= -6) { ReportGiveWay(f, Severity.Dangerous, output); return; }
                if (v.LastAccelMps2 <= -3) { ReportGiveWay(f, Severity.Serious, output); return; }
            }
        }

        private void ReportGiveWay(in DriverFrame f, Severity s, List<RuleEvent> output)
        {
            _giveWayReported = true;
            _visitFault = true;
            output.Add(new RuleEvent(RuleGiveWay, RuleOutcome.Violation, s, f.Time,
                s == Severity.Dangerous
                    ? "Dangerous: you pulled out right in front of a car already on the roundabout. Give way to traffic from your right."
                    : "You didn't give way to traffic from your right. A car on the roundabout had to brake for you.",
                Src185));
        }

        private void OnRingFrame(in DriverFrame f, List<RuleEvent> output)
        {
            var pos = new Vec2(f.Ego.X, f.Ego.Z);
            double b = _v.BearingOf(pos);
            double along = f.Ego.SpeedMps * Math.Cos(f.Ego.Heading - (b + Math.PI / 2));

            // Wrong way round (anticlockwise)
            if (along < -1.0)
            {
                if (double.IsNaN(_wrongWaySince)) _wrongWaySince = f.Time;
                if (!_wrongWayReported && f.Time - _wrongWaySince > 1.0)
                {
                    _wrongWayReported = true;
                    _visitFault = true;
                    output.Add(new RuleEvent(RuleWrongWay, RuleOutcome.Violation, Severity.Dangerous, f.Time,
                        "Wrong way! In the UK you go round a roundabout clockwise: turn left onto it.",
                        "Highway Code rules 185–186"));
                }
            }
            else _wrongWaySince = double.NaN;

            // Exits passed (clockwise)
            if (!double.IsNaN(_prevBearing) && along > 0)
            {
                double moved = TwoLaneRoundaboutGeometry.Cw(_prevBearing, b);
                if (moved < 0.5)
                    for (int k = 0; k < _v.ArmCount; k++)
                        if (double.IsNaN(_passTime[k]) && TwoLaneRoundaboutGeometry.Cw(_prevBearing, _v.DivergeBearing(k)) <= moved)
                        {
                            _passTime[k] = f.Time;
                            _passLane[k] = _v.RingLaneAt(_v.RadiusOf(pos)); // which lane we were in at this exit
                        }
            }
            _prevBearing = b;

            if (f.Indicator < 0) _leftOnRing.Add(f.Time);

            // Lane discipline on multi-lane roundabouts
            int ringLane = _v.RingLaneAt(_v.RadiusOf(pos));
            if (ringLane == 1) _innerLaneLastSeen = f.Time;
            if (_ringLane >= 0 && ringLane != _ringLane)
            {
                _laneChangeTime = f.Time;
                _laneChangeTo = ringLane;
            }
            _ringLane = ringLane;

            if (!_laneChangeReported && f.Time - _laneChangeTime < 2.5)
            {
                foreach (var v in _ring)
                {
                    if (_v.RingLaneAt(v.Radius) != _laneChangeTo || Upstream(v, b) > 25) continue;
                    if (v.LastAccelMps2 <= -3)
                    {
                        _laneChangeReported = true;
                        _visitFault = true;
                        output.Add(new RuleEvent(RuleStayInLane, RuleOutcome.Violation,
                            v.LastAccelMps2 <= -6 ? Severity.Dangerous : Severity.Serious, f.Time,
                            "You changed lanes on the roundabout into the path of another car. Check your mirror before moving across.",
                            Src186));
                        break;
                    }
                }
            }
        }

        private void Leave(in DriverFrame f, int exitArm, List<RuleEvent> output)
        {
            _phase = Phase.Exiting;
            if (_entryArm < 0 || _wrongWayReported) return;
            double time = f.Time; // local functions can't capture 'in' parameters

            int n = _v.ExitNumber(_entryArm, exitArm);
            string exitName = n switch { 1 => "first exit (left)", 2 => "second exit (straight on)", 3 => "third exit (right)", _ => "full circle" };

            // Lane choice (multi-lane): the arrows tell you which lane to use
            if (_v.ApproachLanes > 1 && _entryLane >= 0)
            {
                int wanted = _v.RecommendedApproachLane(n);
                if (_entryLane != wanted)
                    Fault(RuleApproachLane, wanted == 0
                        ? $"For the {exitName}, approach in the left lane. Follow the lane arrows."
                        : $"For the {exitName}, approach in the right lane. Follow the lane arrows.", Src186);
            }

            // Leaving straight from the inner lane cuts across the outer lane
            if (_v.ApproachLanes > 1 && (_passLane[exitArm] == 1 || time - _innerLaneLastSeen < 1.5))
                Fault(RuleStayInLane, "Move into the outer (left) lane before you leave the roundabout. Leaving from the inner lane cuts across other traffic.", Src186);

            // Approach signal
            const double minSignal = 1.0;
            if (n == 1 && _approachLeftS < minSignal)
                Fault(RuleSignalApproach, $"Signal left as you approach when taking the {exitName}.", Src186);
            else if (n >= 3 && _approachRightS < minSignal)
                Fault(RuleSignalApproach, $"Signal right as you approach when taking the {exitName}.", Src186);
            else if (n == 2 && _approachRightS >= minSignal)
                Fault(RuleSignalApproach, "Don't signal right when going straight on. Drivers waiting may think you're going round.", Src186);

            // Exit signal: left after passing the exit before ours (not needed for the first exit)
            if (n >= 2)
            {
                int previous = ((exitArm - 1) % _v.ArmCount + _v.ArmCount) % _v.ArmCount;
                double passed = _passTime[previous];
                bool signalledLeft = false;
                foreach (double t in _leftOnRing)
                    if (!double.IsNaN(passed) && t >= passed - 0.3) { signalledLeft = true; break; }
                if (!signalledLeft)
                    Fault(RuleSignalExit, "Signal left after you pass the exit before the one you want, so waiting drivers know you're leaving.", Src186);
            }

            if (!_visitFault)
                output.Add(new RuleEvent(RuleSignalExit, RuleOutcome.GoodPractice, Severity.None, time,
                    _v.ApproachLanes > 1
                        ? $"Good roundabout: right lane, gave way to the right and signalled correctly for the {exitName}."
                        : $"Good roundabout: you gave way to the right and signalled correctly for the {exitName}.",
                    Src186));

            void Fault(string id, string message, string src)
            {
                _visitFault = true;
                output.Add(new RuleEvent(id, RuleOutcome.Violation, Severity.Driving, time, message, src));
            }
        }
    }
}
