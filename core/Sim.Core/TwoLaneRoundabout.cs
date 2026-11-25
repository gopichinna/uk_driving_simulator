using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Two-lane UK roundabout (left-hand traffic, clockwise). Each arm has two approach lanes and one exit lane:
    ///   approach lane 0 (left / nearside):  left or straight on  → outer circulating lane
    ///   approach lane 1 (right / offside):  right or full circle  → inner circulating lane, moving to the outer
    ///                                       lane after passing the exit before the one you want (spiral style)
    /// Exits leave from the outer lane. Ring vehicles are described by bearing (radians, clockwise from north)
    /// and radius, so lane changes on the ring are just a change of radius.
    /// </summary>
    public sealed class TwoLaneRoundaboutGeometry
    {
        public static readonly double[] ArmBearingsDeg = { 0, 90, 180, 270 }; // N, E, S, W
        public const double LaneWidthM = 3.5;
        public const double InnerLaneR = 12.25;
        public const double OuterLaneR = 15.75;
        public const double DividerR = 14.0;
        public const double IslandR = 10.5;
        public const double OuterEdgeR = 17.5;
        public const double GiveWayDistanceM = 19.5;     // along the arm from the centre
        public static readonly double[] EntryLaneOffset = { 5.25, 1.75 }; // left of inbound travel; lane 0 = nearside
        public const double ExitLaneOffset = 1.825;      // left of outbound travel
        public static readonly double[] EntryMergeAngleDeg = { 38, 28 };   // after the arm's bearing, per lane
        public const double ExitDivergeAngleDeg = 32;    // before the arm's bearing

        public TwoLaneRoundaboutGeometry(Vec2 centre, double armLength = 150.0)
        {
            Centre = centre;
            ArmLength = armLength;
            int n = ArmCount;
            Entry = new PathPolyline[n, 2];
            GiveWayS = new double[n, 2];
            MergeBearing = new double[n, 2];
            Exit = new PathPolyline[n];
            DivergeBearing = new double[n];

            for (int k = 0; k < n; k++)
            {
                double b = Rad(ArmBearingsDeg[k]);
                Vec2 outward = Vec2.FromHeading(b);
                Vec2 inbound = outward * -1;

                for (int lane = 0; lane < 2; lane++)
                {
                    Vec2 off = inbound.LeftNormal * EntryLaneOffset[lane];
                    Vec2 start = centre + outward * armLength + off;
                    Vec2 giveWay = centre + outward * GiveWayDistanceM + off;
                    double mb = b + Rad(EntryMergeAngleDeg[lane]);
                    double r = lane == 0 ? OuterLaneR : InnerLaneR;
                    Vec2 merge = RingPoint(mb, r);
                    Vec2 tangent = Vec2.FromHeading(mb + Math.PI / 2);
                    double k1 = (merge - giveWay).Length * 0.45;
                    var pts = new List<Vec2> { start };
                    pts.AddRange(PathPolyline.Bezier(giveWay, giveWay + inbound * k1, merge - tangent * k1, merge, 14));
                    Entry[k, lane] = new PathPolyline(pts);
                    GiveWayS[k, lane] = (giveWay - start).Length;
                    MergeBearing[k, lane] = Norm(mb);
                }

                double db = b - Rad(ExitDivergeAngleDeg);
                Vec2 diverge = RingPoint(db, OuterLaneR);
                Vec2 dTangent = Vec2.FromHeading(db + Math.PI / 2);
                Vec2 outOff = outward.LeftNormal * ExitLaneOffset;
                Vec2 join = centre + outward * GiveWayDistanceM + outOff;
                Vec2 end = centre + outward * armLength + outOff;
                double k2 = (join - diverge).Length * 0.45;
                var exitPts = PathPolyline.Bezier(diverge, diverge + dTangent * k2, join - outward * k2, join, 14);
                exitPts.Add(end);
                Exit[k] = new PathPolyline(exitPts);
                DivergeBearing[k] = Norm(db);
            }
        }

        public Vec2 Centre { get; }
        public double ArmLength { get; }
        public int ArmCount => ArmBearingsDeg.Length;
        public PathPolyline[,] Entry { get; }
        public double[,] GiveWayS { get; }
        public double[,] MergeBearing { get; }
        public PathPolyline[] Exit { get; }
        public double[] DivergeBearing { get; }

        public static double Rad(double deg) => deg * Math.PI / 180;
        public static double Norm(double a) { a %= 2 * Math.PI; return a < 0 ? a + 2 * Math.PI : a; }
        /// <summary>Clockwise angle from a to b in [0, 2π).</summary>
        public static double Cw(double a, double b) => Norm(b - a);

        public Vec2 RingPoint(double bearing, double radius) => Centre + Vec2.FromHeading(bearing) * radius;
        public double BearingOf(Vec2 p) => Norm((p - Centre).Heading);
        public double RadiusOf(Vec2 p) => (p - Centre).Length;
        public bool OnRing(Vec2 p) { double r = RadiusOf(p); return r >= IslandR - 0.3 && r <= OuterEdgeR + 0.3; }
        public static double LaneRadius(int ringLane) => ringLane == 0 ? OuterLaneR : InnerLaneR;
        /// <summary>0 = outer lane, 1 = inner lane.</summary>
        public static int RingLaneAt(double radius) => radius >= DividerR ? 0 : 1;

        /// <summary>1 = first exit (left), 2 = straight on, 3 = right, 4 = full circle / U-turn.</summary>
        public int ExitNumber(int entryArm, int exitArm)
        {
            int d = ((exitArm - entryArm) % ArmCount + ArmCount) % ArmCount;
            return d == 0 ? ArmCount : d;
        }

        /// <summary>Lane to approach in, as the lane arrows show: left lane for left / ahead, right lane for right.</summary>
        public static int ApproachLaneFor(int exitNumber) => exitNumber <= 2 ? 0 : 1;
    }

    public enum TwoLaneSegment { Entry, Ring, Exit }

    public sealed class TwoLaneCar
    {
        public int Id;
        public int EntryArm, EntryLane, ExitArm;
        public TwoLaneSegment Segment;
        /// <summary>Front-bumper distance along the entry / exit path.</summary>
        public double S;
        // On the ring: bearing of the front bumper, radius, lanes and the angle still to go to the exit.
        public double Bearing, Radius, ThetaToGo;
        public int RingLane, TargetRingLane;
        public double RadialSpeed;
        public double SpeedMps, LastAccelMps2;
        public int Indicator;
        public int ColourIndex;
        public bool IsCyclist;
        public double LengthM = 4.3;
        public Vec2 Position;
        public double Heading;
    }

    public sealed class TwoLaneTrafficParams
    {
        public int TargetCars = 14;
        public double CarLengthM = 4.3;
        public double ApproachSpeedMps = 12.5;
        public double RingSpeedMps = 8.0;
        public double SlowDownBeforeGiveWayM = 30;
        public double CriticalGapS = 3.0;
        public double MinClearDistanceM = 9;
        public double LaneChangeRadialSpeed = 1.3;
        public int ColourCount = 6;
        public double[] ExitWeights = { 0.3, 0.4, 0.3 };
        public double CyclistChance = 0.10;
        public double CyclistLengthM = 1.8;
        public double CyclistSpeedMps = 5.5;
        public double PlayerLengthM = 4.3;
    }

    /// <summary>AI traffic for <see cref="TwoLaneRoundaboutGeometry"/> with correct lane choice and UK signalling.</summary>
    public sealed class TwoLaneRoundaboutTraffic
    {
        private readonly TwoLaneRoundaboutGeometry _g;
        private readonly TwoLaneTrafficParams _p;
        private readonly DeterministicRandom _rng;
        private readonly List<TwoLaneCar> _cars = new List<TwoLaneCar>();
        private int _nextId;

        public TwoLaneRoundaboutTraffic(TwoLaneRoundaboutGeometry g, TwoLaneTrafficParams p, ulong seed)
        {
            _g = g;
            _p = p;
            _rng = new DeterministicRandom(seed);
        }

        public TwoLaneRoundaboutGeometry Geometry => _g;
        public IReadOnlyList<TwoLaneCar> Cars => _cars;
        public void Reset() => _cars.Clear();

        public TwoLaneCar AddOnEntry(int arm, int lane, int exitArm, double s, double speed, bool cyclist = false)
        {
            var c = NewCar(arm, lane, exitArm, cyclist);
            c.Segment = TwoLaneSegment.Entry;
            c.S = s;
            c.SpeedMps = speed;
            UpdatePose(c);
            _cars.Add(c);
            return c;
        }

        /// <summary>Puts a car on the ring at a bearing in a lane (mainly for tests and scripted scenarios).</summary>
        public TwoLaneCar AddOnRing(int entryArm, int exitArm, int ringLane, double bearing, double speed)
        {
            var c = NewCar(entryArm, ringLane, exitArm, false);
            c.Segment = TwoLaneSegment.Ring;
            c.Bearing = TwoLaneRoundaboutGeometry.Norm(bearing);
            c.RingLane = c.TargetRingLane = ringLane;
            c.Radius = TwoLaneRoundaboutGeometry.LaneRadius(ringLane);
            c.ThetaToGo = TwoLaneRoundaboutGeometry.Cw(c.Bearing, _g.DivergeBearing[exitArm]);
            if (c.ThetaToGo < 0.05) c.ThetaToGo += 2 * Math.PI;
            c.SpeedMps = speed;
            UpdatePose(c);
            _cars.Add(c);
            return c;
        }

        private TwoLaneCar NewCar(int arm, int lane, int exitArm, bool cyclist) => new TwoLaneCar
        {
            Id = _nextId++,
            EntryArm = arm,
            EntryLane = lane,
            ExitArm = exitArm,
            IsCyclist = cyclist,
            LengthM = cyclist ? _p.CyclistLengthM : _p.CarLengthM,
            ColourIndex = _rng.Range(0, Math.Max(1, _p.ColourCount)),
        };

        public void Step(double dt, in CarState player, bool spawn = true)
        {
            var pp = new Vec2(player.X, player.Z);
            var accel = new double[_cars.Count];
            for (int i = 0; i < _cars.Count; i++) accel[i] = ComputeAccel(_cars[i], pp, player);

            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                var c = _cars[i];
                double v = Math.Max(0, c.SpeedMps + accel[i] * dt);
                c.LastAccelMps2 = (v - c.SpeedMps) / dt;
                c.SpeedMps = v;
                Advance(c, v * dt, dt, pp, player);
                if (c.Segment == TwoLaneSegment.Exit && c.S >= _g.Exit[c.ExitArm].Length)
                {
                    _cars.RemoveAt(i);
                    continue;
                }
                UpdatePose(c);
                c.Indicator = IndicatorFor(c);
            }

            if (spawn) Spawn(pp);
        }

        // ---------------- movement ----------------

        private void Advance(TwoLaneCar c, double ds, double dt, Vec2 pp, in CarState player)
        {
            if (c.Segment == TwoLaneSegment.Entry)
            {
                c.S += ds;
                var path = _g.Entry[c.EntryArm, c.EntryLane];
                if (c.S >= path.Length)
                {
                    double extra = c.S - path.Length;
                    c.Segment = TwoLaneSegment.Ring;
                    c.RingLane = c.TargetRingLane = c.IsCyclist ? 0 : c.EntryLane;
                    c.Radius = TwoLaneRoundaboutGeometry.LaneRadius(c.RingLane);
                    c.Bearing = _g.MergeBearing[c.EntryArm, c.EntryLane] + extra / c.Radius;
                    c.ThetaToGo = TwoLaneRoundaboutGeometry.Cw(c.Bearing, _g.DivergeBearing[c.ExitArm]);
                    if (c.ThetaToGo < 0.05) c.ThetaToGo += 2 * Math.PI;
                }
                return;
            }

            if (c.Segment == TwoLaneSegment.Ring)
            {
                double dTheta = ds / c.Radius;
                c.Bearing = TwoLaneRoundaboutGeometry.Norm(c.Bearing + dTheta);
                c.ThetaToGo -= dTheta;

                // Spiral: inner-lane cars move out after passing the exit before theirs.
                if (c.RingLane == 1 && c.TargetRingLane == 1 && c.ThetaToGo < Math.PI / 2 + 0.05 && OuterLaneClear(c, pp, player))
                    c.TargetRingLane = 0;

                double targetR = TwoLaneRoundaboutGeometry.LaneRadius(c.TargetRingLane);
                double before = c.Radius;
                double step = _p.LaneChangeRadialSpeed * dt;
                c.Radius = Math.Abs(targetR - c.Radius) <= step ? targetR : c.Radius + Math.Sign(targetR - c.Radius) * step;
                c.RadialSpeed = (c.Radius - before) / dt;
                if (c.Radius == targetR) c.RingLane = c.TargetRingLane;

                if (c.ThetaToGo <= 0)
                {
                    if (c.RingLane == 0 && c.Radius == TwoLaneRoundaboutGeometry.OuterLaneR)
                    {
                        c.Segment = TwoLaneSegment.Exit;
                        c.S = -c.ThetaToGo * c.Radius;
                    }
                    else
                    {
                        c.ThetaToGo += 2 * Math.PI; // couldn't get across in time: go round again (safely)
                    }
                }
                return;
            }

            c.S += ds;
        }

        private void UpdatePose(TwoLaneCar c)
        {
            if (c.Segment == TwoLaneSegment.Ring)
            {
                double centreBearing = c.Bearing - (c.LengthM / 2) / c.Radius;
                c.Position = _g.RingPoint(centreBearing, c.Radius);
                // Clockwise tangent; moving outward (to the left of travel) turns the nose left.
                double tilt = c.SpeedMps > 0.5 ? Math.Atan2(c.RadialSpeed, c.SpeedMps) : 0;
                c.Heading = centreBearing + Math.PI / 2 - tilt;
                return;
            }
            var path = c.Segment == TwoLaneSegment.Entry ? _g.Entry[c.EntryArm, c.EntryLane] : _g.Exit[c.ExitArm];
            double s = Math.Max(0, c.S - c.LengthM / 2);
            var (pos, h) = path.Sample(s);
            c.Position = pos;
            c.Heading = h;
        }

        private int IndicatorFor(TwoLaneCar c)
        {
            if (c.IsCyclist) return 0;
            int n = _g.ExitNumber(c.EntryArm, c.ExitArm);
            switch (c.Segment)
            {
                case TwoLaneSegment.Entry:
                    if (c.S < _g.GiveWayS[c.EntryArm, c.EntryLane] - 40) return 0;
                    return n == 1 ? -1 : n >= 3 ? 1 : 0;
                case TwoLaneSegment.Ring:
                    if (n == 1 || c.ThetaToGo < Math.PI / 2 * 0.95) return -1;
                    return n >= 3 ? 1 : 0;
                default:
                    return c.S < 8 ? -1 : 0;
            }
        }

        // ---------------- longitudinal ----------------

        private double ComputeAccel(TwoLaneCar c, Vec2 pp, in CarState player)
        {
            double desired = DesiredSpeed(c);
            double gap = double.PositiveInfinity, leaderSpeed = c.SpeedMps;

            void Consider(double frontToFront, double speed, double leaderLength)
            {
                double g = frontToFront - leaderLength;
                if (frontToFront > 0 && g < gap) { gap = Math.Max(0, g); leaderSpeed = speed; }
            }

            foreach (var o in _cars)
            {
                if (o == c) continue;
                double d = DistanceAhead(c, o);
                if (!double.IsNaN(d)) Consider(d, o.SpeedMps, o.LengthM);
            }

            double pd = PlayerAhead(c, pp, player, out double pv);
            if (!double.IsNaN(pd)) Consider(pd + _p.PlayerLengthM / 2, Math.Max(0, pv), _p.PlayerLengthM);

            if (c.Segment == TwoLaneSegment.Entry)
            {
                double line = _g.GiveWayS[c.EntryArm, c.EntryLane];
                if (c.S <= line + 0.5 && !CanEnter(c, pp, player)) Consider(line + 0.5 - c.S + 1.0, 0, 1.0);
            }

            return Idm.Accel(c.SpeedMps, desired, gap, leaderSpeed);
        }

        private double DesiredSpeed(TwoLaneCar c)
        {
            if (c.IsCyclist) return _p.CyclistSpeedMps;
            switch (c.Segment)
            {
                case TwoLaneSegment.Entry:
                    double toLine = _g.GiveWayS[c.EntryArm, c.EntryLane] - c.S;
                    if (toLine > _p.SlowDownBeforeGiveWayM) return _p.ApproachSpeedMps;
                    return Math.Max(_p.RingSpeedMps, _p.ApproachSpeedMps * Math.Max(0, toLine) / _p.SlowDownBeforeGiveWayM);
                case TwoLaneSegment.Ring:
                    return c.RingLane == 1 ? _p.RingSpeedMps * 0.9 : _p.RingSpeedMps;
                default:
                    return c.S < 20 ? _p.RingSpeedMps + (_p.ApproachSpeedMps - _p.RingSpeedMps) * c.S / 20 : _p.ApproachSpeedMps;
            }
        }

        private static bool SameRingLane(double r1, double r2) => Math.Abs(r1 - r2) < 2.4;

        /// <summary>Front-to-front distance from c to o along c's route, or NaN.</summary>
        private double DistanceAhead(TwoLaneCar c, TwoLaneCar o)
        {
            const double look = 60;
            double d = double.NaN;
            switch (c.Segment)
            {
                case TwoLaneSegment.Entry:
                    if (o.Segment == TwoLaneSegment.Entry && o.EntryArm == c.EntryArm && o.EntryLane == c.EntryLane) d = o.S - c.S;
                    else if (o.Segment == TwoLaneSegment.Ring)
                    {
                        double mb = _g.MergeBearing[c.EntryArm, c.EntryLane];
                        double r = TwoLaneRoundaboutGeometry.LaneRadius(c.IsCyclist ? 0 : c.EntryLane);
                        double past = TwoLaneRoundaboutGeometry.Cw(mb, o.Bearing) * r;
                        if (past < 14 && SameRingLane(o.Radius, r))
                            d = _g.Entry[c.EntryArm, c.EntryLane].Length - c.S + past;
                    }
                    break;

                case TwoLaneSegment.Ring:
                    if (o.Segment == TwoLaneSegment.Ring)
                    {
                        bool overlap = SameRingLane(o.Radius, c.Radius) || SameRingLane(o.Radius, TwoLaneRoundaboutGeometry.LaneRadius(c.TargetRingLane));
                        if (overlap)
                        {
                            double ahead = TwoLaneRoundaboutGeometry.Cw(c.Bearing, o.Bearing) * c.Radius;
                            if (ahead > 0.05 && ahead < 2 * Math.PI * c.Radius - 1) d = ahead;
                        }
                    }
                    else if (o.Segment == TwoLaneSegment.Exit && o.ExitArm == c.ExitArm && c.RingLane == 0)
                        d = c.ThetaToGo * c.Radius + o.S;
                    else if (o.Segment == TwoLaneSegment.Entry && o.S > _g.GiveWayS[o.EntryArm, o.EntryLane])
                    {
                        // committed entrant: treat as already at (just before) its merge point, in its target lane
                        double r = TwoLaneRoundaboutGeometry.LaneRadius(o.IsCyclist ? 0 : o.EntryLane);
                        if (SameRingLane(r, c.Radius))
                        {
                            double toMerge = TwoLaneRoundaboutGeometry.Cw(c.Bearing, _g.MergeBearing[o.EntryArm, o.EntryLane]) * c.Radius;
                            double stillToGo = _g.Entry[o.EntryArm, o.EntryLane].Length - o.S;
                            if (toMerge < c.ThetaToGo * c.Radius + 2) d = toMerge - stillToGo;
                        }
                    }
                    break;

                case TwoLaneSegment.Exit:
                    if (o.Segment == TwoLaneSegment.Exit && o.ExitArm == c.ExitArm) d = o.S - c.S;
                    break;
            }
            return d > 0 && d < look ? d : double.NaN;
        }

        /// <summary>Distance from c's front to the player's centre along c's route, or NaN.</summary>
        private double PlayerAhead(TwoLaneCar c, Vec2 pp, in CarState player, out double playerSpeedAlong)
        {
            playerSpeedAlong = 0;
            switch (c.Segment)
            {
                case TwoLaneSegment.Ring:
                {
                    double pr = _g.RadiusOf(pp);
                    if (!SameRingLane(pr, c.Radius) && !SameRingLane(pr, TwoLaneRoundaboutGeometry.LaneRadius(c.TargetRingLane))) break;
                    double ahead = TwoLaneRoundaboutGeometry.Cw(c.Bearing, _g.BearingOf(pp)) * c.Radius;
                    if (ahead > 60 || ahead < 0.1) break;
                    playerSpeedAlong = player.SpeedMps * Math.Cos(player.Heading - (_g.BearingOf(pp) + Math.PI / 2));
                    return ahead;
                }
                default:
                {
                    var path = c.Segment == TwoLaneSegment.Entry ? _g.Entry[c.EntryArm, c.EntryLane] : _g.Exit[c.ExitArm];
                    var (s, _, dist) = path.Project(pp);
                    if (dist > 2.2) break;
                    double ahead = s - c.S;
                    if (ahead < -2 || ahead > 60) break;
                    var (_, h) = path.Sample(s);
                    playerSpeedAlong = player.SpeedMps * Math.Cos(player.Heading - h);
                    return ahead;
                }
            }
            return double.NaN;
        }

        // ---------------- give way & lane changes ----------------

        public bool CanEnter(TwoLaneCar c, Vec2 pp, in CarState player)
        {
            double mb = _g.MergeBearing[c.EntryArm, c.EntryLane];
            bool toOuter = c.EntryLane == 0 || c.IsCyclist;

            foreach (var o in _cars)
            {
                if (o.Segment != TwoLaneSegment.Ring) continue;
                double upstreamAngle = TwoLaneRoundaboutGeometry.Cw(o.Bearing, mb);
                // An inner-lane car that will stay inner past our merge doesn't cross an outer-lane entry.
                if (toOuter && o.RingLane == 1 && o.TargetRingLane == 1 && o.ThetaToGo > upstreamAngle + Math.PI / 2) continue;
                if (Conflicts(upstreamAngle, o.Radius, o.SpeedMps, o.ThetaToGo, o.RingLane == 0)) return false;
            }

            if (_g.OnRing(pp))
            {
                double upstreamAngle = TwoLaneRoundaboutGeometry.Cw(_g.BearingOf(pp), mb);
                double along = player.SpeedMps * Math.Cos(player.Heading - (_g.BearingOf(pp) + Math.PI / 2));
                if (Conflicts(upstreamAngle, _g.RadiusOf(pp), Math.Max(0, along), double.PositiveInfinity, true)) return false;
            }
            return true;
        }

        private bool Conflicts(double upstreamAngle, double radius, double speed, double thetaToGo, bool canExitBefore)
        {
            double passedDist = (2 * Math.PI - upstreamAngle) * radius;
            if (passedDist < _p.MinClearDistanceM) return true;            // just passed our merge point
            if (canExitBefore && thetaToGo < upstreamAngle) return false;   // leaves before reaching us
            double dist = upstreamAngle * radius;
            if (dist < _p.MinClearDistanceM) return true;
            return dist / Math.Max(speed, 1.5) < _p.CriticalGapS;
        }

        private bool OuterLaneClear(TwoLaneCar c, Vec2 pp, in CarState player)
        {
            double R = TwoLaneRoundaboutGeometry.OuterLaneR;
            foreach (var o in _cars)
            {
                if (o == c || o.Segment != TwoLaneSegment.Ring || !SameRingLane(o.Radius, R)) continue;
                double ahead = TwoLaneRoundaboutGeometry.Cw(c.Bearing, o.Bearing) * R;   // o ahead of us
                double behind = TwoLaneRoundaboutGeometry.Cw(o.Bearing, c.Bearing) * R;  // o behind us
                if (ahead < 6 + o.LengthM || behind < 8 + Math.Max(0, o.SpeedMps - c.SpeedMps) * 2) return false;
            }
            if (SameRingLane(_g.RadiusOf(pp), R))
            {
                double ahead = TwoLaneRoundaboutGeometry.Cw(c.Bearing, _g.BearingOf(pp)) * R;
                double behind = TwoLaneRoundaboutGeometry.Cw(_g.BearingOf(pp), c.Bearing) * R;
                if (ahead < 9 || behind < 9) return false;
            }
            return true;
        }

        // ---------------- population ----------------

        private void Spawn(Vec2 pp)
        {
            if (_cars.Count >= _p.TargetCars) return;
            int arm = _rng.Range(0, _g.ArmCount);
            double r = _rng.NextDouble();
            int exitNo = r < _p.ExitWeights[0] ? 1 : r < _p.ExitWeights[0] + _p.ExitWeights[1] ? 2 : 3;
            bool cyclist = _rng.NextDouble() < _p.CyclistChance;
            int lane = cyclist ? 0 : TwoLaneRoundaboutGeometry.ApproachLaneFor(exitNo);

            foreach (var o in _cars)
                if (o.Segment == TwoLaneSegment.Entry && o.EntryArm == arm && o.EntryLane == lane && o.S < 20) return;
            var (start, _) = _g.Entry[arm, lane].Sample(0);
            if ((start - pp).Length < 25) return;

            AddOnEntry(arm, lane, (arm + exitNo) % _g.ArmCount, 0, cyclist ? _p.CyclistSpeedMps : _p.ApproachSpeedMps, cyclist);
        }
    }
}
