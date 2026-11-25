using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Wolverhampton city centre (beta): real streets from the OpenStreetMap-derived road pack (ODbL).
    /// AI traffic drives around the player; lights, give way, zebras and keeping left are judged, but only on data
    /// with confidence ≥ 0.7 (signal phases are always judged against what the game shows).
    /// </summary>
    public sealed class WolverhamptonCourse : MonoBehaviour, ICourse, IScenarioWorld
    {
        public TrafficView trafficView;
        public Transform signalsRoot;
        public string startLink = "";
        public float startOffset = 5f;
        /// <summary>Which city pack this course shows: Resources/Content/&lt;prefix&gt;_roadpack and _pois.</summary>
        public string resourcePrefix = "wolverhampton";

        private RoadPackGraph _built;
        private TownTraffic _traffic;
        private TownRules _rules;
        private KeepLeftRule _keepLeft;
        private double _time;
        private string _attribution = "© OpenStreetMap contributors";
        private readonly List<LightState> _lights = new List<LightState>();
        private readonly List<VehicleSnapshot> _snap = new List<VehicleSnapshot>();
        private readonly List<VehicleSnapshot> _replay = new List<VehicleSnapshot>();
        private readonly List<RoadUser> _users = new List<RoadUser>();
        private readonly List<Pedestrian> _noPeople = new List<Pedestrian>();
        private readonly List<(int group, GameObject red, GameObject amber, GameObject green)> _heads = new List<(int, GameObject, GameObject, GameObject)>();
        private GUIStyle _credit;

        public string DisplayName => resourcePrefix == "wolverhampton" ? "Wolverhampton city centre (beta)" : $"{Built.Pack.DisplayName} (beta)";
        public LaneGraph Graph => Built.Graph;

        private RoadPackGraph Built
        {
            get
            {
                if (_built != null) return _built;
                var asset = Resources.Load<TextAsset>($"Content/{resourcePrefix}_roadpack");
                _built = new RoadPackGraph(RoadPack.Parse(asset.text));
                if (!string.IsNullOrEmpty(_built.Pack.Attribution)) _attribution = _built.Pack.Attribution;
                return _built;
            }
        }

        public CarState StartState
        {
            get
            {
                var b = Built;
                int lane = b.LinkLanes.TryGetValue(startLink, out var l) && l.forward.Count > 0 ? l.forward[0] : b.Spawns[0].lane;
                var seg = b.Graph.Segments[lane];
                var (pos, heading) = seg.Path.Sample(Mathf.Min(startOffset, (float)seg.Length - 1));
                return new CarState { X = pos.X, Z = pos.Z, Heading = heading };
            }
        }

        public double SpeedLimitMph(in CarState p)
        {
            var seg = Built.Graph.Nearest(new Vec2(p.X, p.Z), out _, out _, lanesOnly: true);
            return seg?.SpeedLimitMph ?? 30;
        }

        public bool OutOfBounds(in CarState p)
        {
            Built.Graph.Nearest(new Vec2(p.X, p.Z), out _, out double d);
            return d > 25;
        }

        private void Awake()
        {
            var b = Built;
            _traffic = new TownTraffic(b.Graph, b.Signals, new TownTrafficParams { TargetCars = 18, SpawnAroundPlayer = true }, seed: 2026);
            _rules = new TownRules(b.Graph, DrivingSide.Left);
            _keepLeft = new KeepLeftRule(b.Graph, DrivingSide.Left);
            for (int i = 0; i < b.Signals.GroupCount; i++) _lights.Add(LightState.Red);

            if (signalsRoot != null)
                foreach (var t in signalsRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("SignalHead_g") && int.TryParse(t.name.Substring("SignalHead_g".Length), out int group))
                        _heads.Add((group, Lamp(t, "Red"), Lamp(t, "Amber"), Lamp(t, "Green")));
        }

        private static GameObject Lamp(Transform head, string colour)
        {
            var lamp = head.Find(colour + "/On");
            return lamp != null ? lamp.gameObject : null;
        }

        public void ResetCourse()
        {
            if (trafficView != null) trafficView.Clear();
            _traffic.Reset();
            _rules.Reset();
            _keepLeft.Reset();
            var start = StartState;
            for (int i = 0; i < 50 * 20; i++) _traffic.Step(0.02, start, i * 0.02);
            _time = 20;
            UpdateLights();
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
            _traffic.Step(dt, player, _time);
            UpdateLights();
            GetRoadUsers(_users);
            var frame = new DriverFrame(simTime, player, indicator);
            _rules.OnFrame(frame, _lights, _noPeople, _users, events);
            _keepLeft.OnFrame(frame, _users, events);
        }

        private void UpdateLights()
        {
            for (int i = 0; i < _lights.Count; i++) _lights[i] = _built.Signals.State(i, _time);
        }

        public void Render(float alpha, bool blink)
        {
            Snapshot();
            if (trafficView != null) trafficView.Render(_snap, alpha, blink);
            ShowLights(_lights);
        }

        private void ShowLights(IReadOnlyList<LightState> lights)
        {
            foreach (var (group, red, amber, green) in _heads)
            {
                if (group >= lights.Count) continue;
                var s = lights[group];
                if (red != null) red.SetActive(s == LightState.Red || s == LightState.RedAmber);
                if (amber != null) amber.SetActive(s == LightState.Amber || s == LightState.RedAmber);
                if (green != null) green.SetActive(s == LightState.Green);
            }
        }

        public void GetRoadUsers(List<RoadUser> into)
        {
            into.Clear();
            foreach (var c in _traffic.Cars)
                into.Add(new RoadUser(c.Id, c.Position, c.Heading, c.SpeedMps, c.IsCyclist, c.LengthM, c.LastAccelMps2, c.BrakingForPlayer));
        }

        public void Record(List<ActorPose> actors, List<LightState> lights)
        {
            actors.Clear();
            Snapshot();
            Replay.AddVehicles(actors, _snap);
            lights.Clear();
            lights.AddRange(_lights);
        }

        public void ShowRecorded(RecordedFrame frame, bool blink)
        {
            Replay.Vehicles(frame, _replay);
            if (trafficView != null) trafficView.Render(_replay, 1f, blink);
            if (frame.Lights.Length > 0) ShowLights(frame.Lights);
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

        // ---------------- scenarios (IScenarioWorld) ----------------

        private Dictionary<string, Vec2> _pois;

        public bool TryLocate(string name, out Vec2 position, out double heading)
        {
            heading = 0;
            if (name == "start")
            {
                var st = StartState;
                position = new Vec2(st.X, st.Z);
                heading = st.Heading;
                return true;
            }
            if (_pois == null)
            {
                _pois = new Dictionary<string, Vec2>();
                var asset = Resources.Load<TextAsset>($"Content/{resourcePrefix}_pois");
                if (asset != null && MiniJson.Parse(asset.text) is Dictionary<string, object> root && root.TryGetValue("pois", out var list))
                    foreach (var o in MiniJson.Arr(list))
                    {
                        var p = MiniJson.Obj(o);
                        _pois[(string)p["id"]] = new Vec2(MiniJson.Num(p["x"]), MiniJson.Num(p["z"]));
                    }
            }
            return _pois.TryGetValue(name, out position);
        }

        public void SetTrafficDensity(double v) => _traffic.Params.TargetCars = Mathf.RoundToInt(6 + Mathf.Clamp01((float)v) * 20);
        public void PedestrianWaits(string crossing) { }
        public void PedestrianStepsOut(string crossing) { }
        public void CarEmerges(string junction, double gapS, in CarState player) { }
        public void ParkedCar(string anchor, double offsetM) { }
        public void CyclistAhead(string anchor, double offsetM, double speedMph) { }
        public bool LightsTurnAmber(in CarState player) => false;
        public int ExitTaken(in CarState player) => 0;

        /// <summary>ODbL: the map data must be credited where it is shown.</summary>
        private void OnGUI()
        {
            _credit ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight };
            _credit.fontSize = Mathf.RoundToInt(Screen.height * 0.024f);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(Screen.width * 0.55f, Screen.height * 0.955f, Screen.width * 0.44f, Screen.height * 0.04f), _attribution, _credit);
            GUI.color = Color.white;
        }
    }
}
