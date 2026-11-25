using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    public enum SegmentKind { Lane, Connector }

    /// <summary>Turn through a junction, country-independent: near = the kerb side (left in the UK), far = across traffic.</summary>
    public enum TurnKind { Ahead, Near, Far }

    /// <summary>What controls the end of a lane where it meets a junction.</summary>
    public enum LineControl { None, GiveWay, Signals }

    /// <summary>A point where a connector crosses or merges with another connector at the same junction.</summary>
    public readonly struct ConflictPoint
    {
        public ConflictPoint(int other, double s, double otherS, bool merge)
        {
            Other = other;
            S = s;
            OtherS = otherS;
            Merge = merge;
        }

        public int Other { get; }
        /// <summary>Distance along this connector.</summary>
        public double S { get; }
        /// <summary>Distance along the other connector.</summary>
        public double OtherS { get; }
        /// <summary>Both connectors end in the same lane.</summary>
        public bool Merge { get; }
    }

    /// <summary>
    /// A drivable path: a lane between junctions, or a connector through a junction.
    /// Lanes end at the junction mouth (the give-way line, or the advanced stop line at signals).
    /// </summary>
    public sealed class Segment
    {
        public int Id;
        public SegmentKind Kind;
        public string Name = "";
        public PathPolyline Path = null!;
        public double SpeedLimitMph = 30;
        public readonly List<int> Next = new List<int>();
        public readonly List<int> Prev = new List<int>();

        // Lanes: what happens at the end.
        public LineControl EndControl;
        public int SignalGroup = -1;
        /// <summary>Where motor vehicles stop (before an advanced stop line box), as distance along the lane.</summary>
        public double StopLineS;
        public int EndNode = -1;

        // Connectors.
        public int Node = -1;
        public TurnKind Turn;
        public int FromLane = -1, ToLane = -1;
        public readonly List<ConflictPoint> Conflicts = new List<ConflictPoint>();

        /// <summary>Pedestrian crossings this segment passes through: crossing id and the distance range along the path.</summary>
        public readonly List<(int crossing, double sFrom, double sTo)> Crossings = new List<(int, double, double)>();

        public double Length => Path.Length;
        public bool HasAdvancedStopLine => EndControl == LineControl.Signals && StopLineS < Path.Length - 0.5;
    }

    public sealed class JunctionNode
    {
        public int Id;
        public string Name = "";
        public Vec2 Centre;
        public bool Signalised;
        /// <summary>Yellow box junction area, or null.</summary>
        public Vec2[]? Box;
    }

    public enum CrossingKind
    {
        Zebra,
        /// <summary>Pedestrians crossing the mouth of a side road (Highway Code rule H2).</summary>
        JunctionMouth,
    }

    public sealed class CrossingArea
    {
        public int Id;
        public CrossingKind Kind;
        public string Name = "";
        /// <summary>Area on the road surface people walk across.</summary>
        public Vec2[] Polygon = Array.Empty<Vec2>();
    }

    /// <summary>
    /// Lane-level road network: lanes, connectors through junctions, junction nodes, crossings and the
    /// conflict points between connectors. Built by hand (town course) or from a road pack.
    /// </summary>
    public sealed class LaneGraph
    {
        public LaneGraph(bool leftHandTraffic = true) => LeftHandTraffic = leftHandTraffic;

        public bool LeftHandTraffic { get; }
        public List<Segment> Segments { get; } = new List<Segment>();
        public List<JunctionNode> Nodes { get; } = new List<JunctionNode>();
        public List<CrossingArea> CrossingAreas { get; } = new List<CrossingArea>();
        /// <summary>Zig-zag areas on the approaches to zebra crossings.</summary>
        public List<Vec2[]> ZigZags { get; } = new List<Vec2[]>();

        public int AddNode(string name, Vec2 centre, bool signalised = false, Vec2[]? box = null)
        {
            Nodes.Add(new JunctionNode { Id = Nodes.Count, Name = name, Centre = centre, Signalised = signalised, Box = box });
            return Nodes.Count - 1;
        }

        public int AddLane(string name, IReadOnlyList<Vec2> points, double speedLimitMph,
            int endNode = -1, LineControl control = LineControl.None, int signalGroup = -1, double advancedBoxDepth = 0)
        {
            var path = new PathPolyline(points);
            var seg = new Segment
            {
                Id = Segments.Count, Kind = SegmentKind.Lane, Name = name, Path = path, SpeedLimitMph = speedLimitMph,
                EndNode = endNode, EndControl = control, SignalGroup = signalGroup,
                StopLineS = Math.Max(0, path.Length - advancedBoxDepth),
            };
            Segments.Add(seg);
            return seg.Id;
        }

        /// <summary>Curved connector from the end of one lane to the start of another, through a junction.</summary>
        public int AddConnector(int fromLane, int toLane, int node)
        {
            Segment a = Segments[fromLane], b = Segments[toLane];
            var (p0, h0) = a.Path.Sample(a.Length);
            var (p3, _) = b.Path.Sample(0);
            var (p3b, h3) = b.Path.Sample(Math.Min(1.0, b.Length));
            h3 = (p3b - p3).Length > 1e-6 ? (p3b - p3).Heading : h3;
            double k = (p3 - p0).Length * 0.45;
            var pts = PathPolyline.Bezier(p0, p0 + Vec2.FromHeading(h0) * k, p3 - Vec2.FromHeading(h3) * k, p3, 16);

            double turn = Angle(h3 - h0);
            TurnKind kind = Math.Abs(turn) < 0.5 ? TurnKind.Ahead
                : (turn < 0) == LeftHandTraffic ? TurnKind.Near : TurnKind.Far; // heading is clockwise-positive: left turn < 0

            var seg = new Segment
            {
                Id = Segments.Count, Kind = SegmentKind.Connector, Name = $"{a.Name}→{b.Name}", Path = new PathPolyline(pts),
                SpeedLimitMph = b.SpeedLimitMph, Node = node, Turn = kind, FromLane = fromLane, ToLane = toLane,
            };
            Segments.Add(seg);
            a.Next.Add(seg.Id);
            seg.Prev.Add(fromLane);
            seg.Next.Add(toLane);
            b.Prev.Add(seg.Id);
            return seg.Id;
        }

        /// <summary>Joins two lanes end to start with no junction (a bend or a change of speed limit).</summary>
        public void Link(int fromLane, int toLane)
        {
            Segments[fromLane].Next.Add(toLane);
            Segments[toLane].Prev.Add(fromLane);
        }

        public int AddCrossing(CrossingKind kind, string name, Vec2[] polygon)
        {
            CrossingAreas.Add(new CrossingArea { Id = CrossingAreas.Count, Kind = kind, Name = name, Polygon = polygon });
            return CrossingAreas.Count - 1;
        }

        /// <summary>Computes conflict points between connectors and which segments pass through which crossings.</summary>
        public void Finalise()
        {
            foreach (var s in Segments) { s.Conflicts.Clear(); s.Crossings.Clear(); }

            var byNode = new Dictionary<int, List<Segment>>();
            foreach (var s in Segments)
                if (s.Kind == SegmentKind.Connector)
                {
                    if (!byNode.TryGetValue(s.Node, out var list)) byNode[s.Node] = list = new List<Segment>();
                    list.Add(s);
                }

            foreach (var list in byNode.Values)
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        Segment a = list[i], b = list[j];
                        if (a.FromLane == b.FromLane) continue; // diverging: plain following handles it
                        if (a.ToLane == b.ToLane)
                        {
                            a.Conflicts.Add(new ConflictPoint(b.Id, a.Length, b.Length, true));
                            b.Conflicts.Add(new ConflictPoint(a.Id, b.Length, a.Length, true));
                        }
                        else if (FirstIntersection(a.Path, b.Path, out double sa, out double sb))
                        {
                            a.Conflicts.Add(new ConflictPoint(b.Id, sa, sb, false));
                            b.Conflicts.Add(new ConflictPoint(a.Id, sb, sa, false));
                        }
                    }

            foreach (var c in CrossingAreas)
                foreach (var s in Segments)
                {
                    double from = double.NaN, to = double.NaN;
                    for (double d = 0; d <= s.Length; d += 0.25)
                    {
                        var (p, _) = s.Path.Sample(d);
                        if (!Geo2.PointInPolygon(p, c.Polygon)) continue;
                        if (double.IsNaN(from)) from = d;
                        to = d;
                    }
                    if (!double.IsNaN(from)) s.Crossings.Add((c.Id, from, to));
                }
        }

        /// <summary>
        /// Must a vehicle taking connector <paramref name="a"/> give way to one taking <paramref name="b"/> where they meet?
        /// Give-way lines yield to the major road; at signals and on the major road, a far turn (right in the UK)
        /// yields to oncoming traffic going ahead or turning near.
        /// </summary>
        public bool MustYield(Segment a, Segment b)
        {
            Segment la = Segments[a.FromLane], lb = Segments[b.FromLane];
            if (la.EndControl == LineControl.Signals || lb.EndControl == LineControl.Signals)
            {
                if (la.SignalGroup != lb.SignalGroup) return false; // never green together
                return a.Turn == TurnKind.Far && b.Turn != TurnKind.Far;
            }
            bool aMinor = la.EndControl == LineControl.GiveWay, bMinor = lb.EndControl == LineControl.GiveWay;
            if (aMinor != bMinor) return aMinor;
            if (a.Turn == TurnKind.Far && b.Turn != TurnKind.Far) return true;
            if (b.Turn == TurnKind.Far && a.Turn != TurnKind.Far) return false;
            return a.Id > b.Id; // same priority (rare): a fixed order avoids deadlock
        }

        /// <summary>Nearest segment to a point (lanes preferred within a tolerance), with distance along it.</summary>
        public Segment? Nearest(Vec2 p, out double s, out double distance, bool lanesOnly = false)
        {
            Segment? best = null;
            s = 0;
            distance = double.MaxValue;
            foreach (var seg in Segments)
            {
                if (lanesOnly && seg.Kind != SegmentKind.Lane) continue;
                var (ss, _, d) = seg.Path.Project(p);
                if (seg.Kind == SegmentKind.Connector) d += 0.3; // prefer lanes where they touch
                if (d < distance) { distance = d; s = ss; best = seg; }
            }
            return best;
        }

        public static double Angle(double a)
        {
            const double twoPi = 2 * Math.PI;
            a %= twoPi;
            if (a > Math.PI) a -= twoPi;
            if (a < -Math.PI) a += twoPi;
            return a;
        }

        private static bool FirstIntersection(PathPolyline a, PathPolyline b, out double sa, out double sb)
        {
            sa = sb = 0;
            var pa = a.Points;
            var pb = b.Points;
            double accA = 0;
            for (int i = 0; i < pa.Count - 1; i++)
            {
                double lenA = (pa[i + 1] - pa[i]).Length;
                double accB = 0;
                for (int j = 0; j < pb.Count - 1; j++)
                {
                    double lenB = (pb[j + 1] - pb[j]).Length;
                    if (Geo2.SegmentIntersect(pa[i], pa[i + 1], pb[j], pb[j + 1], out double ta, out double tb))
                    {
                        sa = accA + ta * lenA;
                        sb = accB + tb * lenB;
                        return true;
                    }
                    accB += lenB;
                }
                accA += lenA;
            }
            return false;
        }
    }

    /// <summary>Small 2D geometry helpers.</summary>
    public static class Geo2
    {
        public static bool PointInPolygon(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vec2 a = poly[i], b = poly[j];
                if ((a.Z > p.Z) != (b.Z > p.Z) && p.X < (b.X - a.X) * (p.Z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
            }
            return inside;
        }

        /// <summary>Do segments p1-p2 and q1-q2 cross? Returns the fractions along each.</summary>
        public static bool SegmentIntersect(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2, out double t, out double u)
        {
            t = u = 0;
            Vec2 r = p2 - p1, s = q2 - q1;
            double denom = r.X * s.Z - r.Z * s.X;
            if (Math.Abs(denom) < 1e-12) return false;
            Vec2 qp = q1 - p1;
            t = (qp.X * s.Z - qp.Z * s.X) / denom;
            u = (qp.X * r.Z - qp.Z * r.X) / denom;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        /// <summary>Rectangle centred on a point, with half-length along a heading and half-width across it.</summary>
        public static Vec2[] Rect(Vec2 centre, double heading, double halfLength, double halfWidth)
        {
            Vec2 f = Vec2.FromHeading(heading), l = f.LeftNormal;
            return new[]
            {
                centre + f * halfLength + l * halfWidth,
                centre + f * halfLength - l * halfWidth,
                centre - f * halfLength - l * halfWidth,
                centre - f * halfLength + l * halfWidth,
            };
        }

        /// <summary>Offsets a polyline sideways (+ = left of travel) using averaged vertex normals.</summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> pts, double offset)
        {
            var result = new List<Vec2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vec2 dirIn = i > 0 ? Unit(pts[i] - pts[i - 1]) : Unit(pts[1] - pts[0]);
                Vec2 dirOut = i < pts.Count - 1 ? Unit(pts[i + 1] - pts[i]) : dirIn;
                Vec2 avg = Unit(dirIn + dirOut);
                double cos = Math.Max(0.5, avg.Dot(dirOut));
                result.Add(pts[i] + avg.LeftNormal * (offset / cos));
            }
            return result;
        }

        /// <summary>Part of a polyline between two distances along it.</summary>
        public static List<Vec2> Trim(IReadOnlyList<Vec2> pts, double from, double to)
        {
            var path = new PathPolyline(pts);
            to = Math.Min(to, path.Length);
            var list = new List<Vec2> { path.Sample(from).pos };
            double acc = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += (pts[i] - pts[i - 1]).Length;
                if (acc > from + 1e-6 && acc < to - 1e-6) list.Add(pts[i]);
            }
            list.Add(path.Sample(to).pos);
            return list;
        }

        public static List<Vec2> Reverse(IReadOnlyList<Vec2> pts)
        {
            var list = new List<Vec2>(pts);
            list.Reverse();
            return list;
        }

        private static Vec2 Unit(Vec2 v)
        {
            double l = v.Length;
            return l > 1e-9 ? v * (1 / l) : new Vec2(0, 1);
        }
    }
}
