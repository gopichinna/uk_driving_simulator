using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>Guide for a straight road running north: offers the centre of each of your lanes.</summary>
    public sealed class StraightRoadGuide : ISteeringGuide
    {
        private readonly double[] _laneX;
        public StraightRoadGuide(params double[] laneCentresX) => _laneX = laneCentresX;
        public void Reset() { }

        public void GetTargets(in CarState car, double lookahead, List<Vec2> targets)
        {
            foreach (double x in _laneX)
                if (Math.Abs(car.X - x) < 6 && Math.Abs(SteeringAssist.Wrap(car.Heading)) < 1.0) targets.Add(new Vec2(x, car.Z + lookahead));
        }
    }
}
