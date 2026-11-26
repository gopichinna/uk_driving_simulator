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
    /// Aim assist for the wheel. YOU steer. When your turn is roughly right for one of the lines ahead, the game pulls the wheel
    /// part of the way onto that line so you stay in lane. If you are not turning, or your turn matches no line, you steer alone.
    /// </summary>
    public static class SteeringAssist
    {
        /// <summary>How much of the gap between your wheel and the line the game closes.</summary>
        public static double Strength(AssistLevel level) => level switch { AssistLevel.Off => 0, AssistLevel.Light => 0.35, _ => 0.65 };

        /// <summary>Below this you count as not turning (no help at all).</summary>
        public const double EngagedSteer = 0.12;

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
            double k = Strength(level);
            if (guide == null || k <= 0 || Math.Abs(player.Steer) < EngagedSteer || car.SpeedMps < 0.5) return player;

            s_targets.Clear();
            guide.GetTargets(car, Math.Max(5.0, car.SpeedMps * 1.0), s_targets);

            // Which line is the player's turn closest to? Only help if it's roughly right (within about half a turn of it).
            double bestCmd = 0, bestDiff = double.MaxValue;
            foreach (var t in s_targets)
            {
                double cmd = CommandToward(car, t, p);
                double diff = Math.Abs(cmd - player.Steer);
                if (diff < bestDiff) { bestDiff = diff; bestCmd = cmd; }
            }
            if (bestDiff == double.MaxValue || bestDiff > 0.18 + 0.5 * Math.Abs(bestCmd)) return player;

            return new CarControls(player.Steer + k * (bestCmd - player.Steer), player.Throttle, player.Brake);
        }

        internal static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
