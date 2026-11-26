using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Guide for the hand-built two-lane roundabout. On the ring it offers: stay in your ring lane, the other ring lane,
    /// and the next exit ahead. Which one gets help is decided by how you turn the wheel. Changing ring lane is left free.
    /// </summary>
    public sealed class TwoLaneRoundaboutGuide : ISteeringGuide
    {
        private readonly TwoLaneRoundaboutGeometry _g;

        public TwoLaneRoundaboutGuide(TwoLaneRoundaboutGeometry geometry) => _g = geometry;
        public void Reset() { }

        public void GetTargets(in CarState car, double lookahead, List<AimPoint> targets)
        {
            var pos = new Vec2(car.X, car.Z);

            // 1. Still on the way in.
            if (FindPath(pos, car.Heading, entry: true, out int arm, out int lane, out double s))
            {
                var path = _g.Entry[arm, lane];
                double remain = path.Length - s;
                if (lookahead <= remain) { targets.Add(new AimPoint(path.Sample(s + lookahead).pos, EntryWeight(s, _g.GiveWayS[arm, lane]))); return; }
                RingLines(_g.MergeBearing[arm, lane], lane, lookahead - remain, targets, otherLane: false);
                return;
            }

            // 2. On the ring.
            if (_g.OnRing(pos))
            {
                int ringLane = TwoLaneRoundaboutGeometry.RingLaneAt(_g.RadiusOf(pos));
                RingLines(_g.BearingOf(pos), ringLane, lookahead, targets, otherLane: true);
                return;
            }

            // 3. Leaving along an exit arm.
            if (FindPath(pos, car.Heading, entry: false, out arm, out lane, out s))
                targets.Add(new AimPoint(ExitTarget(arm, lane, s, lookahead), 1 - SteeringAssist.Smooth(15, 30, s)));
        }

        private void RingLines(double theta, int ringLane, double distance, List<AimPoint> targets, bool otherLane)
        {
            double radius = TwoLaneRoundaboutGeometry.LaneRadius(ringLane);
            targets.Add(new AimPoint(_g.RingPoint(theta + distance / radius, radius)));                 // stay in this lane, round the ring
            if (otherLane)
            {
                int other = 1 - ringLane;
                double r2 = TwoLaneRoundaboutGeometry.LaneRadius(other);
                targets.Add(new AimPoint(_g.RingPoint(theta + distance / r2, r2), 1, neutral: true));        // move to the other ring lane: free choice
            }

            int exitArm = -1;                                                                    // the next exit ahead
            double best = double.MaxValue;
            for (int a = 0; a < _g.ArmCount; a++)
            {
                double d = Cw(theta, _g.DivergeBearing[a, ringLane]);
                if (d > 0.02 && d < best) { best = d; exitArm = a; }
            }
            if (exitArm < 0) return;
            double arc = best * radius;
            targets.Add(new AimPoint(distance >= arc ? ExitTarget(exitArm, ringLane, 0, distance - arc) : _g.RingPoint(theta + distance / radius, radius)));
        }

        private bool FindPath(Vec2 pos, double heading, bool entry, out int arm, out int lane, out double s)
        {
            arm = lane = -1; s = 0;
            double bestD = 5;
            for (int a = 0; a < _g.ArmCount; a++)
                for (int l = 0; l < 2; l++)
                {
                    var path = entry ? _g.Entry[a, l] : _g.Exit[a, l];
                    var (ps, _, d) = path.Project(pos);
                    if (d >= bestD) continue;
                    if (entry && ps > path.Length - 1) continue;
                    if (Math.Abs(SteeringAssist.Wrap(path.Sample(ps).heading - heading)) > 1.0) continue;
                    bestD = d; arm = a; lane = l; s = ps;
                }
            return arm >= 0;
        }

        /// <summary>No help on the straight approach: it fades in as you near the give-way line.</summary>
        private static double EntryWeight(double s, double giveWayS) => SteeringAssist.Smooth(giveWayS - 14, giveWayS - 4, s);

        private Vec2 ExitTarget(int arm, int lane, double s, double ahead)
        {
            var path = _g.Exit[arm, lane];
            double t = s + ahead;
            if (t <= path.Length) return path.Sample(t).pos;
            var (end, h) = path.Sample(path.Length);
            return end + Vec2.FromHeading(h) * (t - path.Length);
        }

        private static double Cw(double a, double b) => TwoLaneRoundaboutGeometry.Cw(a, b);
    }
}
