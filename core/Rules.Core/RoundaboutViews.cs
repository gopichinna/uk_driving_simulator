using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>A vehicle on the circulatory carriageway: bearing of its front (radians, clockwise from north).</summary>
    public readonly struct RingVehicle
    {
        public RingVehicle(double bearing, double radius, double speedMps, double lastAccelMps2)
        {
            Bearing = bearing;
            Radius = radius;
            SpeedMps = speedMps;
            LastAccelMps2 = lastAccelMps2;
        }

        public double Bearing { get; }
        public double Radius { get; }
        public double SpeedMps { get; }
        public double LastAccelMps2 { get; }
    }

    /// <summary>What the roundabout rules need to know about any roundabout and its traffic.</summary>
    public interface IRoundaboutView
    {
        int ArmCount { get; }
        /// <summary>Number of approach lanes (1 or 2).</summary>
        int ApproachLanes { get; }
        bool OnRing(Vec2 p);
        double BearingOf(Vec2 p);
        double RadiusOf(Vec2 p);
        /// <summary>Arm (and approach lane) the point is approaching on, or -1.</summary>
        int ApproachArm(Vec2 p, double heading, out int lane);
        /// <summary>Exit arm (and exit lane, 0 = left) the point is leaving on, or -1.</summary>
        int ExitArm(Vec2 p, out int lane);
        double DivergeBearing(int arm);
        int ExitNumber(int entryArm, int exitArm);
        /// <summary>Lane the lane arrows tell you to use for this exit (0 = left).</summary>
        int RecommendedApproachLane(int exitNumber);
        /// <summary>0 = outer lane, 1 = inner lane; always 0 on a single-lane roundabout.</summary>
        int RingLaneAt(double radius);
        void RingVehicles(List<RingVehicle> into);
    }

    /// <summary>Adapter for the single-lane <see cref="RoundaboutTraffic"/>.</summary>
    public sealed class SingleLaneRoundaboutView : IRoundaboutView
    {
        private readonly RoundaboutTraffic _t;
        private readonly RoundaboutGeometry _g;

        public SingleLaneRoundaboutView(RoundaboutTraffic traffic)
        {
            _t = traffic;
            _g = traffic.Geometry;
        }

        public int ArmCount => _g.ArmCount;
        public int ApproachLanes => 1;
        public bool OnRing(Vec2 p) => _g.OnRing(p);
        public double BearingOf(Vec2 p) => TwoLaneRoundaboutGeometry.Norm((p - _g.Centre).Heading);
        public double RadiusOf(Vec2 p) => (p - _g.Centre).Length;
        public double DivergeBearing(int arm) => _g.ExitDivergeS[arm] / _g.RingRadius;
        public int ExitNumber(int a, int b) => _g.ExitNumber(a, b);
        public int RecommendedApproachLane(int exitNumber) => 0;
        public int RingLaneAt(double radius) => 0;

        public int ApproachArm(Vec2 p, double heading, out int lane)
        {
            lane = 0;
            for (int k = 0; k < _g.ArmCount; k++)
            {
                var path = _g.Entry[k];
                var (s, _, dist) = path.Project(p);
                if (dist > 2.8 || s < _g.GiveWayS[k] - 70 || s > path.Length - 1) continue;
                var (_, h) = path.Sample(s);
                if (Math.Cos(heading - h) > 0.3) return k;
            }
            return -1;
        }

        public int ExitArm(Vec2 p, out int lane)
        {
            lane = 0;
            for (int k = 0; k < _g.ArmCount; k++)
            {
                var (s, _, dist) = _g.Exit[k].Project(p);
                if (dist < 3.0 && s > 1.5) return k;
            }
            return -1;
        }

        public void RingVehicles(List<RingVehicle> into)
        {
            into.Clear();
            foreach (var c in _t.Cars)
                if (c.Segment == RoundaboutSegment.Ring)
                    into.Add(new RingVehicle(c.S / _g.RingRadius, _g.RingRadius, c.SpeedMps, c.LastAccelMps2));
        }
    }

    /// <summary>Adapter for <see cref="TwoLaneRoundaboutTraffic"/>.</summary>
    public sealed class TwoLaneRoundaboutView : IRoundaboutView
    {
        private readonly TwoLaneRoundaboutTraffic _t;
        private readonly TwoLaneRoundaboutGeometry _g;

        public TwoLaneRoundaboutView(TwoLaneRoundaboutTraffic traffic)
        {
            _t = traffic;
            _g = traffic.Geometry;
        }

        public int ArmCount => _g.ArmCount;
        public int ApproachLanes => 2;
        public bool OnRing(Vec2 p) => _g.OnRing(p);
        public double BearingOf(Vec2 p) => _g.BearingOf(p);
        public double RadiusOf(Vec2 p) => _g.RadiusOf(p);
        /// <summary>The first (outer-lane) turn-off point for this exit.</summary>
        public double DivergeBearing(int arm) => _g.DivergeBearing[arm, 0];
        public int ExitNumber(int a, int b) => _g.ExitNumber(a, b);
        public int RecommendedApproachLane(int exitNumber) => TwoLaneRoundaboutGeometry.ApproachLaneFor(exitNumber);
        public int RingLaneAt(double radius) => TwoLaneRoundaboutGeometry.RingLaneAt(radius);

        public int ApproachArm(Vec2 p, double heading, out int lane)
        {
            lane = -1;
            int bestArm = -1;
            double best = 1.9; // lanes are 3.5 m apart
            for (int k = 0; k < _g.ArmCount; k++)
                for (int l = 0; l < 2; l++)
                {
                    var path = _g.Entry[k, l];
                    var (s, _, dist) = path.Project(p);
                    if (dist > best || s < _g.GiveWayS[k, l] - 70 || s > path.Length - 1) continue;
                    var (_, h) = path.Sample(s);
                    if (Math.Cos(heading - h) <= 0.3) continue;
                    best = dist;
                    bestArm = k;
                    lane = l;
                }
            return bestArm;
        }

        public int ExitArm(Vec2 p, out int lane)
        {
            lane = -1;
            int bestArm = -1;
            double best = 1.9; // exit lanes are 3.5 m apart
            for (int k = 0; k < _g.ArmCount; k++)
                for (int l = 0; l < 2; l++)
                {
                    var (s, _, dist) = _g.Exit[k, l].Project(p);
                    if (dist < best && s > 1.5) { best = dist; bestArm = k; lane = l; }
                }
            return bestArm;
        }

        public void RingVehicles(List<RingVehicle> into)
        {
            into.Clear();
            foreach (var c in _t.Cars)
                if (c.Segment == TwoLaneSegment.Ring)
                    into.Add(new RingVehicle(c.Bearing, c.Radius, c.SpeedMps, c.LastAccelMps2));
        }
    }
}
