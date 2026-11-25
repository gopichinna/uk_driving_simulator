using System;

namespace LToLocal.Sim
{
    /// <summary>
    /// Turns variable frame times (from Unity's Update) into a whole number of fixed
    /// simulation ticks. The simulation always advances in steps of <see cref="StepSeconds"/>
    /// (50 Hz by default), so its behaviour does not depend on the phone's frame rate.
    /// See docs/04-architecture.md, section 4.
    /// </summary>
    public sealed class FixedStepClock
    {
        public const double DefaultStepSeconds = 1.0 / 50.0;

        /// <summary>Upper bound on ticks per frame, so a long hitch can't freeze the game catching up.</summary>
        public const int DefaultMaxStepsPerFrame = 5;

        private readonly int _maxStepsPerFrame;
        private double _accumulator;

        public FixedStepClock(double stepSeconds = DefaultStepSeconds, int maxStepsPerFrame = DefaultMaxStepsPerFrame)
        {
            if (stepSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            if (maxStepsPerFrame < 1) throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame));
            StepSeconds = stepSeconds;
            _maxStepsPerFrame = maxStepsPerFrame;
        }

        public double StepSeconds { get; }

        /// <summary>Total ticks advanced since creation.</summary>
        public long TickCount { get; private set; }

        /// <summary>Simulation time in seconds (TickCount × StepSeconds).</summary>
        public double SimTime => TickCount * StepSeconds;

        /// <summary>
        /// How far (0–1) the real time is between the last tick and the next one.
        /// The renderer uses this to interpolate between the last two simulation states.
        /// </summary>
        public double Alpha => _accumulator / StepSeconds;

        /// <summary>
        /// Adds a frame's elapsed real time and returns how many fixed ticks to run now.
        /// Time beyond the per-frame cap is dropped (the sim slows down instead of spiralling).
        /// </summary>
        public int Advance(double frameSeconds)
        {
            if (frameSeconds < 0 || double.IsNaN(frameSeconds))
                throw new ArgumentOutOfRangeException(nameof(frameSeconds));

            _accumulator += frameSeconds;
            int steps = (int)Math.Floor(_accumulator / StepSeconds + 1e-9);
            if (steps > _maxStepsPerFrame)
            {
                steps = _maxStepsPerFrame;
                _accumulator = 0;
            }
            else
            {
                _accumulator = Math.Max(0, _accumulator - steps * StepSeconds);
            }

            TickCount += steps;
            return steps;
        }
    }
}
