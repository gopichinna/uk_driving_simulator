using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    public enum Manoeuvre { None, TurnLeft, TurnRight, StraightOn, Arrive }

    /// <summary>
    /// Sat-nav over a lane graph, as used in the independent-driving part of the practical test:
    /// the shortest legal path (one-way streets and turn bans are already in the graph) to a destination,
    /// re-planned whenever the driver leaves it, with the next instruction and its distance.
    /// </summary>
    public sealed class SatNav
    {
        private readonly LaneGraph _g;
        private readonly List<int> _path = new List<int>();
        private readonly List<Segment> _near = new List<Segment>();
        private int _at = -1;
        private int _target = -1;
        private Vec2 _destination;
        private double _offRouteSince = double.NaN;

        public SatNav(LaneGraph graph) => _g = graph;

        public bool HasDestination => _target != -1;
        public bool Arrived { get; private set; }
        public int Replans { get; private set; }
        public IReadOnlyList<int> Path => _path;
        public Vec2 Destination => _destination;

        private readonly HashSet<int> _targets = new HashSet<int>();

        public void SetDestination(Vec2 destination, in CarState player)
        {
            _destination = destination;
            _targets.Clear();
            _g.Near(destination, 25, _near);
            foreach (var s in _near)
                if (s.Kind == SegmentKind.Lane && s.Path.Project(destination).distance < 20) _targets.Add(s.Id);
            _target = _targets.Count > 0 ? -2 : -1; // -2: "any of the targets"
            Arrived = false;
            Plan(CurrentSegment(player));
        }

        /// <summary>Call every frame (cheap unless a re-plan is needed).</summary>
        public void Update(in CarState player, double time)
        {
            if (_target == -1 || Arrived) return;
            var p = new Vec2(player.X, player.Z);
            if ((p - _destination).Length < 25) { Arrived = true; return; }

            int seg = CurrentSegment(player);
            if (seg < 0) return;
            int idx = _path.IndexOf(seg, Math.Max(0, _at));
            if (idx >= 0) { _at = idx; _offRouteSince = double.NaN; return; }
            if (double.IsNaN(_offRouteSince)) _offRouteSince = time;
            if (time - _offRouteSince > 1.0) { Plan(seg); _offRouteSince = double.NaN; Replans++; }
        }

        /// <summary>The next turn on the path (straight-on through junctions is skipped) and how far away it is.</summary>
        public (Manoeuvre what, double distanceM) Next(in CarState player)
        {
            if (Arrived) return (Manoeuvre.Arrive, 0);
            if (_path.Count == 0 || _at < 0) return (Manoeuvre.None, 0);
            var p = new Vec2(player.X, player.Z);
            var first = _g.Segments[_path[_at]];
            var (s, _, _) = first.Path.Project(p);
            double dist = first.Length - s;
            for (int i = _at + 1; i < _path.Count; i++)
            {
                var seg = _g.Segments[_path[i]];
                if (seg.Kind == SegmentKind.Connector && _g.Segments[seg.FromLane].Next.Count > 1 && seg.Turn != TurnKind.Ahead)
                {
                    bool left = (seg.Turn == TurnKind.Near) == _g.LeftHandTraffic;
                    return (left ? Manoeuvre.TurnLeft : Manoeuvre.TurnRight, dist);
                }
                dist += seg.Length;
            }
            return (Manoeuvre.Arrive, dist + (p - _destination).Length * 0);
        }

        public static string Describe(Manoeuvre m, double distanceM)
        {
            string when = distanceM < 30 ? "Now" : $"In {Math.Round(distanceM / 10) * 10:0} m";
            return m switch
            {
                Manoeuvre.TurnLeft => $"{when}, turn left",
                Manoeuvre.TurnRight => $"{when}, turn right",
                Manoeuvre.StraightOn => $"{when}, go straight on",
                Manoeuvre.Arrive => distanceM < 30 ? "You have arrived" : $"Follow the road for {Math.Round(distanceM / 10) * 10:0} m",
                _ => "",
            };
        }

        private int CurrentSegment(in CarState player)
        {
            var p = new Vec2(player.X, player.Z);
            _g.Near(p, 6, _near);
            int best = -1;
            double bestD = 4.0;
            foreach (var s in _near)
            {
                var (ss, _, d) = s.Path.Project(p);
                if (d >= bestD) continue;
                if (Math.Cos(player.Heading - s.Path.Sample(ss).heading) < 0.3) continue;
                if (s.Kind == SegmentKind.Connector) d += 0.5;
                if (d < bestD) { bestD = d; best = s.Id; }
            }
            return best;
        }

        /// <summary>Dijkstra over segments (cost = length; changing to the next lane of the same road costs a little extra).</summary>
        private void Plan(int from)
        {
            _path.Clear();
            _at = -1;
            if (from < 0 || _targets.Count == 0) return;
            if (_groupLanes == null)
            {
                _groupLanes = new Dictionary<int, List<int>>();
                foreach (var s in _g.Segments)
                    if (s.Group >= 0)
                    {
                        if (!_groupLanes.TryGetValue(s.Group, out var l)) _groupLanes[s.Group] = l = new List<int>();
                        l.Add(s.Id);
                    }
            }
            int n = _g.Segments.Count;
            var dist = new double[n];
            var prev = new int[n];
            for (int i = 0; i < n; i++) { dist[i] = double.PositiveInfinity; prev[i] = -1; }
            dist[from] = 0;
            var queue = new SortedSet<(double d, int id)> { (0, from) };
            int reached = -1;
            void Relax(int u, int v, double nd)
            {
                if (nd >= dist[v]) return;
                if (!double.IsPositiveInfinity(dist[v])) queue.Remove((dist[v], v));
                dist[v] = nd;
                prev[v] = u;
                queue.Add((nd, v));
            }
            while (queue.Count > 0)
            {
                var (d, u) = queue.Min;
                queue.Remove(queue.Min);
                if (_targets.Contains(u)) { reached = u; break; }
                var seg = _g.Segments[u];
                foreach (int v in seg.Next) Relax(u, v, d + seg.Length);
                if (seg.Group >= 0 && _groupLanes.TryGetValue(seg.Group, out var side))
                    foreach (int v in side) if (v != u) Relax(u, v, d + 25);
            }
            if (reached < 0) return;
            for (int v = reached; v >= 0; v = prev[v]) _path.Add(v);
            _path.Reverse();
            _at = 0;
        }

        private Dictionary<int, List<int>>? _groupLanes;
    }

    /// <summary>
    /// A mock practical test: about 35 minutes of driving (GOV.UK, "What happens during the test"), most of it
    /// independent driving to sat-nav destinations at least <see cref="MinLegM"/> away. Deterministic per seed.
    /// </summary>
    public sealed class MockTestPlan
    {
        private readonly LaneGraph _g;
        private readonly DeterministicRandom _rng;
        private readonly List<int> _lanes = new List<int>();

        public MockTestPlan(LaneGraph g, ulong seed)
        {
            _g = g;
            _rng = new DeterministicRandom(seed);
            foreach (var s in g.Segments) if (s.Kind == SegmentKind.Lane && s.Length > 30 && s.Next.Count > 0 && s.Prev.Count > 0) _lanes.Add(s.Id);
        }

        public double DurationS = 35 * 60;
        public double MinLegM = 350, MaxLegM = 900;
        public int Legs { get; private set; }

        public Vec2 NextDestination(Vec2 from)
        {
            Legs++;
            Vec2 best = from;
            double bestScore = double.MaxValue, mid = (MinLegM + MaxLegM) / 2;
            for (int i = 0; i < 40 && _lanes.Count > 0; i++)
            {
                var seg = _g.Segments[_lanes[_rng.Range(0, _lanes.Count)]];
                var (p, _) = seg.Path.Sample(seg.Length / 2);
                double d = (p - from).Length, score = Math.Abs(d - mid);
                if (d >= MinLegM && d <= MaxLegM) return p;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }
    }
}
