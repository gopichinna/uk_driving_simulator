using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Guide for courses built from a <see cref="LaneGraph"/> (the town, Wolverhampton, the motorway). It offers the line
    /// along your lane, every branch at the next junction (left, ahead, right, roundabout exits) and the lanes beside you.
    /// </summary>
    public sealed class LaneGraphGuide : ISteeringGuide
    {
        private readonly LaneGraph _g;
        private readonly List<Segment> _near = new List<Segment>();
        private int _seg = -1;

        public LaneGraphGuide(LaneGraph graph) => _g = graph;
        public void Reset() => _seg = -1;

        public void GetTargets(in CarState car, double lookahead, List<Vec2> targets)
        {
            var pos = new Vec2(car.X, car.Z);
            _seg = Snap(pos, car.Heading, _seg);
            if (_seg < 0) return;

            var cur = _g.Segments[_seg];
            double along = cur.Path.Project(pos).s;
            Collect(_seg, along, lookahead, 0, targets);

            if (cur.Kind == SegmentKind.Lane && cur.Group >= 0)      // the lanes beside you, for a lane change
                foreach (var sg in _g.Segments)
                {
                    if (sg.Id == _seg || sg.Kind != SegmentKind.Lane || sg.Group != cur.Group) continue;
                    var (s, _, d) = sg.Path.Project(pos);
                    if (d < 7) targets.Add(sg.Path.Sample(Math.Min(sg.Length, s + lookahead)).pos);
                }
        }

        /// <summary>Every place you could be <paramref name="remaining"/> metres on, following each branch.</summary>
        private void Collect(int seg, double s, double remaining, int depth, List<Vec2> into)
        {
            var sg = _g.Segments[seg];
            double avail = sg.Length - s;
            if (remaining <= avail) { into.Add(sg.Path.Sample(s + remaining).pos); return; }
            if (sg.Next.Count == 0 || depth >= 3) { into.Add(sg.Path.Sample(sg.Length).pos); return; }
            foreach (int n in sg.Next) Collect(n, 0, remaining - avail, depth + 1, into);
        }

        private int Snap(Vec2 pos, double heading, int current)
        {
            _near.Clear();
            _g.Near(pos, 12, _near);
            int best = -1;
            double bestScore = double.MaxValue;
            foreach (var sg in _near)
            {
                var (s, _, d) = sg.Path.Project(pos);
                double diff = Math.Abs(SteeringAssist.Wrap(sg.Path.Sample(s).heading - heading));
                if (diff > 1.05 || d > 8) continue;
                double score = d + diff * 4 - (sg.Id == current ? 1.5 : 0);   // stay on the current piece of road unless another fits clearly better
                if (score < bestScore) { bestScore = score; best = sg.Id; }
            }
            return best;
        }
    }
}
