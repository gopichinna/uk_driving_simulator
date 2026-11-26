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
    public sealed class TownCourse : MonoBehaviour, ICourse, IScenarioWorld
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
        /// <summary>Added to the clock the signals read, so a scenario can make the lights change on cue.</summary>
        private double _signalShift;
        private readonly List<(int id, Vec2 pos, double heading)> _parked = new List<(int, Vec2, double)>();
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

        private LaneGraphGuide? _guide;
        public ISteeringGuide? SteeringGuide => _guide ??= new LaneGraphGuide(Geometry.Graph);

        public void ResetCourse()
        {
            _guide?.Reset();
            if (trafficView != null) trafficView.Clear();
            foreach (var kv in _pedViews) { kv.Value.gameObject.SetActive(false); _freePeds.Add(kv.Value); }
            _pedViews.Clear();
            _traffic.Reset();
            _peds.Reset();
            _rules.Reset();
            _parked.Clear();
            _signalShift = 0;
            _traffic.Params.TargetCars = 16;
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
            _traffic.Step(dt, player, _time + _signalShift);
            _lights[0] = _signals.State(0, _time + _signalShift);
            _lights[1] = _signals.State(1, _time + _signalShift);
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
            foreach (var (id, pos, heading) in _parked) into.Add(new RoadUser(id, pos, heading, 0, false));
        }

        // ---------------- scenarios (IScenarioWorld) ----------------

        private const int ParkedIdOffset = 2_000_000;

        public bool TryLocate(string name, out Vec2 position, out double heading)
        {
            heading = 0;
            position = default;
            var g = Geometry;
            switch (name)
            {
                case "zebra": position = Centroid(g.Graph.CrossingAreas[g.Zebra].Polygon); return true;
                case "mill_lane_mouth": position = Centroid(g.Graph.CrossingAreas[g.MillLaneMouth].Polygon); return true;
                case "church_st_mouth": position = Centroid(g.Graph.CrossingAreas[g.ChurchStMouth].Polygon); return true;
                case "crossroads_signals_line": position = TownGeometry.A; return true;
            }
            if (!g.Anchors.ContainsKey(name)) return false;
            (position, heading) = g.AnchorPose(name);
            if (name == "tjunction_north") position = TownGeometry.B;      // the junction itself, for distances
            else if (name == "tjunction_south") position = TownGeometry.D;
            return true;
        }

        private static Vec2 Centroid(Vec2[] poly)
        {
            Vec2 c = new Vec2(0, 0);
            foreach (var v in poly) c = c + v;
            return c * (1.0 / poly.Length);
        }

        public void SetTrafficDensity(double v) => _traffic.Params.TargetCars = Mathf.RoundToInt(4 + Mathf.Clamp01((float)v) * 20);

        private int CrossingId(string name) => name switch
        {
            "zebra" => Geometry.Zebra,
            "mill_lane_mouth" => Geometry.MillLaneMouth,
            "church_st_mouth" => Geometry.ChurchStMouth,
            _ => -1,
        };

        public void PedestrianWaits(string crossing) { int id = CrossingId(crossing); if (id >= 0) _peds.AddAtCrossing(id, stepOut: false); }
        public void PedestrianStepsOut(string crossing) { int id = CrossingId(crossing); if (id >= 0) _peds.AddAtCrossing(id, stepOut: true); }

        /// <summary>Player in the side road: a car arrives on the main road about gapS away. Player on the main road: a car waits to pull out.</summary>
        public void CarEmerges(string junction, double gapS, in CarState player)
        {
            var g = Geometry;
            bool north = junction == "tjunction_north";
            int minor = north ? g.Roads[6].Backward : g.Roads[7].Forward;          // Mill Lane northbound / Church St eastbound
            int major = north ? g.Roads[0].Forward : g.Roads[5].Backward;          // High St eastbound (from the west) / Station Rd northbound (from the south)
            var minorLane = g.Graph.Segments[minor];
            var (ms, _, md) = minorLane.Path.Project(new Vec2(player.X, player.Z));
            if (md < 3 && ms > 0)
            {
                var lane = g.Graph.Segments[major];
                double v = lane.SpeedLimitMph / 2.236936 * 0.9;
                _traffic.Add(major, System.Math.Max(0, lane.Length - v * gapS), v);
            }
            else _traffic.Add(minor, System.Math.Max(0, minorLane.Length - 4), 0);
        }

        /// <summary>A car parked half on the pavement (so traffic can pass) at an anchor, offset along the lane.</summary>
        public void ParkedCar(string anchor, double offsetM)
        {
            if (!Geometry.Anchors.TryGetValue(anchor, out var a)) return;
            var lane = Geometry.Graph.Segments[a.segment];
            var (p, h) = lane.Path.Sample(System.Math.Max(2, System.Math.Min(lane.Length - 2, a.s + offsetM)));
            _parked.Add((ParkedIdOffset + _parked.Count, p + Vec2.FromHeading(h).LeftNormal * 2.4, h));
        }

        public void CyclistAhead(string anchor, double offsetM, double speedMph)
        {
            if (!Geometry.Anchors.TryGetValue(anchor, out var a)) return;
            var lane = Geometry.Graph.Segments[a.segment];
            _traffic.Add(a.segment, System.Math.Max(0, System.Math.Min(lane.Length - 1, a.s + offsetM)), speedMph / 2.236936, cyclist: true);
        }

        /// <summary>Shifts the signal clock so the lights facing the player have just turned amber.</summary>
        public bool LightsTurnAmber(in CarState player)
        {
            var seg = Geometry.Graph.Nearest(new Vec2(player.X, player.Z), out _, out double d, lanesOnly: true);
            if (seg == null || d > 3 || seg.EndControl != LineControl.Signals) return false;
            double now = _time + _signalShift;
            for (double dt = 0; dt < _signals.CycleS; dt += 0.05)
                if (_signals.State(seg.SignalGroup, now + dt) == LightState.Amber && _signals.State(seg.SignalGroup, now + dt - 0.05) == LightState.Green)
                {
                    _signalShift += dt;
                    return true;
                }
            return false;
        }

        public int ExitTaken(in CarState player) => 0;

        private void Snapshot()
        {
            _snap.Clear();
            foreach (var (id, pos, heading) in _parked)
                _snap.Add(new VehicleSnapshot { Id = id, X = pos.X, Z = pos.Z, Heading = heading, Colour = id % 6 });
            foreach (var c in _traffic.Cars)
                _snap.Add(new VehicleSnapshot
                {
                    Id = c.Id, X = c.Position.X, Z = c.Position.Z, Heading = c.Heading,
                    Indicator = c.Indicator, Colour = c.ColourIndex, IsCyclist = c.IsCyclist,
                });
        }
    }
}
