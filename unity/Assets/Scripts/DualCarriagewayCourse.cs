using System;
using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>40 mph dual carriageway with overtaking traffic. Judges lane changes.</summary>
    public sealed class DualCarriagewayCourse : MonoBehaviour, ICourse, IScenarioWorld
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

        private StraightRoadGuide _guide;
        public ISteeringGuide SteeringGuide => _guide ??= new StraightRoadGuide(-1.825, +1.825);   // our two lanes

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

        private readonly List<VehicleSnapshot> _replay = new List<VehicleSnapshot>();

        public void Record(List<ActorPose> actors, List<LightState> lights)
        {
            actors.Clear();
            lights.Clear();
            Snapshot();
            Replay.AddVehicles(actors, _snap);
        }

        public void ShowRecorded(RecordedFrame frame, bool blink)
        {
            Replay.Vehicles(frame, _replay);
            if (trafficView != null) trafficView.Render(_replay, 1f, blink);
        }


        // ---------------- scenarios (IScenarioWorld) ----------------

        public bool TryLocate(string name, out Vec2 position, out double heading)
        {
            var st = StartState;
            position = new Vec2(st.X, st.Z);
            heading = st.Heading;
            return name == "start";
        }

        public void PedestrianWaits(string crossing) { }
        public void PedestrianStepsOut(string crossing) { }
        public void CarEmerges(string junction, double gapS, in CarState player) { }
        public void ParkedCar(string anchor, double offsetM) { }
        public bool LightsTurnAmber(in CarState player) => false;
        public void SetTrafficDensity(double v)
        {
            int n = Mathf.RoundToInt(1 + Mathf.Clamp01((float)v) * 6);
            _params.CarsPerLane = new[] { n, Mathf.Max(1, n - 2), n, n };
        }
        public void CyclistAhead(string anchor, double offsetM, double speedMph) { }
        public int ExitTaken(in CarState player) => 0;

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
