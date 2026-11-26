using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    public sealed class TownCar
    {
        public int Id;
        /// <summary>Segments this vehicle will drive, in order; <see cref="RouteIndex"/> is the current one.</summary>
        public readonly List<int> Route = new List<int>();
        public int RouteIndex;
        /// <summary>Front bumper: distance along the current segment.</summary>
        public double S;
        public double SpeedMps, LastAccelMps2;
        public bool IsCyclist;
        public double LengthM = 4.3;
        public int ColourIndex;
        /// <summary>-1 left, 0 off, +1 right (world directions).</summary>
        public int Indicator;
        public Vec2 Position;
        public double Heading;
        /// <summary>True when the player is the reason this vehicle is slowing or waiting.</summary>
        public bool BrakingForPlayer;
        public int Segment => Route[RouteIndex];
    }

    public sealed class TownTrafficParams
    {
        public int TargetCars = 16;
        public double CarLengthM = 4.3;
        public double CyclistLengthM = 1.8;
        public double CyclistSpeedMps = 5.5;
        public double CyclistChance = 0.10;
        public double PlayerLengthM = 4.3;
        /// <summary>AI keeps a 3-second gap (the Highway Code minimum is 2 s, rule 126).</summary>
        public double TimeHeadwayS = 3.0;
        public double CriticalGapS = 4.0;
        public double ComfortDecelMps2 = 3.0;
        public double LateralAccelMps2 = 2.0;
        public int ColourCount = 6;
        public double WeightAhead = 0.5, WeightNear = 0.25, WeightFar = 0.25;
        /// <summary>
        /// Large networks (road packs): keep traffic around the player. Vehicles appear on lanes between
        /// <see cref="SpawnMinM"/> and <see cref="SpawnMaxM"/> away and are removed beyond <see cref="DespawnM"/>.
        /// </summary>
        public bool SpawnAroundPlayer;
        public double SpawnMinM = 70, SpawnMaxM = 220, DespawnM = 300;
    }

    /// <summary>
    /// AI traffic on a <see cref="LaneGraph"/>: IDM car-following along lane paths; stops for red and for amber
    /// (unless too close to stop); gives way at give-way lines and when turning across oncoming traffic; waits for
    /// pedestrians on or waiting at crossings (Highway Code rules 195, H2); never enters a yellow box unless its
    /// exit is clear (rule 174); signals before turning. 10% cyclists by default.
    /// </summary>
    public sealed class TownTraffic
    {
        private readonly LaneGraph _g;
        private readonly ISignalPlan? _signals;
        private readonly TownTrafficParams _p;
        private readonly DeterministicRandom _rng;
        private readonly List<TownCar> _cars = new List<TownCar>();
        private readonly List<int> _sources = new List<int>();
        private readonly List<TownCar> _queue = new List<TownCar>();
        private int _nextId;
        private double _time;

        public TownTraffic(LaneGraph graph, ISignalPlan? signals, TownTrafficParams p, ulong seed)
        {
            _g = graph;
            _signals = signals;
            _p = p;
            _rng = new DeterministicRandom(seed);
            foreach (var s in graph.Segments)
                if (s.Kind == SegmentKind.Lane && s.Prev.Count == 0) _sources.Add(s.Id);
        }

        public LaneGraph Graph => _g;
        public TownTrafficParams Params => _p;
        public IReadOnlyList<TownCar> Cars => _cars;
        /// <summary>Pedestrians the traffic must give way to (optional).</summary>
        public TownPedestrians? Pedestrians { get; set; }

        public void Reset() => _cars.Clear();

        public LightState Light(int group) => _signals?.State(group, _time) ?? LightState.Green;

        public TownCar Add(int segment, double s, double speed, bool cyclist = false, params int[] route)
        {
            var c = new TownCar
            {
                Id = _nextId++,
                IsCyclist = cyclist,
                LengthM = cyclist ? _p.CyclistLengthM : _p.CarLengthM,
                ColourIndex = _rng.Range(0, Math.Max(1, _p.ColourCount)),
                S = s,
                SpeedMps = speed,
            };
            c.Route.Add(segment);
            c.Route.AddRange(route);
            ExtendRoute(c);
            UpdatePose(c);
            _cars.Add(c);
            return c;
        }

        public void Step(double dt, in CarState player, double time, bool spawn = true)
        {
            _time = time;
            var accel = new double[_cars.Count];
            var forPlayer = new bool[_cars.Count];
            for (int i = 0; i < _cars.Count; i++) accel[i] = ComputeAccel(_cars[i], player, out forPlayer[i]);

            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                var c = _cars[i];
                double v = Math.Max(0, c.SpeedMps + accel[i] * dt);
                c.LastAccelMps2 = (v - c.SpeedMps) / dt;
                c.SpeedMps = v;
                c.BrakingForPlayer = forPlayer[i];
                c.S += v * dt;
                bool gone = false;
                while (c.S >= _g.Segments[c.Segment].Length)
                {
                    if (c.RouteIndex + 1 >= c.Route.Count) { gone = true; break; }
                    c.S -= _g.Segments[c.Segment].Length;
                    c.RouteIndex++;
                    ExtendRoute(c);
                }
                if (gone) { _cars.RemoveAt(i); continue; }
                UpdatePose(c);
                c.Indicator = IndicatorFor(c);
            }

            if (spawn) Spawn(player);
        }

        // ---------------- route & pose ----------------

        private void ExtendRoute(TownCar c)
        {
            while (c.Route.Count - c.RouteIndex < 5)
            {
                var last = _g.Segments[c.Route[c.Route.Count - 1]];
                if (last.Next.Count == 0) return;
                c.Route.Add(last.Next.Count == 1 ? last.Next[0] : Choose(last.Next));
            }
        }

        private int Choose(List<int> options)
        {
            double total = 0;
            foreach (int o in options) total += Weight(_g.Segments[o]);
            double r = _rng.NextDouble() * total;
            foreach (int o in options)
            {
                r -= Weight(_g.Segments[o]);
                if (r <= 0) return o;
            }
            return options[options.Count - 1];
        }

        private double Weight(Segment s) => s.Kind != SegmentKind.Connector ? 1
            : s.Turn == TurnKind.Ahead ? _p.WeightAhead : s.Turn == TurnKind.Near ? _p.WeightNear : _p.WeightFar;

        public int SegmentAhead(TownCar c, int k) => c.RouteIndex + k < c.Route.Count ? c.Route[c.RouteIndex + k] : -1;

        private void UpdatePose(TownCar c)
        {
            double s = c.S - c.LengthM / 2;
            int idx = c.RouteIndex;
            while (s < 0 && idx > 0) { idx--; s += _g.Segments[c.Route[idx]].Length; }
            var (pos, h) = _g.Segments[c.Route[idx]].Path.Sample(Math.Max(0, s));
            c.Position = pos;
            c.Heading = h;
        }

        private int IndicatorFor(TownCar c)
        {
            if (c.IsCyclist) return 0;
            var seg = _g.Segments[c.Segment];
            Segment? turn = null;
            if (seg.Kind == SegmentKind.Connector && c.S < seg.Length * 0.7) turn = seg;
            else if (seg.Kind == SegmentKind.Lane && seg.Length - c.S < 40)
            {
                int n = SegmentAhead(c, 1);
                if (n >= 0 && _g.Segments[n].Kind == SegmentKind.Connector && _g.Segments[n].Node >= 0 && seg.Next.Count > 1) turn = _g.Segments[n];
            }
            if (turn == null || turn.Turn == TurnKind.Ahead) return 0;
            bool left = (turn.Turn == TurnKind.Near) == _g.LeftHandTraffic;
            return left ? -1 : 1;
        }

        // ---------------- longitudinal ----------------

        private double ComputeAccel(TownCar c, in CarState player, out bool forPlayer)
        {
            double desired = DesiredSpeed(c);
            double gap = double.PositiveInfinity, leaderSpeed = c.SpeedMps;
            bool playerIsLimit = false;

            void Consider(double g, double speed, bool isPlayer)
            {
                if (g < gap) { gap = Math.Max(0, g); leaderSpeed = speed; playerIsLimit = isPlayer; }
            }

            // Vehicles ahead along the route (an exact tie is broken by id so two cars never ignore each other).
            foreach (var o in _cars)
            {
                if (o == c) continue;
                double d = DistanceAlong(c, o.Segment, o.S);
                if (!double.IsNaN(d) && (d > 0 || (d == 0 && o.Id < c.Id)) && d < 120) Consider(d - o.LengthM, o.SpeedMps, false);
            }

            // Zipper merge: on a path that merges into another (lane drops, slip roads), whoever is closer to the
            // merge point goes first; treat them as the vehicle in front.
            for (int k = 0; k < 3; k++)
            {
                int sid = SegmentAhead(c, k);
                if (sid < 0) break;
                var conn = _g.Segments[sid];
                if (conn.Kind != SegmentKind.Connector) continue;
                double myToEnd = DistanceAlong(c, sid, conn.Length);
                if (double.IsNaN(myToEnd) || myToEnd > 150) continue;
                foreach (var cp in conn.Conflicts)
                {
                    if (!cp.Merge) continue;
                    var other = _g.Segments[cp.Other];
                    // Only lane drops on the same road; elsewhere give-way priority decides who goes first.
                    int g1 = _g.Segments[conn.FromLane].Group, g2 = _g.Segments[other.FromLane].Group;
                    if (g1 < 0 || g1 != g2) continue;
                    foreach (var o in _cars)
                    {
                        if (o == c) continue;
                        double theirToEnd = double.NaN;
                        if (o.Segment == other.Id) theirToEnd = other.Length - o.S;
                        else if (o.Segment == other.FromLane && SegmentAhead(o, 1) == other.Id) theirToEnd = _g.Segments[other.FromLane].Length - o.S + other.Length;
                        if (double.IsNaN(theirToEnd)) continue;
                        if (theirToEnd < myToEnd || (theirToEnd == myToEnd && o.Id < c.Id))
                            Consider(myToEnd - theirToEnd - o.LengthM, o.SpeedMps, false);
                    }
                }
            }

            // Vehicles that just turned off our lane onto another path still fill the shared space for a few metres.
            for (int k = 0; k < 2; k++)
            {
                int sid = SegmentAhead(c, k);
                if (sid < 0) break;
                var lane = _g.Segments[sid];
                if (lane.Kind != SegmentKind.Lane || lane.Next.Count < 2) continue;
                double toEnd = DistanceAlong(c, sid, lane.Length);
                if (double.IsNaN(toEnd) || toEnd > 80) continue;
                int mine = SegmentAhead(c, k + 1);
                foreach (var o in _cars)
                {
                    if (o == c || o.Segment == mine || o.RouteIndex == 0) continue;
                    var os = _g.Segments[o.Segment];
                    if (os.Kind == SegmentKind.Connector && os.FromLane == sid && o.S - o.LengthM < 6.0)
                        Consider(toEnd + o.S - o.LengthM, o.SpeedMps, false);
                }
            }

            // The player, if on our path.
            var pp = new Vec2(player.X, player.Z);
            for (int k = 0; k < 3; k++)
            {
                int sid = SegmentAhead(c, k);
                if (sid < 0) break;
                var seg = _g.Segments[sid];
                var (ps, _, dist) = seg.Path.Project(pp);
                if (dist > 1.9 || (k > 0 && ps < 0.01 && dist > 1.0)) continue;
                double d = DistanceAlong(c, sid, ps);
                if (double.IsNaN(d) || d < -1.5 || d > 70) continue;
                var (_, h) = seg.Path.Sample(ps);
                double along = player.SpeedMps * Math.Cos(player.Heading - h);
                Consider(d - _p.PlayerLengthM / 2, Math.Max(0, along), true);
                break;
            }

            // Anyone physically in the way where another path crosses ours (a safety net if priority went wrong).
            for (int k = 0; k < 3; k++)
            {
                int sid = SegmentAhead(c, k);
                if (sid < 0) break;
                var seg = _g.Segments[sid];
                if (seg.Kind != SegmentKind.Connector) continue;
                foreach (var cp in seg.Conflicts)
                {
                    if (cp.Merge) continue;
                    double toCp = DistanceAlong(c, sid, cp.S);
                    if (double.IsNaN(toCp) || toCp < 0.5 || toCp > 40) continue;
                    foreach (var o in _cars)
                        if (o != c && o.Segment == cp.Other && o.S > cp.OtherS - 2.0 && o.S - o.LengthM < cp.OtherS + 2.0)
                            Consider(toCp - 2.5, 0, false);
                }
            }

            // Stop / give-way line at the end of the current lane.
            var cur = _g.Segments[c.Segment];
            int nextId = SegmentAhead(c, 1);
            if (cur.Kind == SegmentKind.Lane && nextId >= 0 && _g.Segments[nextId].Kind == SegmentKind.Connector && cur.EndNode >= 0)
            {
                var conn = _g.Segments[nextId];
                double stopS = cur.EndControl == LineControl.Signals && !c.IsCyclist ? cur.StopLineS : cur.Length;
                double toLine = stopS - c.S;
                if (toLine > -0.3 && toLine < 120)
                {
                    bool hold = false, holdForPlayer = false;
                    if (cur.EndControl == LineControl.Signals)
                    {
                        var light = Light(cur.SignalGroup);
                        if (light == LightState.Red || light == LightState.RedAmber) hold = true;
                        else if (light == LightState.Amber)
                            hold = toLine >= c.SpeedMps * c.SpeedMps / (2 * _p.ComfortDecelMps2);
                    }
                    double lookAhead = System.Math.Max(35, c.SpeedMps * c.SpeedMps / (2 * _p.ComfortDecelMps2) + 10);
                    if (!hold && toLine < lookAhead)
                    {
                        if (!CanProceed(c, conn, player, out holdForPlayer)) hold = true;
                        else if (!ExitClear(c, conn)) hold = true;
                    }
                    if (hold) Consider(toLine + 0.3, 0, holdForPlayer);
                }
            }

            // People on (or waiting at) crossings ahead.
            if (Pedestrians != null)
            {
                double offset = 0;
                for (int k = 0; k < 3; k++)
                {
                    int sid = SegmentAhead(c, k);
                    if (sid < 0) break;
                    var seg = _g.Segments[sid];
                    double base0 = k == 0 ? -c.S : offset;
                    foreach (var (cross, from, _) in seg.Crossings)
                    {
                        double toZone = base0 + from;
                        if (toZone < 0.5 || toZone > 60) continue;
                        bool someoneOn = Pedestrians.OnCrossing(cross);
                        bool waiting = Pedestrians.Waiting(cross) && _g.CrossingAreas[cross].Kind != CrossingKind.Other;
                        bool canStop = toZone - 1.5 >= c.SpeedMps * c.SpeedMps / (2 * _p.ComfortDecelMps2) || c.SpeedMps < 3;
                        if (someoneOn || (waiting && canStop)) Consider(toZone - 1.5, 0, false);
                    }
                    offset = (k == 0 ? seg.Length - c.S : offset + seg.Length);
                }
            }

            // Don't stop on a crossing in a queue (rule 192): if where we'd stop leaves us on it, stop before it.
            if (!double.IsPositiveInfinity(gap) && leaderSpeed < 2.0)
            {
                double offset = 0;
                for (int k = 0; k < 3; k++)
                {
                    int sid = SegmentAhead(c, k);
                    if (sid < 0) break;
                    var seg = _g.Segments[sid];
                    double base0 = k == 0 ? -c.S : offset;
                    foreach (var (_, from, to) in seg.Crossings)
                    {
                        double toZone = base0 + from, zoneEnd = base0 + to;
                        if (toZone < 0.5) continue;
                        if (gap > toZone - 1.0 && gap < zoneEnd + c.LengthM + 0.5) { gap = toZone - 1.0; leaderSpeed = 0; }
                    }
                    offset = k == 0 ? seg.Length - c.S : offset + seg.Length;
                }
            }

            forPlayer = playerIsLimit;
            return Idm.Accel(c.SpeedMps, desired, gap, leaderSpeed, headway: _p.TimeHeadwayS, aMax: 1.6, bComfort: 2.2);
        }

        private double DesiredSpeed(TownCar c)
        {
            var seg = _g.Segments[c.Segment];
            double limit = (c.IsCyclist ? _p.CyclistSpeedMps : seg.SpeedLimitMph / 2.236936 * 0.95);
            double v = Math.Min(limit, CurveSpeed(seg));
            // Slow down in time for the next tight segment.
            double offset = seg.Length - c.S;
            for (int k = 1; k < 3; k++)
            {
                int sid = SegmentAhead(c, k);
                if (sid < 0) break;
                var next = _g.Segments[sid];
                double vNext = Math.Min(c.IsCyclist ? _p.CyclistSpeedMps : next.SpeedLimitMph / 2.236936 * 0.95, CurveSpeed(next));
                v = Math.Min(v, Math.Sqrt(vNext * vNext + 2 * 1.5 * Math.Max(0, offset)));
                offset += next.Length;
            }
            return Math.Max(1.0, v);
        }

        private double CurveSpeed(Segment s)
        {
            if (s.Kind != SegmentKind.Connector || s.Turn == TurnKind.Ahead) return double.PositiveInfinity;
            var (_, h0) = s.Path.Sample(0);
            var (_, h1) = s.Path.Sample(s.Length);
            double turn = Math.Abs(LaneGraph.Angle(h1 - h0));
            double radius = s.Length / Math.Max(0.1, turn);
            return Math.Max(3.0, Math.Sqrt(_p.LateralAccelMps2 * radius));
        }

        /// <summary>Front-to-front distance along c's route to a point on a segment, or NaN if not on the route ahead.</summary>
        public double DistanceAlong(TownCar c, int segment, double s)
        {
            double offset = 0;
            for (int k = 0; k < 4; k++)
            {
                int sid = SegmentAhead(c, k);
                if (sid < 0) break;
                if (sid == segment) return k == 0 ? s - c.S : offset + s;
                offset += k == 0 ? _g.Segments[sid].Length - c.S : _g.Segments[sid].Length;
            }
            return double.NaN;
        }

        // ---------------- priority ----------------

        /// <summary>May a vehicle start the connector now, or must it give way to someone with priority?</summary>
        public bool CanProceed(TownCar c, Segment conn, in CarState player, out bool becauseOfPlayer)
        {
            becauseOfPlayer = false;
            var pp = new Vec2(player.X, player.Z);
            double toLine = Math.Max(0, _g.Segments[c.Segment].Length - c.S);
            foreach (var cp in conn.Conflicts)
            {
                var other = _g.Segments[cp.Other];
                if (!_g.MustYield(conn, other)) continue;
                var lane = _g.Segments[other.FromLane];
                // Time we need to get clear of the conflict point (merging: and leave them a safe gap behind us).
                double need = TimeToCover(toLine + cp.S + c.LengthM + 1.0, c.SpeedMps, Math.Min(CurveSpeed(conn), conn.SpeedLimitMph / 2.236936))
                              + (cp.Merge ? _p.TimeHeadwayS : 1.5);
                double gapNeeded = Math.Max(_p.CriticalGapS, need);

                foreach (var o in _cars)
                    if (o != c && o.Segment == other.Id && o.S - o.LengthM < cp.OtherS + 1.0) return false;

                // Cars queuing on the priority lane, front first. A stationary car that isn't coming our way
                // blocks everyone behind it, so they can't reach the conflict point yet.
                _queue.Clear();
                foreach (var o in _cars) if (o != c && o.Segment == lane.Id) _queue.Add(o);
                _queue.Sort((x, y) => y.S.CompareTo(x.S));
                foreach (var o in _queue)
                {
                    bool coming = SegmentAhead(o, 1) == other.Id;
                    if (coming)
                    {
                        if (lane.EndControl == LineControl.Signals && Light(lane.SignalGroup) != LightState.Green && o.S < lane.StopLineS - 1) break;
                        double dist = lane.Length - o.S + cp.OtherS;
                        double vMax = lane.SpeedLimitMph / 2.236936;
                        if (dist < 8 || TimeToCover(dist, o.SpeedMps, vMax) < gapNeeded) return false;
                    }
                    if (o.SpeedMps < 0.5) break;
                }

                // The player, if on the priority path.
                var (cs, _, cd) = other.Path.Project(pp);
                if (cd < 2.5 && cs < cp.OtherS + 3.5 && cs > 0.01)
                {
                    double dist = cp.OtherS - cs;
                    if (dist < 8 || PlayerArrival(dist, player.SpeedMps) < gapNeeded) { becauseOfPlayer = true; return false; }
                }
                var (ls, _, ld) = lane.Path.Project(pp);
                if (ld < 2.5 && Math.Cos(player.Heading - lane.Path.Sample(ls).heading) > 0.7 && ls < lane.Length - 0.01)
                {
                    if (lane.EndControl == LineControl.Signals && Light(lane.SignalGroup) != LightState.Green && ls < lane.StopLineS - 5) continue;
                    double dist = lane.Length - ls + cp.OtherS;
                    if (dist < 70 && (dist < 10 || PlayerArrival(dist, player.SpeedMps) < gapNeeded)) { becauseOfPlayer = true; return false; }
                }
            }
            return true;
        }

        /// <summary>Earliest the player could arrive (they may speed up briskly).</summary>
        private static double PlayerArrival(double dist, double v) => Math.Min(dist / Math.Max(v, 0.1), TimeToCover(dist, v, 15, 3.0));

        /// <summary>Seconds to cover a distance from speed v, accelerating gently up to vMax.</summary>
        private static double TimeToCover(double dist, double v, double vMax, double a = 1.6)
        {
            vMax = Math.Max(vMax, 1.0);
            v = Math.Min(v, vMax);
            double accelDist = (vMax * vMax - v * v) / (2 * a);
            if (dist <= accelDist) return (-v + Math.Sqrt(v * v + 2 * a * dist)) / a;
            return (vMax - v) / a + (dist - accelDist) / vMax;
        }

        /// <summary>Yellow box (rule 174): only enter if there's room on the exit lane.</summary>
        private bool ExitClear(TownCar c, Segment conn)
        {
            // Turning into a road where people are crossing (or about to): wait at the line, not in the junction.
            if (Pedestrians != null && conn.Turn != TurnKind.Ahead)
                foreach (var (cross, from, _) in _g.Segments[conn.ToLane].Crossings)
                    if (from < 15 && (Pedestrians.OnCrossing(cross) || Pedestrians.Waiting(cross))) return false;
            bool box = _g.Nodes[conn.Node].Box != null;
            if (!box && conn.Turn == TurnKind.Ahead) return true;
            double need = c.LengthM + 2.0;
            foreach (var o in _cars)
            {
                if (o == c) continue;
                if (o.Segment == conn.ToLane && o.S - o.LengthM < need && o.SpeedMps < 3.0) return false;
                // Someone in the junction heading for the same lane counts as already there.
                if (o.Segment != c.Segment && _g.Segments[o.Segment].Kind == SegmentKind.Connector && _g.Segments[o.Segment].ToLane == conn.ToLane)
                {
                    foreach (var o2 in _cars)
                        if (o2.Segment == conn.ToLane && o2.S - o2.LengthM < need + o.LengthM && o2.SpeedMps < 3.0) return false;
                }
            }
            return true;
        }

        // ---------------- population ----------------

        private readonly List<int> _anyLane = new List<int>();

        private void SpawnAround(in CarState player)
        {
            var pp = new Vec2(player.X, player.Z);
            for (int i = _cars.Count - 1; i >= 0; i--)
                if ((_cars[i].Position - pp).Length > _p.DespawnM) _cars.RemoveAt(i);
            if (_cars.Count >= _p.TargetCars) return;
            if (_anyLane.Count == 0)
                foreach (var seg in _g.Segments)
                    if (seg.Kind == SegmentKind.Lane && seg.Length > 25 && seg.Next.Count > 0) _anyLane.Add(seg.Id);
            if (_anyLane.Count == 0) return;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var lane = _g.Segments[_anyLane[_rng.Range(0, _anyLane.Count)]];
                double s = lane.Length * 0.5;
                var (pos, _) = lane.Path.Sample(s);
                double d = (pos - pp).Length;
                if (d < _p.SpawnMinM || d > _p.SpawnMaxM) continue;
                // Cyclists aren't allowed on motorways (Highway Code rule 253); keep them off 50 mph+ roads.
                bool cyclist = lane.SpeedLimitMph < 50 && _rng.NextDouble() < _p.CyclistChance;
                double speed = cyclist ? _p.CyclistSpeedMps : lane.SpeedLimitMph / 2.236936 * 0.8;
                // Room to settle in: nobody within a 3-second gap (+20 m) of where it appears.
                bool free = true;
                foreach (var o in _cars) if ((o.Position - pos).Length < speed * _p.TimeHeadwayS + 20) free = false;
                if (!free) continue;
                Add(lane.Id, s, speed, cyclist);
                return;
            }
        }

        private void Spawn(in CarState player)
        {
            if (_p.SpawnAroundPlayer) { SpawnAround(player); return; }
            if (_cars.Count >= _p.TargetCars || _sources.Count == 0) return;
            int lane = _sources[_rng.Range(0, _sources.Count)];
            bool cyclist = _g.Segments[lane].SpeedLimitMph < 50 && _rng.NextDouble() < _p.CyclistChance;
            foreach (var o in _cars)
                if (o.Segment == lane && o.S < 25) return;
            var (start, _) = _g.Segments[lane].Path.Sample(0);
            if ((start - new Vec2(player.X, player.Z)).Length < 35) return;
            var seg = _g.Segments[lane];
            Add(lane, 0, cyclist ? _p.CyclistSpeedMps : seg.SpeedLimitMph / 2.236936 * 0.9, cyclist);
        }
    }

    public enum PedState { Walking, Waiting, Crossing }

    public sealed class Pedestrian
    {
        public int Id;
        public int Route;
        public double S;
        public double SpeedMps;
        public PedState State;
        /// <summary>Crossing the person is waiting at or walking on, or -1.</summary>
        public int Crossing = -1;
        public Vec2 Position;
        public double Heading;
        public int ColourIndex;
    }

    /// <summary>
    /// People walking the footways. At a crossing (zebra or side-road mouth) they wait at the kerb until approaching
    /// vehicles are far enough away or have stopped for them, then cross. Deterministic for a given seed.
    /// </summary>
    public sealed class TownPedestrians
    {
        private readonly LaneGraph _g;
        private readonly List<PathPolyline> _routes;
        private readonly List<List<(int crossing, double sFrom, double sTo)>> _routeCrossings = new List<List<(int, double, double)>>();
        private readonly DeterministicRandom _rng;
        private readonly List<Pedestrian> _peds = new List<Pedestrian>();
        private readonly int _target;
        private int _nextId;

        public TownPedestrians(LaneGraph graph, List<PathPolyline> routes, int targetCount, ulong seed)
        {
            _g = graph;
            _routes = routes;
            _target = targetCount;
            _rng = new DeterministicRandom(seed);
            foreach (var r in routes)
            {
                var list = new List<(int, double, double)>();
                foreach (var c in graph.CrossingAreas)
                {
                    double from = double.NaN, to = double.NaN;
                    for (double s = 0; s <= r.Length; s += 0.1)
                    {
                        if (!Geo2.PointInPolygon(r.Sample(s).pos, c.Polygon)) continue;
                        if (double.IsNaN(from)) from = s;
                        to = s;
                    }
                    if (!double.IsNaN(from)) list.Add((c.Id, from, to));
                }
                list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                _routeCrossings.Add(list);
            }
        }

        public IReadOnlyList<Pedestrian> People => _peds;
        /// <summary>How long a person waits at the kerb before deciding (looking both ways).</summary>
        public double LookTimeS = 0.6;
        public double SafeTimeS = 4.5;

        public void Reset() => _peds.Clear();

        public bool OnCrossing(int crossing)
        {
            foreach (var p in _peds) if (p.State == PedState.Crossing && p.Crossing == crossing) return true;
            return false;
        }

        public bool Waiting(int crossing)
        {
            foreach (var p in _peds) if (p.State == PedState.Waiting && p.Crossing == crossing) return true;
            return false;
        }

        /// <summary>
        /// Puts someone at the kerb of a crossing (scenarios): waiting there, or already stepping out onto it.
        /// Returns false if no walking route uses that crossing.
        /// </summary>
        public bool AddAtCrossing(int crossing, bool stepOut)
        {
            for (int r = 0; r < _routes.Count; r++)
                foreach (var (c, from, _) in _routeCrossings[r])
                    if (c == crossing)
                    {
                        var p = Add(r, Math.Max(0, from - 0.35), 1.35);
                        if (stepOut) { p.S = from + 0.1; p.State = PedState.Crossing; p.Crossing = crossing; }
                        else { p.State = PedState.Waiting; p.Crossing = crossing; _waited[p.Id] = -3.0; } // waits a few seconds at least
                        Pose(p);
                        return true;
                    }
            return false;
        }

        public Pedestrian Add(int route, double s, double speed = 1.4)
        {
            var p = new Pedestrian { Id = _nextId++, Route = route, S = s, SpeedMps = speed, ColourIndex = _rng.Range(0, 6) };
            Pose(p);
            _peds.Add(p);
            return p;
        }

        private readonly Dictionary<int, double> _waited = new Dictionary<int, double>();

        public void Step(double dt, TownTraffic? traffic, in CarState player, bool spawn = true)
        {
            for (int i = _peds.Count - 1; i >= 0; i--)
            {
                var p = _peds[i];
                var route = _routes[p.Route];
                var crossings = _routeCrossings[p.Route];

                if (p.State == PedState.Walking)
                {
                    foreach (var (cross, from, _) in crossings)
                    {
                        if (p.S > from - 0.3) continue;
                        if (p.S + p.SpeedMps * dt >= from - 0.3)
                        {
                            p.S = from - 0.3;
                            p.State = PedState.Waiting;
                            p.Crossing = cross;
                            _waited[p.Id] = 0;
                        }
                        break;
                    }
                    if (p.State == PedState.Walking) p.S += p.SpeedMps * dt;
                }
                else if (p.State == PedState.Waiting)
                {
                    _waited[p.Id] = _waited.TryGetValue(p.Id, out var w) ? w + dt : dt;
                    if (_waited[p.Id] >= LookTimeS && Clear(p.Crossing, traffic, player))
                    {
                        p.State = PedState.Crossing;
                        _waited.Remove(p.Id);
                    }
                }
                else
                {
                    p.S += p.SpeedMps * dt;
                    foreach (var (cross, _, to) in crossings)
                        if (cross == p.Crossing && p.S > to + 0.2) { p.State = PedState.Walking; p.Crossing = -1; }
                }

                if (p.S >= route.Length) { _peds.RemoveAt(i); continue; }
                Pose(p);
            }

            if (spawn && _peds.Count < _target && _routes.Count > 0)
            {
                int r = _rng.Range(0, _routes.Count);
                bool free = true;
                foreach (var o in _peds) if (o.Route == r && o.S < 4) free = false;
                if (free) Add(r, 0, 1.2 + _rng.NextDouble() * 0.4);
            }
        }

        private void Pose(Pedestrian p)
        {
            var (pos, h) = _routes[p.Route].Sample(p.S);
            p.Position = pos;
            p.Heading = h;
        }

        /// <summary>Is it safe to step out: no vehicle on the crossing, and every approaching one is far away or has stopped?</summary>
        public bool Clear(int crossing, TownTraffic? traffic, in CarState player)
        {
            if (traffic != null)
                foreach (var c in traffic.Cars)
                {
                    double d = DistanceToCrossing(traffic, c, crossing, out double zoneLen);
                    if (double.IsNaN(d)) continue;
                    if (d < 0 && d > -(zoneLen + c.LengthM)) return false;       // on the crossing now
                    if (d < 0) continue;
                    if (c.SpeedMps < 1.0 && d < 15) continue;                      // stopped for us
                    if (d < 3 || d / Math.Max(c.SpeedMps, 0.01) < SafeTimeS) return false;
                }

            double pd = PlayerDistanceToCrossing(crossing, player, out double len);
            if (!double.IsNaN(pd))
            {
                if (pd < 0 && pd > -(len + 4.3)) return false;
                if (pd >= 0)
                {
                    if (player.SpeedMps < 1.0 && pd < 15) return true;
                    if (pd < 3 || pd / Math.Max(player.SpeedMps, 0.01) < SafeTimeS) return false;
                }
            }
            return true;
        }

        private double DistanceToCrossing(TownTraffic traffic, TownCar c, int crossing, out double zoneLen)
        {
            zoneLen = 0;
            for (int k = 0; k < 3; k++)
            {
                int sid = traffic.SegmentAhead(c, k);
                if (sid < 0) break;
                foreach (var (cross, from, to) in _g.Segments[sid].Crossings)
                    if (cross == crossing)
                    {
                        zoneLen = to - from;
                        return traffic.DistanceAlong(c, sid, from);
                    }
            }
            return double.NaN;
        }

        /// <summary>Distance from the player's front bumper to the crossing along the lane they're on (or the one before), or NaN.</summary>
        public double PlayerDistanceToCrossing(int crossing, in CarState player, out double zoneLen)
        {
            zoneLen = 0;
            var pp = new Vec2(player.X, player.Z);
            double best = double.NaN;
            foreach (var seg in _g.Segments)
                foreach (var (cross, from, to) in seg.Crossings)
                {
                    if (cross != crossing) continue;
                    var (s, _, d) = seg.Path.Project(pp);
                    if (d < 2.6 && Math.Cos(player.Heading - seg.Path.Sample(s).heading) > 0.5 && s > 0.01 && s < seg.Length - 0.01)
                    {
                        double dist = from - (s + 2.15);
                        if (double.IsNaN(best) || Math.Abs(dist) < Math.Abs(best)) { best = dist; zoneLen = to - from; }
                    }
                    foreach (int prev in seg.Prev)
                    {
                        var ps = _g.Segments[prev];
                        var (s2, _, d2) = ps.Path.Project(pp);
                        if (d2 < 2.6 && Math.Cos(player.Heading - ps.Path.Sample(s2).heading) > 0.5 && s2 < ps.Length - 0.01)
                        {
                            double dist = ps.Length - (s2 + 2.15) + from;
                            if (double.IsNaN(best) || Math.Abs(dist) < Math.Abs(best)) { best = dist; zoneLen = to - from; }
                        }
                    }
                }
            return best;
        }
    }
}
