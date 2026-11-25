using System;
using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>40 mph dual carriageway with overtaking traffic. Judges lane changes.</summary>
    public sealed class DualCarriagewayCourse : MonoBehaviour, ICourse
    {
        public TrafficView trafficView;
        public float roadEndZ = 1900f;

        private static readonly StraightLane[] Lanes =
        {
            new StraightLane(-1.825, +1, isNearside: true),   // lane 1 (left) – ours
            new StraightLane(+1.825, +1, isNearside: false),  // lane 2 (overtaking) – ours
            new StraightLane(8.475, -1, isNearside: false),   // oncoming, their lane 2
            new StraightLane(12.125, -1, isNearside: true),   // oncoming, their lane 1
        };

        private TrafficSim _traffic;
        private LaneChangeRule _laneChange;
        private readonly List<VehicleSnapshot> _snap = new List<VehicleSnapshot>();
        private readonly TrafficParams _params = new TrafficParams { CarsPerLane = new[] { 5, 3, 4, 4 } };

        public string DisplayName => "Dual carriageway (40 mph)";
        public CarState StartState => new CarState { X = -1.825, Z = 0 };
        public double SpeedLimitMph(in CarState player) => 40;
        public bool OutOfBounds(in CarState p) => p.Z > roadEndZ || Math.Abs(p.X) > 60;

        private void Awake()
        {
            _traffic = new TrafficSim(Lanes, _params, seed: 2026);
            _laneChange = new LaneChangeRule(_traffic, drivingSide: DrivingSide.Left);
        }

        public void ResetCourse()
        {
            if (trafficView != null) trafficView.Clear();
            _traffic.Reset(StartState.Z);
            _laneChange.Reset();
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
            _laneChange.OnFrame(new DriverFrame(simTime, player, indicator), events);
        }

        public void Render(float alpha, bool blink)
        {
            Snapshot();
            if (trafficView != null) trafficView.Render(_snap, alpha, blink);
        }

        public void GetRoadUsers(List<RoadUser> into)
        {
            into.Clear();
            foreach (var c in _traffic.Cars)
                into.Add(new RoadUser(c.Id, new Vec2(c.X, c.Z), _traffic.Lanes[c.Lane].Direction > 0 ? 0 : Math.PI, c.SpeedMps, c.IsCyclist));
        }

        private void Snapshot()
        {
            _snap.Clear();
            foreach (var c in _traffic.Cars)
            {
                int dir = _traffic.Lanes[c.Lane].Direction;
                double vx = 0;
                if (c.ChangingLane && c.SignalTimer <= 0)
                    vx = Math.Sign(_traffic.Lanes[c.TargetLane].CenterX - c.X) * _params.LateralSpeedMps;
                _snap.Add(new VehicleSnapshot
                {
                    Id = c.Id, X = c.X, Z = c.Z,
                    Heading = Math.Atan2(vx, dir * Math.Max(1.0, c.SpeedMps)),
                    Indicator = c.Indicator, Colour = c.ColourIndex, IsCyclist = c.IsCyclist,
                });
            }
        }
    }
}
