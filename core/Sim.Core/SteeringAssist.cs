using System;

namespace LToLocal.Sim
{
    /// <summary>How much the game helps with the wheel. The rules still judge where the car really goes.</summary>
    public enum AssistLevel { Off = 0, Light = 1, Full = 2 }

    /// <summary>
    /// Knows where the road goes. Each simulation step it names a point ahead on the line the car should follow,
    /// working out which lane, turn or exit you mean from your indicator and from a deliberate push on the wheel.
    /// </summary>
    public interface ISteeringGuide
    {
        /// <param name="indicator">-1 left, 0 off, +1 right.</param>
        /// <param name="steerIntent">The player's own wheel position, -1..1 (only used to spot a deliberate lane change).</param>
        /// <param name="lookahead">How far ahead to aim, metres.</param>
        /// <returns>False if the car is not on a road this guide knows: the player then steers alone.</returns>
        bool TryTarget(in CarState car, int indicator, double steerIntent, double dt, double lookahead, out Vec2 target);
    }

    /// <summary>Turns "aim at that point" into wheel movements (pure pursuit), blended with the player's own steering.</summary>
    public static class SteeringAssist
    {
        public static double Strength(AssistLevel level) => level switch { AssistLevel.Off => 0, AssistLevel.Light => 0.5, _ => 1 };

        /// <summary>The controls to feed the car this step. Throttle and brake are never touched.</summary>
        public static CarControls Apply(in CarState car, in CarControls player, ISteeringGuide? guide, int indicator,
                                        CarParams p, AssistLevel level, double dt)
        {
            double k = Strength(level);
            if (guide == null || k <= 0) return player;

            double lookahead = Math.Max(5.0, car.SpeedMps * 1.0);
            if (!guide.TryTarget(car, indicator, player.Steer, dt, lookahead, out Vec2 target)) return player;

            Vec2 to = target - new Vec2(car.X, car.Z);
            double dist = to.Length;
            if (dist < 0.5) return player;

            double alpha = Wrap(to.Heading - car.Heading);                     // + = the target is to the right
            double delta = Math.Atan(2 * p.WheelbaseM * Math.Sin(alpha) / dist);   // wheel angle that arcs onto the target
            double lockScale = 1 - (1 - p.SteerAtTopSpeedFraction) * Math.Min(1, car.SpeedMps / p.TopSpeedMps);
            double maxAngle = p.MaxSteerDeg * Math.PI / 180 * lockScale;          // undo CarModel's speed-sensitive lock
            double cmd = Math.Max(-1, Math.Min(1, delta / maxAngle));

            return new CarControls(player.Steer * (1 - k) + cmd * k, player.Throttle, player.Brake);
        }

        internal static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
