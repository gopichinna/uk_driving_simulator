using System;

namespace LToLocal.Sim
{
    /// <summary>
    /// Steering guide for the hand-built two-lane roundabout: it follows your entry lane round the ring and takes
    /// the next exit when you signal left. A firm push on the wheel moves you between the two ring lanes.
    /// </summary>
    public sealed class TwoLaneRoundaboutGuide : ISteeringGuide
    {
        private const double LaneChangeSteer = 0.4, LaneChangeHoldS = 0.3, CooldownS = 1.5;

        private readonly TwoLaneRoundaboutGeometry _g;
        private int _ringLane, _exitArm = -1;
        private double _hold, _cooldown;

        public TwoLaneRoundaboutGuide(TwoLaneRoundaboutGeometry geometry) => _g = geometry;

        public int CommittedExit => _exitArm;
        public void Reset() { _exitArm = -1; _hold = _cooldown = 0; }

        public bool TryTarget(in CarState car, int indicator, double steerIntent, double dt, double lookahead, out Vec2 target)
        {
            target = default;
            var pos = new Vec2(car.X, car.Z);

            // 1. Still on the way in?
            if (FindPath(pos, car.Heading, entry: true, out int arm, out int lane, out double s))
            {
                _ringLane = lane;
                var path = _g.Entry[arm, lane];
                double remain = path.Length - s;
                if (lookahead <= remain) { target = path.Sample(s + lookahead).pos; return true; }
                double theta0 = _g.MergeBearing[arm, lane];
                ChooseExit(theta0, indicator);
                target = RingTarget(theta0, lookahead - remain);
                return true;
            }

            // 2. On the way out of a committed exit?
            if (_exitArm >= 0)
            {
                var path = _g.Exit[_exitArm, _ringLane];
                var (es, _, ed) = path.Project(pos);
                if (ed < 2.5 && es > 2) { target = ExitTarget(_exitArm, _ringLane, es, lookahead); return true; }
            }

            // 3. On the ring.
            if (_g.OnRing(pos))
            {
                if (_hold == 0 && _exitArm < 0) _ringLane = TwoLaneRoundaboutGeometry.RingLaneAt(_g.RadiusOf(pos));
                double theta = _g.BearingOf(pos);
                UpdateLaneChange(steerIntent, dt);
                ChooseExit(theta, indicator);
                target = RingTarget(theta, lookahead);
                return true;
            }

            // 4. Leaving along any exit arm.
            if (FindPath(pos, car.Heading, entry: false, out arm, out lane, out s))
            {
                target = ExitTarget(arm, lane, s, lookahead);
                return true;
            }

            _exitArm = -1;
            return false;
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

        /// <summary>Signal left and the next exit ahead is yours. Cancel the signal and you stay on the ring.</summary>
        private void ChooseExit(double theta, int indicator)
        {
            if (_exitArm >= 0)
            {
                double togo = Cw(theta, _g.DivergeBearing[_exitArm, _ringLane]);
                if (togo < 0.5 || togo > 2 * Math.PI - 0.3) return;   // too close to change your mind
            }
            _exitArm = -1;
            if (indicator >= 0) return;
            double best = double.MaxValue;
            for (int a = 0; a < _g.ArmCount; a++)
            {
                double d = Cw(theta, _g.DivergeBearing[a, _ringLane]);
                if (d > 0.02 && d < best) { best = d; _exitArm = a; }
            }
        }

        private Vec2 RingTarget(double theta, double distance)
        {
            double radius = TwoLaneRoundaboutGeometry.LaneRadius(_ringLane);
            if (_exitArm >= 0)
            {
                double toDiverge = Cw(theta, _g.DivergeBearing[_exitArm, _ringLane]);
                if (toDiverge > 2 * Math.PI - 0.3) toDiverge = 0;
                double arc = toDiverge * radius;
                if (distance >= arc) return ExitTarget(_exitArm, _ringLane, 0, distance - arc);
            }
            return _g.RingPoint(theta + distance / radius, radius);
        }

        private Vec2 ExitTarget(int arm, int lane, double s, double ahead)
        {
            var path = _g.Exit[arm, lane];
            double t = s + ahead;
            if (t <= path.Length) return path.Sample(t).pos;
            var (end, h) = path.Sample(path.Length);
            return end + Vec2.FromHeading(h) * (t - path.Length);
        }

        private void UpdateLaneChange(double steer, double dt)
        {
            _cooldown = Math.Max(0, _cooldown - dt);
            if (_cooldown > 0 || _exitArm >= 0 || Math.Abs(steer) < LaneChangeSteer) { _hold = 0; return; }
            _hold += Math.Sign(steer) * dt;
            if (Math.Abs(_hold) < LaneChangeHoldS) return;
            _ringLane = steer > 0 ? 1 : 0;          // right is towards the middle (inner lane), left is the outer lane
            _cooldown = CooldownS;
            _hold = 0;
        }

        private static double Cw(double a, double b) => TwoLaneRoundaboutGeometry.Cw(a, b);
    }
}
