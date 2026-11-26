using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>Guide for the hand-built single-lane roundabout: the way in, the ring, and the next exit ahead.</summary>
    public sealed class RoundaboutGuide : ISteeringGuide
    {
        private readonly RoundaboutGeometry _g;
        public RoundaboutGuide(RoundaboutGeometry geometry) => _g = geometry;
        public void Reset() { }

        public void GetTargets(in CarState car, double lookahead, List<Vec2> targets)
        {
            var pos = new Vec2(car.X, car.Z);
            for (int a = 0; a < _g.ArmCount; a++)
            {
                var entry = _g.Entry[a];
                var (s, _, d) = entry.Project(pos);
                if (d > 4 || s > entry.Length - 1 || Math.Abs(SteeringAssist.Wrap(entry.Sample(s).heading - car.Heading)) > 1.0) continue;
                double remain = entry.Length - s;
                if (lookahead <= remain) targets.Add(entry.Sample(s + lookahead).pos);
                else Chain(_g.EntryMergeS[a], lookahead - remain, targets);
                return;
            }
            if (_g.OnRing(pos)) { Chain(_g.Ring.Project(pos).s, lookahead, targets); return; }
            for (int a = 0; a < _g.ArmCount; a++)
            {
                var exit = _g.Exit[a];
                var (s, _, d) = exit.Project(pos);
                if (d > 4 || Math.Abs(SteeringAssist.Wrap(exit.Sample(s).heading - car.Heading)) > 1.0) continue;
                var (end, h) = exit.Sample(Math.Min(exit.Length, s + lookahead));
                targets.Add(s + lookahead <= exit.Length ? end : end + Vec2.FromHeading(h) * (s + lookahead - exit.Length));
                return;
            }
        }

        /// <summary>Round the ring from ring distance <paramref name="ringS"/>, and out of the next exit ahead.</summary>
        private void Chain(double ringS, double distance, List<Vec2> targets)
        {
            targets.Add(_g.Ring.Sample(ringS + distance).pos);
            int next = -1;
            double best = double.MaxValue;
            for (int a = 0; a < _g.ArmCount; a++)
            {
                double fwd = _g.RingForward(ringS, _g.ExitDivergeS[a]);
                if (fwd > 0.5 && fwd < best) { best = fwd; next = a; }
            }
            if (next >= 0 && distance >= best) targets.Add(_g.Exit[next].Sample(distance - best).pos);
        }
    }
}
