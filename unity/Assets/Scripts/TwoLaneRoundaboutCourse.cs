using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>Two-lane 4-arm UK roundabout with lane arrows, 30 mph. Judges lane choice, give way, signals, lane discipline.</summary>
    public sealed class TwoLaneRoundaboutCourse : MonoBehaviour, ICourse
    {
        public TrafficView trafficView;
        public static readonly Vec2 Centre = new Vec2(0, 0);
        public const int StartArm = 2; // south

        private TwoLaneRoundaboutGeometry _geo;
        private TwoLaneRoundaboutTraffic _traffic;
        private RoundaboutRule _rule;
        private readonly List<VehicleSnapshot> _snap = new List<VehicleSnapshot>();

        public string DisplayName => "Two-lane roundabout (30 mph)";
        public TwoLaneRoundaboutGeometry Geometry => _geo ??= new TwoLaneRoundaboutGeometry(Centre);

        public CarState StartState
        {
            get
            {
                var (pos, heading) = Geometry.Entry[StartArm, 0].Sample(25);
                return new CarState { X = pos.X, Z = pos.Z, Heading = heading };
            }
        }

        public double SpeedLimitMph(in CarState player) => 30;
        public bool OutOfBounds(in CarState p) => (new Vec2(p.X, p.Z) - Centre).Length > Geometry.ArmLength + 25;

        private void Awake()
        {
            _traffic = new TwoLaneRoundaboutTraffic(Geometry, new TwoLaneTrafficParams { TargetCars = 14 }, seed: 2026);
            _rule = new RoundaboutRule(new TwoLaneRoundaboutView(_traffic));
        }

        public void ResetCourse()
        {
            if (trafficView != null) trafficView.Clear();
            _traffic.Reset();
            _rule.Reset();
            var away = new CarState { X = 1000, Z = 1000 };
            for (int i = 0; i < 50 * 40; i++) _traffic.Step(0.02, away);
            Snapshot();
        }

        public void CapturePrevious()
        {
            Snapshot();
            if (trafficView != null) trafficView.CapturePrevious(_snap);
        }

        public void Step(double dt, in CarState player, double simTime, int indicator, List<RuleEvent> events)
        {
            _traffic.Step(dt, player);
            _rule.OnFrame(new DriverFrame(simTime, player, indicator), dt, events);
        }

        public void Render(float alpha, bool blink)
        {
            Snapshot();
            if (trafficView != null) trafficView.Render(_snap, alpha, blink);
        }

        public void GetRoadUsers(List<RoadUser> into)
        {
            into.Clear();
            foreach (var c in _traffic.Cars) into.Add(new RoadUser(c.Id, c.Position, c.Heading, c.SpeedMps, c.IsCyclist));
        }

        private void Snapshot()
        {
            _snap.Clear();
            foreach (var c in _traffic.Cars)
                _snap.Add(new VehicleSnapshot
                {
                    Id = c.Id, X = c.Position.X, Z = c.Position.Z, Heading = c.Heading,
                    Indicator = c.Indicator, Colour = c.ColourIndex, IsCyclist = c.IsCyclist,
                });
        }
    }
}
