using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>2D point on the ground plane. X = east, Z = north (metres).</summary>
    public readonly struct Vec2
    {
        public Vec2(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, double k) => new Vec2(a.X * k, a.Z * k);
        public double Length => Math.Sqrt(X * X + Z * Z);
        public double Dot(Vec2 o) => X * o.X + Z * o.Z;

        /// <summary>Unit vector for a heading (0 = north, positive = clockwise).</summary>
        public static Vec2 FromHeading(double heading) => new Vec2(Math.Sin(heading), Math.Cos(heading));

        /// <summary>Heading of this direction vector (0 = north, positive = clockwise).</summary>
        public double Heading => Math.Atan2(X, Z);

        /// <summary>Left of a direction of travel (90° anticlockwise).</summary>
        public Vec2 LeftNormal => new Vec2(-Z, X);

        public override string ToString() => $"({X:0.00}, {Z:0.00})";
    }

    /// <summary>A lane centreline as a polyline, measured by distance along it (s).</summary>
    public sealed class PathPolyline
    {
        private readonly Vec2[] _pts;
        private readonly double[] _cum;

        public PathPolyline(IReadOnlyList<Vec2> points, bool closed = false)
        {
            if (points.Count < 2) throw new ArgumentException("A path needs at least two points.");
            Closed = closed;
            int n = points.Count + (closed ? 1 : 0);
            _pts = new Vec2[n];
            for (int i = 0; i < points.Count; i++) _pts[i] = points[i];
            if (closed) _pts[n - 1] = points[0];

            _cum = new double[n];
            for (int i = 1; i < n; i++) _cum[i] = _cum[i - 1] + (_pts[i] - _pts[i - 1]).Length;
            Length = _cum[n - 1];
        }

        public bool Closed { get; }
        public double Length { get; }
        public IReadOnlyList<Vec2> Points => _pts;

        public double Wrap(double s)
        {
            if (!Closed) return Math.Max(0, Math.Min(Length, s));
            s %= Length;
            return s < 0 ? s + Length : s;
        }

        /// <summary>Position and heading at distance s along the path.</summary>
        public (Vec2 pos, double heading) Sample(double s)
        {
            s = Wrap(s);
            int i = Segment(s);
            Vec2 a = _pts[i], b = _pts[i + 1];
            double segLen = _cum[i + 1] - _cum[i];
            double t = segLen > 1e-9 ? (s - _cum[i]) / segLen : 0;
            return (a + (b - a) * t, (b - a).Heading);
        }

        /// <summary>Nearest point on the path: distance along it, signed lateral offset (+ = left of travel), and distance.</summary>
        public (double s, double lateral, double distance) Project(Vec2 p)
        {
            double bestD2 = double.MaxValue, bestS = 0, bestLat = 0;
            for (int i = 0; i < _pts.Length - 1; i++)
            {
                Vec2 a = _pts[i], ab = _pts[i + 1] - a;
                double len2 = ab.Dot(ab);
                double t = len2 > 1e-12 ? Math.Max(0, Math.Min(1, (p - a).Dot(ab) / len2)) : 0;
                Vec2 q = a + ab * t;
                Vec2 d = p - q;
                double d2 = d.Dot(d);
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    bestS = _cum[i] + Math.Sqrt(len2) * t;
                    double len = Math.Sqrt(len2);
                    bestLat = len > 1e-9 ? d.Dot(ab.LeftNormal) / len : 0;
                }
            }
            return (bestS, bestLat, Math.Sqrt(bestD2));
        }

        private int Segment(double s)
        {
            int lo = 0, hi = _cum.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_cum[mid] <= s) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Samples a cubic Bézier into points (excluding nothing; includes both ends).</summary>
        public static List<Vec2> Bezier(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, int steps)
        {
            var list = new List<Vec2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps, u = 1 - t;
                list.Add(p0 * (u * u * u) + p1 * (3 * u * u * t) + p2 * (3 * u * t * t) + p3 * (t * t * t));
            }
            return list;
        }
    }

    /// <summary>Intelligent Driver Model acceleration (shared by all traffic models).</summary>
    public static class Idm
    {
        public static double Accel(double v, double v0, double gap, double vLeader,
            double minGap = 2.0, double headway = 1.5, double aMax = 1.5, double bComfort = 2.0)
        {
            v0 = Math.Max(0.1, v0);
            double free = 1 - Math.Pow(v / v0, 4);
            if (double.IsPositiveInfinity(gap)) return aMax * free;
            double dv = v - vLeader;
            double sStar = minGap + Math.Max(0, v * headway + v * dv / (2 * Math.Sqrt(aMax * bComfort)));
            double s = Math.Max(0.1, gap);
            return Math.Max(-9.0, aMax * (free - (sStar / s) * (sStar / s)));
        }
    }
}
