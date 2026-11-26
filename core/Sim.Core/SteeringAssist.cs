using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>How much the game helps with the wheel. The rules still judge where the car really goes.</summary>
    public enum AssistLevel { Off = 0, Light = 1, Full = 2 }

    /// <summary>Knows where the road goes: lists the lines you could be following ahead (stay in lane, each turn, each exit, the next lane).</summary>
    public interface ISteeringGuide
    {
        /// <summary>Adds one aim point per possible line, <paramref name="lookahead"/> metres ahead. Adds nothing if the car is not on a road the guide knows.</summary>
        void GetTargets(in CarState car, double lookahead, List<Vec2> targets);
    }

    /// <summary>
    /// Aim assist for the wheel. YOU steer and choose the line. The game keeps your wheel within a small band of the wheel position
    /// that would hold that line, so you stay in lane even if you turn too little or too much. If you are not turning, or you
    /// swerve decisively away from every line, you steer alone.
    /// </summary>
    public static class SteeringAssist
    {
        /// <summary>
        /// How far your wheel may stray from the line's wheel position before the game pulls it back (the "free play" band).
        /// Smaller = more help. Beyond the band the pull gets stronger smoothly, so spinning the wheel too hard is still reined in.
        /// </summary>
        public static double Band(AssistLevel level) => level switch { AssistLevel.Off => double.MaxValue, AssistLevel.Light => 0.30, _ => 0.12 };

        /// <summary>Below this you count as not turning (no help at all).</summary>
        public const double EngagedSteer = 0.10;

        /// <summary>A turn this far from every line is a deliberate swerve: the game stays out of it.</summary>
        public const double OverrideDeviation = 0.75;

        private static readonly List<Vec2> s_targets = new List<Vec2>();

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

        /// <summary>The controls to feed the car this step. Throttle and brake are never touched.</summary>
        public static CarControls Apply(in CarState car, in CarControls player, ISteeringGuide? guide, CarParams p, AssistLevel level)
        {
            double band = Band(level);
            if (guide == null || level == AssistLevel.Off || Math.Abs(player.Steer) < EngagedSteer || car.SpeedMps < 0.5) return player;

            s_targets.Clear();
            guide.GetTargets(car, Math.Max(5.0, car.SpeedMps * 1.0), s_targets);

            // Which line is the player turning for? The one whose wheel position is closest to theirs.
            double bestCmd = 0, bestDev = double.MaxValue;
            foreach (var t in s_targets)
            {
                double cmd = CommandToward(car, t, p);
                double dev = player.Steer - cmd;
                if (Math.Abs(dev) < Math.Abs(bestDev)) { bestDev = dev; bestCmd = cmd; }
            }
            if (bestDev == double.MaxValue || Math.Abs(bestDev) > OverrideDeviation) return player;

            // Free play inside the band, then a soft wall: an over-the-top turn is pulled back to just outside the band.
            double steer = bestCmd + band * Math.Tanh(bestDev / band);
            return new CarControls(steer, player.Throttle, player.Brake);
        }

        internal static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
