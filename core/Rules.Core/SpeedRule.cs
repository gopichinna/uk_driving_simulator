using System;
using System.Collections.Generic;

namespace LToLocal.Rules
{
    /// <summary>
    /// GB.SPD.OVER_LIMIT: never exceed the speed limit (Highway Code rule 124, MUST).
    /// One event per "speeding episode", re-raised only if the severity gets worse.
    /// Thresholds are ASSUMPTIONs pending ADI review (docs/02 §2.B).
    /// </summary>
    public sealed class SpeedRule
    {
        public const string RuleId = "GB.SPD.OVER_LIMIT";
        private const string Source = "Highway Code rule 124";

        private readonly double _sustainS;
        private readonly double _toleranceMph;
        private double _overSince = double.NaN;
        private double _underSince = double.NaN;
        private Severity _reported = Severity.None;

        public SpeedRule(double sustainS = 1.5, double toleranceMph = 0.5)
        {
            _sustainS = sustainS;
            _toleranceMph = toleranceMph;
        }

        public void Reset()
        {
            _overSince = _underSince = double.NaN;
            _reported = Severity.None;
        }

        public void OnFrame(in DriverFrame f, double limitMph, List<RuleEvent> output)
        {
            double mph = f.Ego.SpeedMph;
            double over = mph - limitMph;

            if (over <= _toleranceMph)
            {
                _overSince = double.NaN;
                if (double.IsNaN(_underSince)) _underSince = f.Time;
                if (f.Time - _underSince > 3.0) _reported = Severity.None; // episode over
                return;
            }

            _underSince = double.NaN;
            if (double.IsNaN(_overSince)) _overSince = f.Time;
            if (f.Time - _overSince < _sustainS) return;

            Severity s = over >= 15 || (limitMph <= 20 && over >= 10) ? Severity.Dangerous
                       : over >= 5 ? Severity.Serious
                       : Severity.Driving;
            if (s <= _reported) return;
            _reported = s;

            output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, s, f.Time,
                $"You were doing {Math.Round(mph):0} mph in a {limitMph:0} mph limit. The limit is a maximum, not a target.",
                Source));
        }
    }
}
