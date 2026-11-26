using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Guide for courses built from a <see cref="LaneGraph"/> (the town, Wolverhampton, the motorway). It offers the line along your
    /// lane and every branch at the next junction (left, ahead, right, roundabout exits). Only bends get help, never a straight lane.
    /// </summary>
    public sealed class LaneGraphGuide : ISteeringGuide
    {
        private readonly LaneGraph _g;
        private readonly List<Segment> _near = new List<Segment>();
        private int _seg = -1;

        public LaneGraphGuide(LaneGraph graph) => _g = graph;
        public void Reset() => _seg = -1;

        public void GetTargets(in CarState car, double lookahead, List<AimPoint> targets)
        {
            var pos = new Vec2(car.X, car.Z);
            _seg = Snap(pos, car.Heading, _seg);
            if (_seg < 0) return;

            var cur = _g.Segments[_seg];
            double along = cur.Path.Project(pos).s;
            Collect(_seg, along, lookahead, 0, targets);

            if (cur.Kind == SegmentKind.Lane && cur.Group >= 0)      // the lanes beside you: recognised as a lane change, never pulled
                foreach (var sg in _g.Segments)
                {
                    if (sg.Id == _seg || sg.Kind != SegmentKind.Lane || sg.Group != cur.Group) continue;
                    var (s, _, d) = sg.Path.Project(pos);
                    if (d < 7) targets.Add(new AimPoint(sg.Path.Sample(Math.Min(sg.Length, s + lookahead)).pos, 1, neutral: true));
                }
        }

        /// <summary>Every place you could be <paramref name="remaining"/> metres on, following each branch. Only bends get help.</summary>
        private void Collect(int seg, double s, double remaining, int depth, List<AimPoint> into)
        {
            var sg = _g.Segments[seg];
            double avail = sg.Length - s;
            if (remaining <= avail || sg.Next.Count == 0 || depth >= 3)
            {
                double at = Math.Min(sg.Length, s + remaining);
                into.Add(new AimPoint(sg.Path.Sample(at).pos, BendWeight(sg, at)));
                return;
            }
            foreach (int n in sg.Next) Collect(n, 0, remaining - avail, depth + 1, into);
        }

        /// <summary>1 on a turn or a roundabout bend, fading to 0 on a straight, so there is no help keeping a straight lane.</summary>
        private static double BendWeight(Segment sg, double s)
        {
            if (sg.Kind == SegmentKind.Connector && sg.Turn != TurnKind.Ahead) return 1;
            double h1 = sg.Path.Sample(s).heading, h2 = sg.Path.Sample(Math.Min(sg.Length, s + 15)).heading;
            double h0 = sg.Path.Sample(Math.Max(0, s - 15)).heading;
            double turn = Math.Max(Math.Abs(SteeringAssist.Wrap(h2 - h1)), Math.Abs(SteeringAssist.Wrap(h1 - h0)));
            return SteeringAssist.Smooth(0.15, 0.40, turn);
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
