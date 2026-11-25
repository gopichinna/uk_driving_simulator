using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// Roundabout rules for the player (docs/02 catalogue, section F):
    ///  GB.RBT.GIVE_WAY_RIGHT  – give priority to traffic approaching from the right (Highway Code rule 185)
    ///  GB.RBT.SIGNAL_APPROACH – left for a left exit, right for exits past 12 o'clock, usually none for ahead (rule 186)
    ///  GB.RBT.SIGNAL_EXIT     – signal left after passing the exit before the one you want (rule 186)
    ///  GB.RBT.WRONG_WAY       – circulate clockwise in the UK (rules 185–186)
    /// Single-lane roundabout: lane choice (GB.RBT.APPROACH_LANE) comes with the two-lane roundabout.
    /// </summary>
    public sealed class RoundaboutRule
    {
        public const string RuleGiveWay = "GB.RBT.GIVE_WAY_RIGHT";
        public const string RuleSignalApproach = "GB.RBT.SIGNAL_APPROACH";
        public const string RuleSignalExit = "GB.RBT.SIGNAL_EXIT";
        public const string RuleWrongWay = "GB.RBT.WRONG_WAY";
        private const string Src185 = "Highway Code rule 185";
        private const string Src186 = "Highway Code rule 186";

        private enum Phase { Away, Approaching, OnRing, Exiting }

        private readonly RoundaboutTraffic _traffic;
        private readonly RoundaboutGeometry _g;

        private Phase _phase = Phase.Away;
        private int _approachArm = -1;
        private int _entryArm = -1;
        private double _approachLeftS, _approachRightS;
        private double _entryTime;
        private double _prevRingS = double.NaN;
        private readonly double[] _passTime;
        private readonly List<double> _leftOnRing = new List<double>();
        private double _wrongWaySince = double.NaN;
        private bool _wrongWayReported;
        private bool _giveWayReported;
        private bool _visitFault;

        public RoundaboutRule(RoundaboutTraffic traffic)
        {
            _traffic = traffic;
            _g = traffic.Geometry;
            _passTime = new double[_g.ArmCount];
        }

        public void Reset()
        {
            _phase = Phase.Away;
            _approachArm = _entryArm = -1;
            _prevRingS = double.NaN;
        }

        public void OnFrame(in DriverFrame f, double dt, List<RuleEvent> output)
        {
            var pos = new Vec2(f.Ego.X, f.Ego.Z);
            bool onRing = _g.OnRing(pos);
            int approach = ApproachArm(pos, f.Ego);
            int exiting = onRing ? -1 : ExitArm(pos);

            switch (_phase)
            {
                case Phase.Away:
                case Phase.Exiting:
                    if (approach >= 0)
                    {
                        StartApproach(approach);
                    }
                    else if (onRing)
                    {
                        StartVisit(f, -1); // drove onto the ring without using an entry (unusual)
                        EnterRing(f, output);
                    }
                    else if (_phase == Phase.Exiting && (pos - _g.Centre).Length > _g.RingRadius + 45)
                    {
                        _phase = Phase.Away;
                    }
                    break;

                case Phase.Approaching:
                    if (onRing)
                    {
                        StartVisit(f, _approachArm);
                        EnterRing(f, output);
                    }
                    else if (approach < 0)
                    {
                        _phase = Phase.Away; // turned off / reversed out
                    }
                    else
                    {
                        if (f.Indicator < 0) _approachLeftS += dt;
                        if (f.Indicator > 0) _approachRightS += dt;
                    }
                    break;

                case Phase.OnRing:
                    if (onRing) OnRingFrame(f, output);
                    else if (exiting >= 0) Leave(f, exiting, output);
                    else if (approach < 0) _phase = Phase.Away; // left the road
                    break;
            }

            if (_phase == Phase.OnRing || (_phase == Phase.Exiting && f.Time - _entryTime < 4))
                CheckGiveWay(f, output);
        }

        // ---------------------------------------------------------------

        private void StartApproach(int arm)
        {
            _phase = Phase.Approaching;
            _approachArm = arm;
            _approachLeftS = _approachRightS = 0;
        }

        private void StartVisit(in DriverFrame f, int entryArm)
        {
            _entryArm = entryArm;
            _entryTime = f.Time;
            _prevRingS = double.NaN;
            for (int i = 0; i < _passTime.Length; i++) _passTime[i] = double.NaN;
            _leftOnRing.Clear();
            _wrongWaySince = double.NaN;
            _wrongWayReported = false;
            _giveWayReported = false;
            _visitFault = false;
        }

        private void EnterRing(in DriverFrame f, List<RuleEvent> output)
        {
            _phase = Phase.OnRing;
            // Immediate check: did we pull out right in front of someone?
            var (playerS, _, _) = _g.Ring.Project(new Vec2(f.Ego.X, f.Ego.Z));
            foreach (var c in _traffic.Cars)
            {
                if (c.Segment != RoundaboutSegment.Ring) continue;
                double upstream = _g.RingForward(c.S, playerS); // how far behind us it is
                if (upstream < 6 && c.SpeedMps > 2)
                {
                    ReportGiveWay(f, Severity.Dangerous, output);
                    return;
                }
            }
        }

        private void CheckGiveWay(in DriverFrame f, List<RuleEvent> output)
        {
            if (_giveWayReported || f.Time - _entryTime > 3.0) return;
            var (playerS, _, _) = _g.Ring.Project(new Vec2(f.Ego.X, f.Ego.Z));
            foreach (var c in _traffic.Cars)
            {
                if (c.Segment != RoundaboutSegment.Ring) continue;
                double upstream = _g.RingForward(c.S, playerS);
                if (upstream > 35) continue;
                if (c.LastAccelMps2 <= -6) { ReportGiveWay(f, Severity.Dangerous, output); return; }
                if (c.LastAccelMps2 <= -3) { ReportGiveWay(f, Severity.Serious, output); return; }
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
            var (s, _, _) = _g.Ring.Project(new Vec2(f.Ego.X, f.Ego.Z));
            var (_, ringHeading) = _g.Ring.Sample(s);
            double along = f.Ego.SpeedMps * Math.Cos(f.Ego.Heading - ringHeading);

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
            else
            {
                _wrongWaySince = double.NaN;
            }

            // Which exits have we driven past (clockwise)?
            if (!double.IsNaN(_prevRingS) && along > 0)
            {
                double moved = _g.RingForward(_prevRingS, s);
                if (moved < 5)
                    for (int k = 0; k < _g.ArmCount; k++)
                    {
                        double toDiverge = _g.RingForward(_prevRingS, _g.ExitDivergeS[k]);
                        if (toDiverge <= moved && double.IsNaN(_passTime[k])) _passTime[k] = f.Time;
                    }
            }
            _prevRingS = s;

            if (f.Indicator < 0) _leftOnRing.Add(f.Time);
        }

        private void Leave(in DriverFrame f, int exitArm, List<RuleEvent> output)
        {
            _phase = Phase.Exiting;
            if (_entryArm < 0 || _wrongWayReported) return;
            double time = f.Time; // local functions can't capture 'in' parameters

            int n = _g.ExitNumber(_entryArm, exitArm);
            string exitName = n switch { 1 => "first exit (left)", 2 => "second exit (straight on)", 3 => "third exit (right)", _ => "full circle" };

            // Approach signal (rule 186)
            const double minSignal = 1.0;
            if (n == 1 && _approachLeftS < minSignal)
                Fault(RuleSignalApproach, $"Signal left as you approach when taking the {exitName}.", Src186);
            else if (n >= 3 && _approachRightS < minSignal)
                Fault(RuleSignalApproach, $"Signal right as you approach when taking the {exitName}.", Src186);
            else if (n == 2 && _approachRightS >= minSignal)
                Fault(RuleSignalApproach, "Don't signal right when going straight on. Drivers waiting may think you're going round.", Src186);

            // Exit signal: left after passing the exit before ours (rule 186). Not needed for the first exit.
            if (n >= 2)
            {
                int previous = ((exitArm - 1) % _g.ArmCount + _g.ArmCount) % _g.ArmCount;
                double passed = _passTime[previous];
                bool signalledLeft = false;
                foreach (double t in _leftOnRing)
                    if (!double.IsNaN(passed) && t >= passed - 0.3) { signalledLeft = true; break; }
                if (!signalledLeft)
                    Fault(RuleSignalExit, "Signal left after you pass the exit before the one you want, so waiting drivers know you're leaving.", Src186);
            }

            if (!_visitFault)
                output.Add(new RuleEvent(RuleSignalExit, RuleOutcome.GoodPractice, Severity.None, time,
                    $"Good roundabout: you gave way to the right and signalled correctly for the {exitName}.", Src186));

            void Fault(string id, string message, string src)
            {
                _visitFault = true;
                output.Add(new RuleEvent(id, RuleOutcome.Violation, Severity.Driving, time, message, src));
            }
        }

        // ---------- zones ----------

        private int ApproachArm(Vec2 pos, in CarState ego)
        {
            for (int k = 0; k < _g.ArmCount; k++)
            {
                var path = _g.Entry[k];
                var (s, _, dist) = path.Project(pos);
                if (dist > 2.8 || s < _g.GiveWayS[k] - 70 || s > path.Length - 1) continue;
                var (_, h) = path.Sample(s);
                if (Math.Cos(ego.Heading - h) > 0.3) return k;
            }
            return -1;
        }

        private int ExitArm(Vec2 pos)
        {
            for (int k = 0; k < _g.ArmCount; k++)
            {
                var (s, _, dist) = _g.Exit[k].Project(pos);
                if (dist < 3.0 && s > 1.5) return k;
            }
            return -1;
        }
    }
}
