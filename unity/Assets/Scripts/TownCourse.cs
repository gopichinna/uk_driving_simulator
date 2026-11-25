using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Town centre, 30 mph with a 20 mph school street: traffic lights with cycle boxes and a yellow box,
    /// two give-way T-junctions, a zebra crossing and pedestrians. Judges lights, give way, crossings, zig-zags,
    /// the box, keeping left and signalling when moving out. Signal heads, Belisha beacons and pedestrians in the scene are found by name.
    /// </summary>
    public sealed class TownCourse : MonoBehaviour, ICourse
    {
        public TrafficView trafficView;
        public Transform pedestrianRoot;
        /// <summary>Parent of the signal heads ("SignalHead_g{group}") and Belisha beacon lamps ("BelishaOn").</summary>
        public Transform signalsRoot;
        /// <summary>Where the drive starts (a lesson anchor, see TownGeometry.Anchors).</summary>
        public string startAnchor = "start";

        private TownGeometry _geo;
        private TrafficSignals _signals;
        private TownTraffic _traffic;
        private TownPedestrians _peds;
        private TownRules _rules;
        private KeepLeftRule _keepLeft;
        private double _time;
        private readonly List<LightState> _lights = new List<LightState> { LightState.Red, LightState.Red };
        private readonly List<VehicleSnapshot> _snap = new List<VehicleSnapshot>();
        private readonly List<RoadUser> _users = new List<RoadUser>();
        private readonly List<(int group, GameObject red, GameObject amber, GameObject green)> _heads = new List<(int, GameObject, GameObject, GameObject)>();
        private readonly List<GameObject> _beacons = new List<GameObject>();
        private readonly List<Transform> _freePeds = new List<Transform>();
        private readonly Dictionary<int, Transform> _pedViews = new Dictionary<int, Transform>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _release = new List<int>();

        public string DisplayName => "Town centre (30 mph; 20 by the school)";
        public TownGeometry Geometry => _geo ??= new TownGeometry();

        public CarState StartState
        {
            get
            {
                var (pos, heading) = Geometry.AnchorPose(Geometry.Anchors.ContainsKey(startAnchor) ? startAnchor : "start");
                return new CarState { X = pos.X, Z = pos.Z, Heading = heading };
            }
        }

        public double SpeedLimitMph(in CarState player) => Geometry.SpeedLimitAt(new Vec2(player.X, player.Z));

        public bool OutOfBounds(in CarState p)
        {
            Geometry.Graph.Nearest(new Vec2(p.X, p.Z), out _, out double d);
            return d > 22;
        }

        private void Awake()
        {
            _signals = TownGeometry.CreateSignals();
            _traffic = new TownTraffic(Geometry.Graph, _signals, new TownTrafficParams { TargetCars = 16 }, seed: 2026);
            _peds = new TownPedestrians(Geometry.Graph, Geometry.PedestrianRoutes, 12, seed: 2027);
            _traffic.Pedestrians = _peds;
            _rules = new TownRules(Geometry.Graph, DrivingSide.Left);
            _keepLeft = new KeepLeftRule(Geometry.Graph, DrivingSide.Left);

            foreach (var t in signalsRoot != null ? signalsRoot.GetComponentsInChildren<Transform>(true) : new Transform[0])
            {
                if (t.name.StartsWith("SignalHead_g"))
                {
                    int group = t.name["SignalHead_g".Length] - '0';
                    _heads.Add((group, Lamp(t, "Red"), Lamp(t, "Amber"), Lamp(t, "Green")));
                }
                else if (t.name == "BelishaOn") _beacons.Add(t.gameObject);
            }
            if (pedestrianRoot != null)
                foreach (Transform p in pedestrianRoot) { p.gameObject.SetActive(false); _freePeds.Add(p); }
        }

        private static GameObject Lamp(Transform head, string colour)
        {
            var lamp = head.Find(colour + "/On");
            return lamp != null ? lamp.gameObject : null;
        }

        public void ResetCourse()
        {
            if (trafficView != null) trafficView.Clear();
            foreach (var kv in _pedViews) { kv.Value.gameObject.SetActive(false); _freePeds.Add(kv.Value); }
            _pedViews.Clear();
            _traffic.Reset();
            _peds.Reset();
            _rules.Reset();
            _keepLeft.Reset();
            // Warm up so the town is busy when the drive starts (the player is parked far away meanwhile).
            var away = new CarState { X = 5000, Z = 5000 };
            for (int i = 0; i < 50 * 45; i++)
            {
                double t = i * 0.02;
                _peds.Step(0.02, _traffic, away);
                _traffic.Step(0.02, away, t);
            }
            _time = 45;
            Snapshot();
        }

        public void CapturePrevious()
        {
            Snapshot();
            if (trafficView != null) trafficView.CapturePrevious(_snap);
        }

        public void Step(double dt, in CarState player, double simTime, int indicator, List<RuleEvent> events)
        {
            _time += dt;
            _peds.Step(dt, _traffic, player);
            _traffic.Step(dt, player, _time);
            _lights[0] = _signals.State(0, _time);
            _lights[1] = _signals.State(1, _time);
            GetRoadUsers(_users);
            var frame = new DriverFrame(simTime, player, indicator);
            _rules.OnFrame(frame, _lights, _peds.People, _users, events);
            _keepLeft.OnFrame(frame, _users, events);
        }

        public void Render(float alpha, bool blink)
        {
            Snapshot();
            if (trafficView != null) trafficView.Render(_snap, alpha, blink);
            ShowLights(_lights);
            _pedPoses.Clear();
            foreach (var p in _peds.People) _pedPoses.Add((p.Id, p.Position.X, p.Position.Z, p.Heading));
            RenderPedestrians(_pedPoses);
        }

        private const int PedIdOffset = 1_000_000;
        private readonly List<(int id, double x, double z, double heading)> _pedPoses = new List<(int, double, double, double)>();
        private readonly List<VehicleSnapshot> _replay = new List<VehicleSnapshot>();

        public void Record(List<ActorPose> actors, List<LightState> lights)
        {
            actors.Clear();
            Snapshot();
            Replay.AddVehicles(actors, _snap);
            foreach (var p in _peds.People)
                actors.Add(new ActorPose { Id = PedIdOffset + p.Id, Kind = ActorKind.Pedestrian, X = (float)p.Position.X, Z = (float)p.Position.Z, Heading = (float)p.Heading });
            lights.Clear();
            lights.AddRange(_lights);
        }

        public void ShowRecorded(RecordedFrame frame, bool blink)
        {
            Replay.Vehicles(frame, _replay);
            if (trafficView != null) trafficView.Render(_replay, 1f, blink);
            if (frame.Lights.Length >= 2) ShowLights(frame.Lights);
            _pedPoses.Clear();
            foreach (var a in frame.Actors)
                if (a.Kind == ActorKind.Pedestrian) _pedPoses.Add((a.Id - PedIdOffset, a.X, a.Z, a.Heading));
            RenderPedestrians(_pedPoses);
        }

        private void ShowLights(IReadOnlyList<LightState> lights)
        {
            foreach (var (group, red, amber, green) in _heads)
            {
                var s = lights[group];
                if (red != null) red.SetActive(s == LightState.Red || s == LightState.RedAmber);
                if (amber != null) amber.SetActive(s == LightState.Amber || s == LightState.RedAmber);
                if (green != null) green.SetActive(s == LightState.Green);
            }
            bool beaconOn = Mathf.Repeat(Time.time, 1.0f) < 0.5f;
            foreach (var b in _beacons) b.SetActive(beaconOn);
        }

        private void RenderPedestrians(List<(int id, double x, double z, double heading)> people)
        {
            _seen.Clear();
            foreach (var (id, x, z, heading) in people)
            {
                _seen.Add(id);
                if (!_pedViews.TryGetValue(id, out var view))
                {
                    if (_freePeds.Count == 0) continue;
                    view = _freePeds[_freePeds.Count - 1];
                    _freePeds.RemoveAt(_freePeds.Count - 1);
                    view.gameObject.SetActive(true);
                    _pedViews[id] = view;
                }
                view.SetPositionAndRotation(new Vector3((float)x, 0.08f, (float)z), Quaternion.Euler(0f, (float)(heading * Mathf.Rad2Deg), 0f));
            }
            _release.Clear();
            foreach (var id in _pedViews.Keys) if (!_seen.Contains(id)) _release.Add(id);
            foreach (var id in _release)
            {
                _pedViews[id].gameObject.SetActive(false);
                _freePeds.Add(_pedViews[id]);
                _pedViews.Remove(id);
            }
        }

        public void GetRoadUsers(List<RoadUser> into)
        {
            into.Clear();
            foreach (var c in _traffic.Cars)
                into.Add(new RoadUser(c.Id, c.Position, c.Heading, c.SpeedMps, c.IsCyclist, c.LengthM, c.LastAccelMps2, c.BrakingForPlayer));
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
