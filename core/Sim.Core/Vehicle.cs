using System;

namespace LToLocal.Sim
{
    /// <summary>Driver inputs for one tick. Steer: -1 full left … +1 full right. Throttle/brake: 0…1.</summary>
    public readonly struct CarControls
    {
        public CarControls(double steer, double throttle, double brake)
        {
            Steer = Clamp(steer, -1, 1);
            Throttle = Clamp(throttle, 0, 1);
            Brake = Clamp(brake, 0, 1);
        }

        public double Steer { get; }
        public double Throttle { get; }
        public double Brake { get; }

        private static double Clamp(double v, double min, double max) =>
            double.IsNaN(v) ? 0 : Math.Max(min, Math.Min(max, v));
    }

    /// <summary>Tunable car parameters. Defaults approximate a small family hatchback (automatic).</summary>
    public sealed class CarParams
    {
        public double WheelbaseM { get; set; } = 2.6;
        public double MaxSteerDeg { get; set; } = 35;
        /// <summary>Steering lock is reduced at speed so the car stays stable: full lock at 0, this fraction at top speed.</summary>
        public double SteerAtTopSpeedFraction { get; set; } = 0.25;
        public double MaxAccelMps2 { get; set; } = 3.5;
        public double MaxBrakeMps2 { get; set; } = 8.0;
        /// <summary>Rolling resistance + engine braking when coasting.</summary>
        public double CoastDecelMps2 { get; set; } = 0.4;
        /// <summary>About 75 mph.</summary>
        public double TopSpeedMps { get; set; } = 33.5;
    }

    /// <summary>Car position and motion. X = east, Z = north (metres). Heading in radians, 0 = north, positive = clockwise (turning right).</summary>
    public struct CarState
    {
        public double X;
        public double Z;
        public double Heading;
        public double SpeedMps;
        public double SteerAngleRad;

        public double SpeedMph => SpeedMps * 2.236936;
    }

    /// <summary>
    /// Arcade-realistic car: kinematic bicycle model with simple longitudinal dynamics.
    /// Forward only for now (no reverse gear yet). See docs/04-architecture.md, section 5.
    /// </summary>
    public static class CarModel
    {
        public static CarState Step(CarState s, in CarControls c, CarParams p, double dt)
        {
            // Longitudinal
            double speedRatio = s.SpeedMps / p.TopSpeedMps;
            double accel = c.Throttle * p.MaxAccelMps2 * (1 - speedRatio * speedRatio);
            double decel = c.Brake * p.MaxBrakeMps2 + p.CoastDecelMps2;
            double speed = s.SpeedMps + (accel - decel) * dt;
            s.SpeedMps = Math.Max(0, Math.Min(p.TopSpeedMps, speed));

            // Steering (speed-sensitive lock)
            double speedFraction = s.SpeedMps / p.TopSpeedMps;
            double lockScale = 1 - (1 - p.SteerAtTopSpeedFraction) * speedFraction;
            s.SteerAngleRad = c.Steer * p.MaxSteerDeg * Math.PI / 180 * lockScale;

            // Kinematic bicycle
            double yawRate = s.SpeedMps / p.WheelbaseM * Math.Tan(s.SteerAngleRad);
            s.Heading = NormaliseAngle(s.Heading + yawRate * dt);
            s.X += s.SpeedMps * Math.Sin(s.Heading) * dt;
            s.Z += s.SpeedMps * Math.Cos(s.Heading) * dt;
            return s;
        }

        private static double NormaliseAngle(double a)
        {
            const double twoPi = 2 * Math.PI;
            a %= twoPi;
            if (a > Math.PI) a -= twoPi;
            if (a < -Math.PI) a += twoPi;
            return a;
        }
    }
}
