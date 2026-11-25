using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    public sealed class LaneChangeParams
    {
        /// <summary>Signal must be on at least this long before the car crosses into the new lane.</summary>
        public double MinSignalBeforeS = 1.5;
        /// <summary>After crossing, watch traffic in the new lane for this long.</summary>
        public double ConflictWindowS = 3.0;
        public double SeriousBrakeMps2 = 3.0;
        public double DangerousBrakeMps2 = 6.0;
        /// <summary>Bumper gap to a car behind in the new lane that counts as cutting in dangerously.</summary>
        public double DangerousGapM = 4.0;
        /// <summary>Blind-spot zone: from this far behind our rear bumper up to alongside.</summary>
        public double BlindSpotBehindM = 8.0;
        /// <summary>Time gap to the car ahead in the new lane below which we moved in too close.</summary>
        public double TooCloseAheadS = 1.0;
        /// <summary>Hysteresis so drifting on the lane line doesn't count as several lane changes.</summary>
        public double LaneHysteresisM = 0.35;
        /// <summary>A signal is needed if another vehicle is within this distance (it "would benefit", rule 103).</summary>
        public double SignalNeededWithinM = 80.0;
        public double CarLengthM = 4.3;
    }

    /// <summary>
    /// GB.OBS.LANE_CHANGE + GB.SIG.TIMELY (docs/02 catalogue, section A).
    /// Highway Code rule 133: before changing lane, check mirrors and glance sideways, signal, and move only when safe.
    /// Highway Code rule 103: signal in good time.
    ///
    /// Design decision (prototype play-test, 2026-11-25): players are NOT asked to tap mirrors or hold a
    /// shoulder-check button; that felt like a chore. Observation is judged by its outcome instead: if a car
    /// was in the blind spot, alongside, or had to brake, the feedback explains that checking would have shown it.
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

        /// <summary>Kept for a possible "observation" assist mode; observations are not required or judged.</summary>
        public void Observe(ObservationEvent e) { }

        public void Reset()
        {
            _lane = -1;
            _indicatorDir = 0;
            _pending = null;
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
            string sideWord = moveDir > 0 ? "right" : "left";
            double t = f.Time;
            bool clean = true;

            // 1. Signal (rules 103 and 133)
            bool othersAround = AnyoneWithin(f.Ego, _p.SignalNeededWithinM, travel);
            if (_indicatorDir == moveDir)
            {
                if (t - _indicatorSince < _p.MinSignalBeforeS)
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

            // 2. What was actually in the new lane when we moved across? (rule 133: "move when safe")
            Severity worst = Severity.None;
            string? message = null;
            double egoSpeedAlong = f.Ego.SpeedMps * Math.Cos(f.Ego.Heading) * travel;
            foreach (var c in _traffic.Cars)
            {
                if (!InLane(c, to)) continue;
                double ahead = (c.Z - f.Ego.Z) * travel; // centre-to-centre, > 0: other car is ahead of us
                double len = _p.CarLengthM;

                if (Math.Abs(ahead) < len)
                {
                    Raise(Severity.Dangerous, $"Dangerous lane change: there was a car right alongside you on your {sideWord}. "
                        + $"Check your {sideWord} mirror and look over your {sideWord} shoulder before moving across.");
                }
                else if (ahead < 0 && -ahead - len <= _p.BlindSpotBehindM)
                {
                    Raise(Severity.Serious, $"A car was in your {sideWord} blind spot. "
                        + $"A mirror check and a look over your {sideWord} shoulder would have shown it.");
                }
                else if (ahead > 0)
                {
                    double gap = ahead - len;
                    double headway = egoSpeedAlong > 1 ? gap / egoSpeedAlong : double.PositiveInfinity;
                    if (headway < _p.TooCloseAheadS)
                        Raise(Severity.Driving, "You moved in too close behind the car in front. Leave a bigger gap.");
                }
            }

            if (message != null)
            {
                clean = false;
                output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.Violation, worst, t, message, Source133));
            }

            // 3. Did anyone behind have to brake for us? Judged over the next few seconds.
            _pending = new Pending { CrossTime = t, TargetLane = to, CleanSoFar = clean, WorstConflict = worst };
            CheckPending(f, output);

            void Raise(Severity s, string m)
            {
                if (s > worst) { worst = s; message = m; }
            }
        }

        private void CheckPending(in DriverFrame f, List<RuleEvent> output)
        {
            if (_pending == null) return;
            var p = _pending;
            int travel = _traffic.Lanes[p.TargetLane].Direction;

            if (f.Time > p.CrossTime) // the crossing frame itself was judged above
            {
                foreach (var c in _traffic.Cars)
                {
                    if (!InLane(c, p.TargetLane)) continue;
                    double behind = (f.Ego.Z - c.Z) * travel;
                    if (behind <= 0 || behind > 60) continue;

                    Severity s = c.LastAccelMps2 <= -_p.DangerousBrakeMps2 ? Severity.Dangerous
                               : c.LastAccelMps2 <= -_p.SeriousBrakeMps2 ? Severity.Serious
                               : Severity.None;
                    if (s > p.WorstConflict)
                    {
                        p.WorstConflict = s;
                        output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.Violation, s, f.Time,
                            s == Severity.Dangerous
                                ? "Dangerous lane change: the car behind had to brake hard to avoid you."
                                : "Unsafe lane change: a car behind had to brake for you. Check your mirror before moving across.",
                            Source133));
                        p.CleanSoFar = false;
                    }
                }
            }

            if (f.Time - p.CrossTime >= _p.ConflictWindowS)
            {
                if (p.CleanSoFar)
                    output.Add(new RuleEvent(RuleIdLaneChange, RuleOutcome.GoodPractice, Severity.None, f.Time,
                        "Good lane change: signalled in good time and moved across safely.", Source133));
                _pending = null;
            }
        }

        private bool InLane(TrafficCar c, int lane) => Math.Abs(c.X - _traffic.Lanes[lane].CenterX) <= 1.8;

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
