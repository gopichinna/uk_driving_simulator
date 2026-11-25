using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>
    /// Hand-authored single-lane roundabout for a left-hand-traffic country (UK): traffic circulates
    /// clockwise and joins by turning left. Arms are two-way roads; arm k points out from the centre at
    /// <see cref="ArmBearingsDeg"/>[k] (0 = north, 90 = east…). Exit numbers count clockwise from the entry.
    /// All lane paths are built here once, so the simulation, the rules and the rendered road share one geometry.
    /// </summary>
    public sealed class RoundaboutGeometry
    {
        public RoundaboutGeometry(Vec2 centre, double ringRadius = 14.0, double armLength = 150.0, double laneOffset = 1.825)
        {
            Centre = centre;
            RingRadius = ringRadius;
            ArmLength = armLength;
            LaneOffset = laneOffset;

            // Ring: clockwise from north. Bearing b (radians) ↦ centre + R·(sin b, cos b); s = R·b.
            var ring = new List<Vec2>();
            const int ringSteps = 120;
            for (int i = 0; i < ringSteps; i++) ring.Add(RingPoint(2 * Math.PI * i / ringSteps));
            Ring = new PathPolyline(ring, closed: true);

            int n = ArmBearingsDeg.Length;
            Entry = new PathPolyline[n];
            Exit = new PathPolyline[n];
            EntryMergeS = new double[n];
            ExitDivergeS = new double[n];
            GiveWayS = new double[n];

            for (int k = 0; k < n; k++)
            {
                double b = ArmBearingsDeg[k] * Math.PI / 180;
                Vec2 outward = Vec2.FromHeading(b);
                Vec2 inbound = outward * -1;
                Vec2 inLane = inbound.LeftNormal * laneOffset;   // keep left on the way in
                Vec2 outLane = outward.LeftNormal * laneOffset;  // keep left on the way out

                // Entry: straight in, give way at the outer edge, then a left-turning curve onto the ring.
                Vec2 start = centre + outward * armLength + inLane;
                Vec2 giveWay = centre + outward * (ringRadius + GiveWayOffsetM) + inLane;
                double mergeB = b + MergeAngleDeg * Math.PI / 180;
                Vec2 merge = RingPoint(mergeB);
                Vec2 mergeTangent = Vec2.FromHeading(mergeB + Math.PI / 2); // clockwise tangent
                double k1 = (merge - giveWay).Length * 0.45;
                var entryPts = new List<Vec2> { start };
                entryPts.AddRange(PathPolyline.Bezier(giveWay, giveWay + inbound * k1, merge - mergeTangent * k1, merge, 12));
                Entry[k] = new PathPolyline(entryPts);
                GiveWayS[k] = (giveWay - start).Length;
                EntryMergeS[k] = Ring.Wrap(ringRadius * mergeB);

                // Exit: leave the ring with a left-turning curve, then straight out.
                double divergeB = b - MergeAngleDeg * Math.PI / 180;
                Vec2 diverge = RingPoint(divergeB);
                Vec2 divergeTangent = Vec2.FromHeading(divergeB + Math.PI / 2);
                Vec2 exitJoin = centre + outward * (ringRadius + GiveWayOffsetM) + outLane;
                Vec2 end = centre + outward * armLength + outLane;
                double k2 = (exitJoin - diverge).Length * 0.45;
                var exitPts = PathPolyline.Bezier(diverge, diverge + divergeTangent * k2, exitJoin - outward * k2, exitJoin, 12);
                exitPts.Add(end);
                Exit[k] = new PathPolyline(exitPts);
                ExitDivergeS[k] = Ring.Wrap(ringRadius * divergeB);
            }
        }

        public static readonly double[] ArmBearingsDeg = { 0, 90, 180, 270 }; // N, E, S, W
        public const double GiveWayOffsetM = 4.5;   // give-way line distance outside the ring centreline
        public const double MergeAngleDeg = 24;     // where entries join / exits leave, either side of the arm
        public const double RingHalfWidthM = 3.0;   // circulatory carriageway half-width

        public Vec2 Centre { get; }
        public double RingRadius { get; }
        public double ArmLength { get; }
        public double LaneOffset { get; }
        public PathPolyline Ring { get; }
        public PathPolyline[] Entry { get; }
        public PathPolyline[] Exit { get; }
        public double[] EntryMergeS { get; }
        public double[] ExitDivergeS { get; }
        public double[] GiveWayS { get; }
        public int ArmCount => ArmBearingsDeg.Length;

        public Vec2 RingPoint(double bearingRad) => Centre + Vec2.FromHeading(bearingRad) * RingRadius;

        /// <summary>1 = first exit (left), 2 = straight on, 3 = right, 4 = full circle / U-turn.</summary>
        public int ExitNumber(int entryArm, int exitArm)
        {
            int d = ((exitArm - entryArm) % ArmCount + ArmCount) % ArmCount;
            return d == 0 ? ArmCount : d;
        }

        /// <summary>Clockwise distance along the ring from a to b.</summary>
        public double RingForward(double fromS, double toS)
        {
            double d = (toS - fromS) % Ring.Length;
            return d < 0 ? d + Ring.Length : d;
        }

        /// <summary>Is a point on the circulatory carriageway?</summary>
        public bool OnRing(Vec2 p) => Math.Abs((p - Centre).Length - RingRadius) <= RingHalfWidthM;
    }

    public enum RoundaboutSegment { Entry, Ring, Exit }

    public sealed class RoundaboutCar
    {
        public int Id;
        public int EntryArm;
        public int ExitArm;
        public RoundaboutSegment Segment;
        /// <summary>Front-bumper distance along the current segment's path.</summary>
        public double S;
        public double RingTravelled;
        public double SpeedMps;
        public double LastAccelMps2;
        public int Indicator;
        public int ColourIndex;
        public Vec2 Position;   // centre of the car
        public double Heading;
        public bool IsCyclist;
        public double LengthM = 4.3;
    }

    public sealed class RoundaboutTrafficParams
    {
        public int TargetCars = 10;
        public double CarLengthM = 4.3;
        public double ApproachSpeedMps = 12.5;   // ~28 mph on the arms
        public double RingSpeedMps = 7.0;        // ~16 mph round the ring
        public double SlowDownBeforeGiveWayM = 30;
        /// <summary>Critical gap: a ring vehicle arriving at the merge point sooner than this blocks entry.</summary>
        public double CriticalGapS = 3.2;
        public double MinClearDistanceM = 9;
        public int ColourCount = 6;
        /// <summary>Probabilities for exits 1 (left), 2 (ahead), 3 (right).</summary>
        public double[] ExitWeights = { 0.35, 0.4, 0.25 };
        public double CyclistChance = 0.10;
        public double CyclistLengthM = 1.8;
        public double CyclistSpeedMps = 5.5;
        /// <summary>Following gap in seconds (AI keeps 3 s; the Highway Code minimum is 2 s, rule 126).</summary>
        public double TimeHeadwayS = 3.0;
    }

    /// <summary>
    /// AI traffic for <see cref="RoundaboutGeometry"/>: IDM along lane paths, give way to traffic from the right
    /// at the entry (gap acceptance), UK signalling (left for the first exit, right for exits past 12 o'clock,
    /// left after passing the exit before theirs), and yields to the player's car wherever it is on their path.
    /// </summary>
    public sealed class RoundaboutTraffic
    {
        private readonly RoundaboutGeometry _g;
        private readonly RoundaboutTrafficParams _p;
        private readonly DeterministicRandom _rng;
        private readonly List<RoundaboutCar> _cars = new List<RoundaboutCar>();
        private int _nextId;

        public RoundaboutTraffic(RoundaboutGeometry geometry, RoundaboutTrafficParams p, ulong seed)
        {
            _g = geometry;
            _p = p;
            _rng = new DeterministicRandom(seed);
        }

        public IReadOnlyList<RoundaboutCar> Cars => _cars;
        public RoundaboutGeometry Geometry => _g;

        public void Reset()
        {
            _cars.Clear();
        }

        public RoundaboutCar AddCar(int entryArm, int exitArm, RoundaboutSegment segment, double s, double speed, bool isCyclist = false)
        {
            var c = new RoundaboutCar
            {
                IsCyclist = isCyclist,
                LengthM = isCyclist ? _p.CyclistLengthM : _p.CarLengthM,
                Id = _nextId++,
                EntryArm = entryArm,
                ExitArm = exitArm,
                Segment = segment,
                S = s,
                SpeedMps = speed,
                ColourIndex = _rng.Range(0, Math.Max(1, _p.ColourCount)),
            };
            if (segment == RoundaboutSegment.Ring)
                c.RingTravelled = _g.RingForward(_g.EntryMergeS[entryArm], s);
            UpdatePose(c);
            return Add(c);
        }

        private RoundaboutCar Add(RoundaboutCar c)
        {
            _cars.Add(c);
            return c;
        }

        public void Step(double dt, in CarState player, bool spawn = true)
        {
            var playerPos = new Vec2(player.X, player.Z);
            var accel = new double[_cars.Count];
            for (int i = 0; i < _cars.Count; i++) accel[i] = ComputeAccel(_cars[i], playerPos, player);

            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                var c = _cars[i];
                double v = Math.Max(0, c.SpeedMps + accel[i] * dt);
                c.LastAccelMps2 = (v - c.SpeedMps) / dt;
                c.SpeedMps = v;
                Advance(c, v * dt);
                if (c.Segment == RoundaboutSegment.Exit && c.S >= _g.Exit[c.ExitArm].Length)
                {
                    _cars.RemoveAt(i);
                    continue;
                }
                UpdatePose(c);
                c.Indicator = IndicatorFor(c);
            }

            if (spawn) SpawnIfNeeded(playerPos);
        }

        // ---------- movement ----------

        private void Advance(RoundaboutCar c, double ds)
        {
            c.S += ds;
            if (c.Segment == RoundaboutSegment.Entry && c.S >= _g.Entry[c.EntryArm].Length)
            {
                double extra = c.S - _g.Entry[c.EntryArm].Length;
                c.Segment = RoundaboutSegment.Ring;
                c.S = _g.Ring.Wrap(_g.EntryMergeS[c.EntryArm] + extra);
                c.RingTravelled = extra;
            }
            else if (c.Segment == RoundaboutSegment.Ring)
            {
                c.RingTravelled += ds;
                c.S = _g.Ring.Wrap(c.S);
                double needed = RingDistance(c);
                if (c.RingTravelled >= needed)
                {
                    c.Segment = RoundaboutSegment.Exit;
                    c.S = c.RingTravelled - needed;
                }
            }
        }

        /// <summary>Ring distance from this car's merge point to its exit's diverge point.</summary>
        private double RingDistance(RoundaboutCar c)
        {
            double d = _g.RingForward(_g.EntryMergeS[c.EntryArm], _g.ExitDivergeS[c.ExitArm]);
            return d < 1 ? d + _g.Ring.Length : d;
        }

        private PathPolyline PathOf(RoundaboutCar c) => c.Segment switch
        {
            RoundaboutSegment.Entry => _g.Entry[c.EntryArm],
            RoundaboutSegment.Ring => _g.Ring,
            _ => _g.Exit[c.ExitArm],
        };

        private void UpdatePose(RoundaboutCar c)
        {
            // Position of the car's centre: half a car length behind the front bumper, along the path.
            var path = PathOf(c);
            double centreS = c.S - c.LengthM / 2;
            if (!path.Closed && centreS < 0) centreS = 0;
            var (pos, heading) = path.Sample(centreS);
            c.Position = pos;
            c.Heading = heading;
        }

        private int IndicatorFor(RoundaboutCar c)
        {
            int exit = _g.ExitNumber(c.EntryArm, c.ExitArm);
            switch (c.Segment)
            {
                case RoundaboutSegment.Entry:
                    if (c.S < _g.GiveWayS[c.EntryArm] - 40) return 0;
                    return exit == 1 ? -1 : exit >= 3 ? 1 : 0;
                case RoundaboutSegment.Ring:
                    double remaining = RingDistance(c) - c.RingTravelled;
                    if (exit == 1 || remaining < _g.Ring.Length / _g.ArmCount * 0.9) return -1; // passed the exit before ours
                    return exit >= 3 ? 1 : 0;
                default:
                    return c.S < 8 ? -1 : 0;
            }
        }

        // ---------- longitudinal control ----------

        private double ComputeAccel(RoundaboutCar c, Vec2 playerPos, in CarState player)
        {
            double desired = DesiredSpeed(c);
            double gap = double.PositiveInfinity, leaderSpeed = c.SpeedMps;

            void Consider(double distanceAhead, double speed, double leaderLength)
            {
                double g = distanceAhead - leaderLength;
                if (distanceAhead > 0 && g < gap) { gap = Math.Max(0, g); leaderSpeed = speed; }
            }

            // Other AI vehicles ahead on our route (distances are front bumper to front bumper)
            foreach (var o in _cars)
            {
                if (o == c) continue;
                double d = RouteDistanceTo(c, o);
                if (!double.IsNaN(d)) Consider(d, o.SpeedMps, o.LengthM);
            }

            // The player (4.3 m car), wherever they are on our route; pd is to the player's centre
            double pd = PlayerRouteDistance(c, playerPos, player, out double playerAlong);
            if (!double.IsNaN(pd)) Consider(pd + _p.CarLengthM / 2, Math.Max(0, playerAlong), _p.CarLengthM);

            // Give way at the entry line until there is a safe gap
            if (c.Segment == RoundaboutSegment.Entry && c.S <= _g.GiveWayS[c.EntryArm] + 0.5 && !CanEnter(c, playerPos, player))
                Consider(_g.GiveWayS[c.EntryArm] + 0.5 - c.S + 1.0, 0, 1.0);

            return Idm.Accel(c.SpeedMps, desired, gap, leaderSpeed, headway: _p.TimeHeadwayS);
        }

        private double DesiredSpeed(RoundaboutCar c)
        {
            if (c.IsCyclist) return _p.CyclistSpeedMps;
            switch (c.Segment)
            {
                case RoundaboutSegment.Entry:
                    double toLine = _g.GiveWayS[c.EntryArm] - c.S;
                    if (toLine > _p.SlowDownBeforeGiveWayM) return _p.ApproachSpeedMps;
                    return Math.Max(_p.RingSpeedMps, _p.ApproachSpeedMps * Math.Max(0, toLine) / _p.SlowDownBeforeGiveWayM);
                case RoundaboutSegment.Ring:
                    return _p.RingSpeedMps;
                default:
                    return c.S < 20 ? _p.RingSpeedMps + (_p.ApproachSpeedMps - _p.RingSpeedMps) * c.S / 20 : _p.ApproachSpeedMps;
            }
        }

        /// <summary>How far ahead of c (front to front) o is along c's remaining route, or NaN if not on it.</summary>
        private double RouteDistanceTo(RoundaboutCar c, RoundaboutCar o)
        {
            const double lookAhead = 70;
            double d = double.NaN;
            switch (c.Segment)
            {
                case RoundaboutSegment.Entry:
                    if (o.Segment == RoundaboutSegment.Entry && o.EntryArm == c.EntryArm) d = o.S - c.S;
                    else if (o.Segment == RoundaboutSegment.Ring)
                    {
                        // a car that has just merged from our entry, still right in front of us
                        double past = _g.RingForward(_g.EntryMergeS[c.EntryArm], o.S);
                        if (past < 15) d = _g.Entry[c.EntryArm].Length - c.S + past;
                    }
                    break;
                case RoundaboutSegment.Ring:
                    double remaining = RingDistance(c) - c.RingTravelled;
                    if (o.Segment == RoundaboutSegment.Ring)
                    {
                        double ahead = _g.RingForward(c.S, o.S);
                        if (ahead > 0.01) d = ahead;
                    }
                    else if (o.Segment == RoundaboutSegment.Exit && o.ExitArm == c.ExitArm) d = remaining + o.S;
                    else if (o.Segment == RoundaboutSegment.Entry && o.S > _g.GiveWayS[o.EntryArm])
                    {
                        // A car that has committed past its give-way line will join just ahead of its merge point:
                        // treat it as already being there, so we don't drive into it as it merges.
                        double toMerge = _g.RingForward(c.S, _g.EntryMergeS[o.EntryArm]);
                        double stillToGo = _g.Entry[o.EntryArm].Length - o.S;
                        if (toMerge <= remaining + 2) d = toMerge - stillToGo;
                    }
                    break;
                case RoundaboutSegment.Exit:
                    if (o.Segment == RoundaboutSegment.Exit && o.ExitArm == c.ExitArm) d = o.S - c.S;
                    break;
            }
            return d > 0 && d < lookAhead ? d : double.NaN;
        }

        /// <summary>Distance from c's front bumper to the player's centre along c's route (NaN if the player isn't on it).</summary>
        private double PlayerRouteDistance(RoundaboutCar c, Vec2 playerPos, in CarState player, out double playerSpeedAlong)
        {
            playerSpeedAlong = 0;
            const double lateralTolerance = 2.4, lookAhead = 60;
            double playerSpeed = player.SpeedMps, playerHeading = player.Heading; // local functions can't capture 'in' params

            double Check(PathPolyline path, double fromS, double offset, out double along)
            {
                along = 0;
                var (s, _, dist) = path.Project(playerPos);
                if (dist > lateralTolerance) return double.NaN;
                double ahead = path.Closed ? _g.RingForward(fromS, s) : s - fromS;
                if (ahead <= -_p.CarLengthM * 0.5 || ahead > lookAhead) return double.NaN;
                var (_, h) = path.Sample(s);
                along = playerSpeed * Math.Cos(playerHeading - h);
                return offset + ahead;
            }

            double best = double.NaN;
            double a;
            switch (c.Segment)
            {
                case RoundaboutSegment.Entry:
                    best = Check(_g.Entry[c.EntryArm], c.S, 0, out a);
                    if (!double.IsNaN(best)) playerSpeedAlong = a;
                    break;
                case RoundaboutSegment.Ring:
                    double remaining = RingDistance(c) - c.RingTravelled;
                    var (sRing, _, distRing) = _g.Ring.Project(playerPos);
                    if (distRing <= lateralTolerance)
                    {
                        double ahead = _g.RingForward(c.S, sRing);
                        if (ahead <= remaining + 2 && ahead < lookAhead && ahead > 0.1)
                        {
                            best = ahead;
                            var (_, h) = _g.Ring.Sample(sRing);
                            playerSpeedAlong = player.SpeedMps * Math.Cos(player.Heading - h);
                        }
                    }
                    if (double.IsNaN(best))
                    {
                        best = Check(_g.Exit[c.ExitArm], 0, remaining, out a);
                        if (!double.IsNaN(best)) playerSpeedAlong = a;
                    }
                    break;
                case RoundaboutSegment.Exit:
                    best = Check(_g.Exit[c.ExitArm], c.S, 0, out a);
                    if (!double.IsNaN(best)) playerSpeedAlong = a;
                    break;
            }
            return best;
        }

        // ---------- give way (Highway Code rule 185) ----------

        /// <summary>Is there a safe gap in traffic coming from the right (upstream on the ring)?</summary>
        public bool CanEnter(RoundaboutCar c, Vec2 playerPos, in CarState player)
        {
            double merge = _g.EntryMergeS[c.EntryArm];

            foreach (var o in _cars)
            {
                if (o.Segment != RoundaboutSegment.Ring) continue;
                if (!RingVehicleConflicts(o.S, o.SpeedMps, merge, RingDistance(o) - o.RingTravelled)) continue;
                return false;
            }

            // Player on the ring, coming from the right (we don't know where they'll exit: assume they won't).
            var (s, _, dist) = _g.Ring.Project(playerPos);
            if (dist <= RoundaboutGeometry.RingHalfWidthM + 0.5)
            {
                var (_, h) = _g.Ring.Sample(s);
                double along = player.SpeedMps * Math.Cos(player.Heading - h);
                if (RingVehicleConflicts(s, Math.Max(0, along), merge, double.PositiveInfinity)) return false;
            }
            return true;
        }

        private bool RingVehicleConflicts(double vehicleS, double speed, double mergeS, double remainingToExit)
        {
            double upstream = _g.RingForward(vehicleS, mergeS);     // distance until it reaches our merge point
            double justPassed = _g.RingForward(mergeS, vehicleS);   // distance since it passed it
            if (justPassed < _p.MinClearDistanceM) return true;      // still right at the merge point
            if (remainingToExit < upstream) return false;            // it will leave before reaching us
            if (upstream < _p.MinClearDistanceM) return true;
            return upstream / Math.Max(speed, 1.5) < _p.CriticalGapS;
        }

        // ---------- population ----------

        private void SpawnIfNeeded(Vec2 playerPos)
        {
            if (_cars.Count >= _p.TargetCars) return;
            int arm = _rng.Range(0, _g.ArmCount);

            // keep the start of the entry clear, and don't spawn on top of the player
            foreach (var o in _cars)
                if (o.Segment == RoundaboutSegment.Entry && o.EntryArm == arm && o.S < 20) return;
            var (start, _) = _g.Entry[arm].Sample(0);
            if ((start - playerPos).Length < 25) return;

            double r = _rng.NextDouble();
            int exitNo = r < _p.ExitWeights[0] ? 1 : r < _p.ExitWeights[0] + _p.ExitWeights[1] ? 2 : 3;
            int exitArm = (arm + exitNo) % _g.ArmCount;
            bool cyclist = _rng.NextDouble() < _p.CyclistChance;
            AddCar(arm, exitArm, RoundaboutSegment.Entry, 0, cyclist ? _p.CyclistSpeedMps : _p.ApproachSpeedMps, cyclist);
        }
    }
}
