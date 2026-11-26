using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>Two-lane 4-arm UK roundabout with lane arrows, 30 mph. Judges lane choice, give way, signals, lane discipline.</summary>
    public sealed class TwoLaneRoundaboutCourse : MonoBehaviour, ICourse, IScenarioWorld
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
            _traffic = new TwoLaneRoundaboutTraffic(Geometry, _params, seed: 2026);
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
        private readonly TwoLaneTrafficParams _params = new TwoLaneTrafficParams { TargetCars = 14 };
        public void SetTrafficDensity(double v) => _params.TargetCars = Mathf.RoundToInt(4 + Mathf.Clamp01((float)v) * 16);
        public void CyclistAhead(string anchor, double offsetM, double speedMph) { }

        public int ExitTaken(in CarState player)
        {
            var p = new Vec2(player.X, player.Z);
            for (int k = 0; k < Geometry.ArmCount; k++)
                for (int l = 0; l < 2; l++)
                {
                    var (s, _, d) = Geometry.Exit[k, l].Project(p);
                    if (d < 2.5 && s > 14) return Geometry.ExitNumber(StartArm, k);
                }
            return 0;
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
