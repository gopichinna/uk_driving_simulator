using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Steering guide for courses built from a <see cref="LaneGraph"/> (the town, Wolverhampton, the motorway).
    /// It keeps the car in its lane, takes the left/right turn or roundabout exit you signal, and moves to the
    /// neighbouring lane when you push the wheel that way on purpose.
    /// </summary>
    public sealed class LaneGraphGuide : ISteeringGuide
    {
        private const double TurnThresholdRad = 0.35;     // a branch counts as a real left/right turn above this
        private const double LatchDistanceM = 15;          // the choice is fixed this close to the junction
        private const double LaneChangeSteer = 0.4, LaneChangeHoldS = 0.3, CooldownS = 1.2;

        private readonly LaneGraph _g;
        private readonly List<Segment> _near = new List<Segment>();
        private int _seg = -1;
        private int _latchSeg = -1, _latchNext = -1;
        private double _hold, _cooldown;

        public LaneGraphGuide(LaneGraph graph) => _g = graph;

        public int CurrentSegment => _seg;
        public void Reset() { _seg = -1; _latchSeg = _latchNext = -1; _hold = _cooldown = 0; }

        public bool TryTarget(in CarState car, int indicator, double steerIntent, double dt, double lookahead, out Vec2 target)
        {
            target = default;
            var pos = new Vec2(car.X, car.Z);

            if (_seg >= 0 && !StillOn(_seg, pos, car.Heading)) _seg = -1;
            if (_seg < 0) _seg = Snap(pos, car.Heading);
            if (_seg < 0) return false;

            // Moving on to the next piece of road once we reach the end of this one.
            for (int hop = 0; hop < 2; hop++)
            {
                var sg = _g.Segments[_seg];
                double s = sg.Path.Project(pos).s;
                if (s < sg.Length - 1.0 || sg.Next.Count == 0) break;
                _seg = ChooseNext(_seg, indicator, steerIntent, 0);
            }

            UpdateLaneChange(pos, car.Heading, steerIntent, dt);

            var cur = _g.Segments[_seg];
            double along = cur.Path.Project(pos).s;
            if (cur.Length - along < LatchDistanceM && cur.Next.Count > 1 && _latchSeg != _seg)
            {
                _latchSeg = _seg;
                _latchNext = ChooseNext(_seg, indicator, steerIntent, 0);
            }
            target = Walk(_seg, along, lookahead, indicator, steerIntent);
            return true;
        }

        private bool StillOn(int id, Vec2 pos, double heading)
        {
            var (s, _, d) = _g.Segments[id].Path.Project(pos);
            if (d > 6) return false;
            var h = _g.Segments[id].Path.Sample(s).heading;
            return Math.Abs(SteeringAssist.Wrap(h - heading)) < 1.4;
        }

        private int Snap(Vec2 pos, double heading)
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
                double score = d + diff * 4;
                if (score < bestScore) { bestScore = score; best = sg.Id; }
            }
            return best;
        }

        /// <summary>The piece of road that follows <paramref name="id"/>, picked by your indicator (or your wheel) when the road forks.</summary>
        private int ChooseNext(int id, int indicator, double steer, int _)
        {
            var sg = _g.Segments[id];
            if (sg.Next.Count == 1) return sg.Next[0];
            if (_latchSeg == id && _latchNext >= 0) return _latchNext;

            double headingOut = sg.Path.Sample(sg.Length).heading;
            int ahead = -1, left = -1, right = -1;
            double aheadAbs = double.MaxValue, leftTurn = 0, rightTurn = 0;
            foreach (int n in sg.Next)
            {
                var path = _g.Segments[n].Path;
                double turn = SteeringAssist.Wrap(path.Sample(path.Length).heading - path.Sample(0).heading);
                if (_g.Segments[n].Kind == SegmentKind.Lane) turn = SteeringAssist.Wrap(path.Sample(Math.Min(path.Length, 10)).heading - headingOut);
                if (Math.Abs(turn) < aheadAbs) { aheadAbs = Math.Abs(turn); ahead = n; }
                if (turn < leftTurn) { leftTurn = turn; left = n; }
                if (turn > rightTurn) { rightTurn = turn; right = n; }
            }
            int want = indicator < 0 ? -1 : indicator > 0 ? 1 : 0;
            if (want == 0 && aheadAbs > 0.6) want = steer < -0.3 ? -1 : steer > 0.3 ? 1 : 0;   // a T-junction: your wheel decides
            if (want < 0 && left >= 0 && leftTurn < -TurnThresholdRad) return left;
            if (want > 0 && right >= 0 && rightTurn > TurnThresholdRad) return right;
            return ahead >= 0 ? ahead : sg.Next[0];
        }

        private Vec2 Walk(int seg, double s, double remaining, int indicator, double steer)
        {
            for (int hop = 0; hop < 5; hop++)
            {
                var sg = _g.Segments[seg];
                double avail = sg.Length - s;
                if (remaining <= avail) return sg.Path.Sample(s + remaining).pos;
                remaining -= avail;
                if (sg.Next.Count == 0) return sg.Path.Sample(sg.Length).pos;
                seg = ChooseNext(seg, indicator, steer, 0);
                s = 0;
            }
            var last = _g.Segments[seg];
            return last.Path.Sample(last.Length).pos;
        }

        /// <summary>A firm push on the wheel for a moment moves you to the next lane that way on the same road.</summary>
        private void UpdateLaneChange(Vec2 pos, double heading, double steer, double dt)
        {
            _cooldown = Math.Max(0, _cooldown - dt);
            if (_cooldown > 0 || Math.Abs(steer) < LaneChangeSteer) { _hold = 0; return; }
            _hold = Math.Sign(steer) == Math.Sign(_hold) || _hold == 0 ? _hold + Math.Sign(steer) * dt : Math.Sign(steer) * dt;
            if (Math.Abs(_hold) < LaneChangeHoldS) return;

            var cur = _g.Segments[_seg];
            if (cur.Kind != SegmentKind.Lane || cur.Group < 0) { _hold = 0; return; }
            int best = -1;
            double bestD = 7;
            foreach (var sg in _g.Segments)
            {
                if (sg.Id == _seg || sg.Kind != SegmentKind.Lane || sg.Group != cur.Group) continue;
                var (_, lateral, d) = sg.Path.Project(pos);
                bool onWantedSide = steer < 0 ? lateral < 0 : lateral > 0;   // lateral > 0 means we are left of that lane, so it lies to our right
                if (onWantedSide && d < bestD) { bestD = d; best = sg.Id; }
            }
            if (best >= 0) { _seg = best; _latchSeg = _latchNext = -1; _cooldown = CooldownS; }
            _hold = 0;
        }
    }
}
