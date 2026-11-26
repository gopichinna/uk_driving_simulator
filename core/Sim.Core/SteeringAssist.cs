using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>How much the game helps with the wheel. The rules still judge where the car really goes.</summary>
    public enum AssistLevel { Off = 0, Light = 1, Full = 2 }

    /// <summary>A place to aim at, one of the lines you could be following.</summary>
    public readonly struct AimPoint
    {
        public AimPoint(Vec2 pos, double weight = 1, bool neutral = false) { Pos = pos; Weight = weight; Neutral = neutral; }
        public Vec2 Pos { get; }
        /// <summary>0..1: how much help this line gets (1 in a turn or on a roundabout, 0 on a straight).</summary>
        public double Weight { get; }
        /// <summary>A line you may choose freely (a lane change): it is recognised but never pulled toward or away from.</summary>
        public bool Neutral { get; }
    }

    /// <summary>Knows where the road goes: lists the lines you could be following ahead.</summary>
    public interface ISteeringGuide
    {
        /// <summary>Adds one aim point per possible line, <paramref name="lookahead"/> metres ahead. Adds nothing if the car is not on a road the guide knows.</summary>
        void GetTargets(in CarState car, double lookahead, List<AimPoint> targets);
    }

    /// <summary>Wheel-position maths shared by the assist and the guides.</summary>
    public static class SteeringAssist
    {
        /// <summary>The wheel position that would arc the car onto <paramref name="target"/> (pure pursuit), -1..1.</summary>
        public static double CommandToward(in CarState car, Vec2 target, CarParams p)
        {
            Vec2 to = target - new Vec2(car.X, car.Z);
            double dist = to.Length;
            if (dist < 0.5) return 0;
            double alpha = Wrap(to.Heading - car.Heading);                         // + = the target is to the right
            double delta = Math.Atan(2 * p.WheelbaseM * Math.Sin(alpha) / dist);    // wheel angle that arcs onto the target
            double lockScale = 1 - (1 - p.SteerAtTopSpeedFraction) * Math.Min(1, car.SpeedMps / p.TopSpeedMps);
            double maxAngle = p.MaxSteerDeg * Math.PI / 180 * lockScale;            // undo CarModel's speed-sensitive lock
            return Math.Max(-1, Math.Min(1, delta / maxAngle));
        }

        public static double Smooth(double edge0, double edge1, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - edge0) / (edge1 - edge0)));
            return t * t * (3 - 2 * t);
        }

        internal static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }

    /// <summary>
    /// Aim assist for turns and roundabouts. YOU steer and choose the line. The game keeps your wheel within a small band of the
    /// wheel position that would hold that line, so you stay in lane even if you turn too little or too much. Nothing switches on or
    /// off abruptly: every on/off decision is a smooth fade, and the correction itself can only change at a limited rate, so the
    /// help never jerks the wheel. Hands off, no help. A decisive swerve away from every line, no help. One instance per drive.
    /// </summary>
    public sealed class SteeringAssistor
    {
        /// <summary>How far your wheel may stray from the line's wheel position before the game pulls it back.</summary>
        public static double Band(AssistLevel level) => level switch { AssistLevel.Off => double.MaxValue, AssistLevel.Light => 0.30, _ => 0.12 };

        public const double EngageFrom = 0.05, EngageFull = 0.18;       // |your wheel| over which help fades in
        public const double OverrideFrom = 0.50, OverrideFull = 0.80;   // deviation over which help fades out (a swerve)
        public const double CmdTimeConstantS = 0.12;                     // smoothing of the line's wheel position
        public const double MaxCorrectionRatePerS = 2.0;                 // the correction can change by at most this much per second

        private readonly List<AimPoint> _targets = new List<AimPoint>();
        private double _cmd, _corr;
        private bool _hasCmd;

        /// <summary>How much the wheel is being nudged right now (signed). Drives the on-screen ASSIST marker.</summary>
        public double Correction => _corr;

        public void Reset() { _corr = 0; _hasCmd = false; }

        /// <summary>The controls to feed the car this step. Throttle and brake are never touched.</summary>
        public CarControls Apply(in CarState car, in CarControls player, ISteeringGuide? guide, CarParams p, AssistLevel level, double dt)
        {
            double target = 0;
            if (guide != null && level != AssistLevel.Off && car.SpeedMps >= 0.5)
                target = TargetCorrection(car, player.Steer, guide, p, Band(level), dt);
            else _hasCmd = false;

            double step = MaxCorrectionRatePerS * dt;
            _corr += Math.Max(-step, Math.Min(step, target - _corr));
            double steer = Math.Max(-1, Math.Min(1, player.Steer + _corr));
            return new CarControls(steer, player.Throttle, player.Brake);
        }

        private double TargetCorrection(in CarState car, double player, ISteeringGuide guide, CarParams p, double band, double dt)
        {
            _targets.Clear();
            guide.GetTargets(car, Math.Max(5.0, car.SpeedMps * 1.0), _targets);
            if (_targets.Count == 0) { _hasCmd = false; return 0; }

            // The line you are turning for: the one whose wheel position is closest to yours.
            int best = 0;
            double bestCmd = 0, bestAbs = double.MaxValue;
            for (int i = 0; i < _targets.Count; i++)
            {
                double cmd = SteeringAssist.CommandToward(car, _targets[i].Pos, p);
                if (Math.Abs(player - cmd) < bestAbs) { bestAbs = Math.Abs(player - cmd); best = i; bestCmd = cmd; }
            }

            _cmd = _hasCmd ? _cmd + (bestCmd - _cmd) * Math.Min(1, dt / CmdTimeConstantS) : bestCmd;   // smooth over line changes
            _hasCmd = true;

            var line = _targets[best];
            if (line.Neutral) return 0;                                      // a lane change: neither helped nor hindered
            double dev = player - _cmd;
            double wanted = _cmd + band * Math.Tanh(dev / band) - player;     // free play inside the band, a soft wall outside it
            double engage = SteeringAssist.Smooth(EngageFrom, EngageFull, Math.Abs(player));
            double keep = 1 - SteeringAssist.Smooth(OverrideFrom, OverrideFull, Math.Abs(dev));
            return wanted * engage * keep * line.Weight;
        }
    }
}
