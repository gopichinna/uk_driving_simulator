using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    public sealed class LaneChangeParams
    {
        /// <summary>Signal must be on at least this long before the car crosses into the new lane.</summary>
        public double MinSignalBeforeS = 1.5;
        /// <summary>A mirror check counts if made within this many seconds before crossing.</summary>
        public double MirrorWindowS = 6.0;
        /// <summary>A shoulder check counts if made within this many seconds before crossing.</summary>
        public double ShoulderWindowS = 4.0;
        /// <summary>After crossing, watch traffic in the new lane for this long.</summary>
        public double ConflictWindowS = 3.0;
        public double SeriousBrakeMps2 = 3.0;
        public double DangerousBrakeMps2 = 6.0;
        /// <summary>Bumper gap to a car behind in the new lane that counts as cutting in dangerously.</summary>
        public double DangerousGapM = 4.0;
        /// <summary>Hysteresis so drifting on the lane line doesn't count as several lane changes.</summary>
        public double LaneHysteresisM = 0.35;
        /// <summary>A signal is needed if another vehicle is within this distance (it "would benefit", rule 103).</summary>
        public double SignalNeededWithinM = 80.0;
    }

    /// <summary>
    /// GB.OBS.LANE_CHANGE + GB.SIG.TIMELY (docs/02 catalogue, section A).
    /// Highway Code rule 133: before changing lane, check mirrors and glance sideways, signal, and move only when safe.
    /// Highway Code rule 103: signal in good time.
    /// </summary>
    public sealed class LaneChangeRule
    {
        public const string RuleIdLaneChange = "GB.OBS.LANE_CHANGE";
        public const string RuleIdSignal = "GB.SIG.TIMELY";
        private const string Source133 = "Highway Code rule 133";
        private const string Source103 = "Highway Code rule 103";

        private readonly TrafficSim _traffic;
        private readonly LaneChangeParams _p;
        private readonly DrivingSide _drivingSide;
        private readonly List<ObservationEvent> _observations = new List<ObservationEvent>();

        private int _lane = -1;
        private int _indicatorDir;
        private double _indicatorSince;
        private Pending? _pending;

        private sealed class Pending
        {
            public double CrossTime;
            public int TargetLane;
            public bool CleanSoFar;
            public Severity WorstConflict;
        }

        public LaneChangeRule(TrafficSim traffic, LaneChangeParams? p = null, DrivingSide drivingSide = DrivingSide.Left)
        {
            _traffic = traffic;
            _p = p ?? new LaneChangeParams();
            _drivingSide = drivingSide;
        }

        public void Observe(ObservationEvent e)
        {
            _observations.Add(e);
            if (_observations.Count > 64) _observations.RemoveAt(0);
        }

        public void Reset()
        {
            _lane = -1;
            _indicatorDir = 0;
            _pending = null;
            _observations.Clear();
        }

        public void OnFrame(in DriverFrame f, List<RuleEvent> output)
        {
            TrackIndicator(f);
            CheckPending(f, output);

            int lane = CurrentLane(f.Ego);
            if (_lane < 0) { _lane = lane; return; }
            if (lane < 0 || lane == _lane) return;

            int from = _lane;
            _lane = lane;
            EvaluateLaneChange(f, from, lane, output);
        }

        // ---------------------------------------------------------------

        private void TrackIndicator(in DriverFrame f)
        {
            if (f.Indicator != _indicatorDir)
            {
                _indicatorDir = f.Indicator;
                _indicatorSince = f.Time;
            }
        }

        /// <summary>Lane the car's centre is clearly in (same direction of travel as the car), with hysteresis.</summary>
        private int CurrentLane(in CarState ego)
        {
            int best = -1;
            double bestDist = double.MaxValue;
            var lanes = _traffic.Lanes;
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].Direction * Math.Cos(ego.Heading) < 0) continue; // opposite carriageway
                double d = Math.Abs(ego.X - lanes[i].CenterX);
                if (d < bestDist) { bestDist = d; best = i; }
            }

            if (best < 0) return -1;
            if (_lane >= 0 && best != _lane)
            {
                // Only switch once clearly past the line between the two lanes.
                double half = Math.Abs(lanes[best].CenterX - lanes[_lane].CenterX) / 2;
                if (bestDist > half - _p.LaneHysteresisM) return _lane;
            }
            return best;
        }

        private void EvaluateLaneChange(in DriverFrame f, int from, int to, List<RuleEvent> output)
        {
            var lanes = _traffic.Lanes;
            int travel = lanes[to].Direction;
            int moveDir = Math.Sign((lanes[to].CenterX - lanes[from].CenterX) * travel); // +1 right, -1 left
            CarSide side = Sides.FromDirection(moveDir, _drivingSide);
            double t = f.Time;
            bool clean = true;

            // 1. Mirror (rule 133: "check mirrors")
            bool mirrored = Seen(t - _p.MirrorWindowS, t, side == CarSide.Offside ? ObservationKind.MirrorOffside : ObservationKind.MirrorNearside)
                         || Seen(t - _p.MirrorWindowS, t, ObservationKind.MirrorInterior);
            if (!mirrored)
            {
                clean = false;
                output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.Violation, Severity.Driving, t,
                    $"Check your mirrors before changing lanes. Tap the {(moveDir > 0 ? "right" : "left")} mirror.", Source133));
            }

            // 2. Signal (rules 103 and 133)
            bool othersAround = AnyoneWithin(f.Ego, _p.SignalNeededWithinM, travel);
            if (_indicatorDir == moveDir)
            {
                double onFor = t - _indicatorSince;
                if (onFor < _p.MinSignalBeforeS)
                {
                    clean = false;
                    output.Add(new RuleEvent(RuleIdSignal, RuleOutcome.Violation, Severity.Driving, t,
                        "Signal earlier, so other drivers have time to react.", Source103));
                }
            }
            else if (_indicatorDir == -moveDir)
            {
                clean = false;
                output.Add(new RuleEvent(RuleIdSignal, RuleOutcome.Violation, Severity.Driving, t,
                    "You signalled the wrong way. That can mislead other drivers.", Source103));
            }
            else if (othersAround)
            {
                clean = false;
                output.Add(new RuleEvent(RuleIdSignal, RuleOutcome.Violation, Severity.Driving, t,
                    "Signal before changing lanes when other road users are around.", Source133));
            }

            // 3. Shoulder check (rule 133: "glance sideways")
            bool shoulder = Seen(t - _p.ShoulderWindowS, t, side == CarSide.Offside ? ObservationKind.ShoulderOffside : ObservationKind.ShoulderNearside);
            if (!shoulder)
            {
                clean = false;
                output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.Violation, Severity.Driving, t,
                    $"Look over your {(moveDir > 0 ? "right" : "left")} shoulder before moving across. A car can hide in your blind spot.", Source133));
            }

            // 4. Safety (rule 133: "move when safe") is judged over the next few seconds.
            _pending = new Pending { CrossTime = t, TargetLane = to, CleanSoFar = clean, WorstConflict = Severity.None };
            CheckPending(f, output);
        }

        private void CheckPending(in DriverFrame f, List<RuleEvent> output)
        {
            if (_pending == null) return;
            var p = _pending;
            int travel = _traffic.Lanes[p.TargetLane].Direction;

            foreach (var c in _traffic.Cars)
            {
                if (Math.Abs(c.X - _traffic.Lanes[p.TargetLane].CenterX) > 1.8) continue;
                double behind = (f.Ego.Z - c.Z) * travel; // > 0: the other car is behind us
                if (behind <= 0 || behind > 60) continue;

                Severity s = Severity.None;
                if (f.Time - p.CrossTime < 1.0 && behind - 4.3 < _p.DangerousGapM) s = Severity.Dangerous;
                else if (c.LastAccelMps2 <= -_p.DangerousBrakeMps2) s = Severity.Dangerous;
                else if (c.LastAccelMps2 <= -_p.SeriousBrakeMps2) s = Severity.Serious;
                if (s > p.WorstConflict) p.WorstConflict = s;
            }

            if (p.WorstConflict >= Severity.Serious)
            {
                // Report a conflict as soon as it happens, once.
                output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.Violation, p.WorstConflict, f.Time,
                    p.WorstConflict == Severity.Dangerous
                        ? "Dangerous lane change: you cut in right in front of another car."
                        : "Unsafe lane change: a car behind had to brake for you.",
                    Source133));
                _pending = null;
                return;
            }

            if (f.Time - p.CrossTime >= _p.ConflictWindowS)
            {
                if (p.CleanSoFar)
                    output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.GoodPractice, Severity.None, f.Time,
                        "Good lane change: mirror, signal, shoulder check.", Source133));
                _pending = null;
            }
        }

        private bool Seen(double from, double to, ObservationKind kind)
        {
            foreach (var o in _observations)
                if (o.Kind == kind && o.Time >= from && o.Time <= to) return true;
            return false;
        }

        private bool AnyoneWithin(in CarState ego, double distance, int travel)
        {
            foreach (var c in _traffic.Cars)
            {
                if (_traffic.Lanes[c.Lane].Direction != travel) continue;
                if (Math.Abs(c.Z - ego.Z) <= distance) return true;
            }
            return false;
        }
    }
}
