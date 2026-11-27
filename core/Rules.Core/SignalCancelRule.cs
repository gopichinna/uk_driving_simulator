using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// GB.SIG.MISLEADING (rule 103): cancel your indicator once you have turned, and don't leave it on when you are
    /// not about to turn, because other road users may think you are. Two cases, both a minor (driving) fault:
    /// (1) the car has turned more than <see cref="TurnRad"/> since the indicator went on, has been going straight for
    ///     more than <see cref="AfterTurnS"/> seconds or <see cref="AfterTurnM"/> metres, and the indicator is still on;
    /// (2) the indicator has been on for <see cref="ForgottenS"/> seconds of straight driving with no turn seen (for example
    /// it was left on after a roundabout exit).
    /// Time standing still does not count (waiting to turn at lights). One report per time the indicator is switched on.
    /// Thresholds are PROPOSED values pending ADI review.
    /// </summary>
    public sealed class SignalCancelRule
    {
        public const string RuleId = "GB.SIG.MISLEADING";
        private const string Source = "Highway Code rule 103";

        private int _on;                 // -1 / +1 while an indicator is on
        private double _headingAtOn, _prevHeading, _prevTime = double.NaN;
        private bool _manoeuvreDone, _reported;
        private double _straightS, _straightM;

        public double TurnRad = 0.5, AfterTurnS = 5.0, AfterTurnM = 40.0, ForgottenS = 9.0;
        public double StraightRateRad = 0.05, MinMovingMps = 3.0;

        public void Reset()
        {
            _on = 0;
            _manoeuvreDone = _reported = false;
            _straightS = _straightM = 0;
            _prevTime = double.NaN;
        }

        public void OnFrame(in DriverFrame f, List<RuleEvent> output)
        {
            double dt = double.IsNaN(_prevTime) ? 0 : f.Time - _prevTime;
            double rate = dt > 1e-6 ? Math.Abs(LaneGraph.Angle(f.Ego.Heading - _prevHeading)) / dt : 0;
            _prevHeading = f.Ego.Heading;
            _prevTime = f.Time;

            if (f.Indicator == 0)
            {
                _on = 0;
                _manoeuvreDone = _reported = false;
                _straightS = _straightM = 0;
                return;
            }
            if (f.Indicator != _on)
            {
                _on = f.Indicator;
                _headingAtOn = f.Ego.Heading;
                _manoeuvreDone = _reported = false;
                _straightS = _straightM = 0;
                return;
            }
            if (f.Ego.SpeedMps < MinMovingMps) return; // waiting, or crawling in a queue

            if (!_manoeuvreDone && Math.Abs(LaneGraph.Angle(f.Ego.Heading - _headingAtOn)) > TurnRad) _manoeuvreDone = true;

            if (rate < StraightRateRad) { _straightS += dt; _straightM += f.Ego.SpeedMps * dt; }
            else { _straightS = _straightM = 0; }

            if (_reported) return;
            bool afterTurn = _manoeuvreDone && (_straightS > AfterTurnS || _straightM > AfterTurnM);
            bool forgotten = !_manoeuvreDone && _straightS > ForgottenS;
            if (!afterTurn && !forgotten) return;

            _reported = true;
            output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Driving, f.Time,
                afterTurn
                    ? "Your indicator was still on after you turned. Cancel it, or others may think you are turning again."
                    : "Your indicator has been on for a long time. If you are not turning, cancel it.", Source));
        }
    }
}
