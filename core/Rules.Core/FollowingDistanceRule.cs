using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// GB.GAP.TWO_SECOND: leave at least a two-second gap to the vehicle in front (Highway Code rule 126;
    /// double it in the wet). Many instructors teach three seconds, so the message suggests that.
    /// Judged only at speed (default 20 mph and above): in slow queues the gap is about stopping distance, not time.
    /// One event per "too close" episode; it can escalate from minor to serious.
    /// </summary>
    public sealed class FollowingDistanceRule
    {
        public const string RuleId = "GB.GAP.TWO_SECOND";
        private const string Source = "Highway Code rule 126";
        private const double PlayerLength = 4.3;

        public double MinSpeedMph = 20;
        public double MinGapS = 2.0;
        public double DangerGapS = 1.0;
        public double MinorAfterS = 3.0;
        public double SeriousAfterS = 2.0;

        private double _closeSince = double.NaN, _dangerSince = double.NaN, _okSince = double.NaN;
        private Severity _reported = Severity.None;

        public void Reset()
        {
            _closeSince = _dangerSince = _okSince = double.NaN;
            _reported = Severity.None;
        }

        /// <summary>Time gap (s) to the nearest vehicle ahead in our lane, or +∞ if none.</summary>
        public static double HeadwayTo(in CarState ego, IReadOnlyList<RoadUser> users, out RoadUser leader)
        {
            leader = default;
            var me = new Vec2(ego.X, ego.Z);
            Vec2 fwd = Vec2.FromHeading(ego.Heading);
            double best = double.PositiveInfinity;
            foreach (var u in users)
            {
                if (Math.Cos(u.Heading - ego.Heading) < 0.8) continue;       // not going our way
                Vec2 d = u.Position - me;
                double along = d.Dot(fwd);
                if (along <= 0 || Math.Abs(d.Dot(fwd.LeftNormal)) > 1.6) continue; // behind us, or another lane
                double gap = along - (PlayerLength + u.LengthM) / 2;
                if (gap < best) { best = gap; leader = u; }
            }
            if (double.IsPositiveInfinity(best) || ego.SpeedMps < 0.5) return double.PositiveInfinity;
            return Math.Max(0, best) / ego.SpeedMps;
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            RoadUser leader = default;
            double h = f.Ego.SpeedMph < MinSpeedMph ? double.PositiveInfinity : HeadwayTo(f.Ego, users, out leader);

            if (h >= MinGapS)
            {
                _closeSince = _dangerSince = double.NaN;
                if (double.IsNaN(_okSince)) _okSince = f.Time;
                if (h > MinGapS + 0.5 && f.Time - _okSince > 3) _reported = Severity.None; // episode over
                return;
            }

            _okSince = double.NaN;
            if (double.IsNaN(_closeSince)) _closeSince = f.Time;
            if (h < DangerGapS) { if (double.IsNaN(_dangerSince)) _dangerSince = f.Time; }
            else _dangerSince = double.NaN;

            string who = leader.IsCyclist ? "the cyclist" : "the car";
            if (!double.IsNaN(_dangerSince) && f.Time - _dangerSince >= SeriousAfterS && _reported < Severity.Serious)
            {
                _reported = Severity.Serious;
                output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Serious, f.Time,
                    $"Far too close: less than one second behind {who} in front. Drop back to at least two seconds.", Source));
            }
            else if (f.Time - _closeSince >= MinorAfterS && _reported < Severity.Driving)
            {
                _reported = Severity.Driving;
                output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Driving, f.Time,
                    $"Too close to {who} in front (about {h:0.0} s). Leave at least a two-second gap; three is safer.", Source));
            }
        }
    }
}
