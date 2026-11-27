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
        /// <summary>Lesson start point: "start", or on motorways "join" / "leave".</summary>
        public string startAnchor = "start";
        public Transform pedestrianRoot;

        private RoadPackGraph _built;
        private TownTraffic _traffic;
        private TownRules _rules;
        private SchoolSlowRule _school;
        private KeepLeftRule _keepLeft;
        private double _time;
        private string _attribution = "© OpenStreetMap contributors";
        private readonly List<LightState> _lights = new List<LightState>();
        private readonly List<VehicleSnapshot> _snap = new List<VehicleSnapshot>();
        private readonly List<VehicleSnapshot> _replay = new List<VehicleSnapshot>();
        private readonly List<RoadUser> _users = new List<RoadUser>();
        private readonly List<Pedestrian> _noPeople = new List<Pedestrian>();
        private TownPedestrians _peds;
        private const int PedIdOffset = 1_000_000;
        private readonly List<Transform> _freePeds = new List<Transform>();
        private readonly Dictionary<int, Transform> _pedViews = new Dictionary<int, Transform>();
        private readonly HashSet<int> _pedSeen = new HashSet<int>();
        private readonly List<int> _pedRelease = new List<int>();
        private readonly List<(int id, double x, double z, double heading)> _pedPoses = new List<(int, double, double, double)>();
        private readonly List<(int group, GameObject red, GameObject amber, GameObject green)> _heads = new List<(int, GameObject, GameObject, GameObject)>();
        private GUIStyle _credit;

        public string DisplayName => resourcePrefix == "wolverhampton" ? "Wolverhampton city centre (beta)" : resourcePrefix == "m54_j2" ? "M54 motorway (beta)" : $"{Built.Pack.DisplayName} (beta)";
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
                double s0 = startOffset;
                if (startAnchor != "start" || startLink.Length == 0)
                {
                    var a = b.Anchor(startAnchor);
                    if (a.HasValue) { lane = a.Value.lane; s0 = a.Value.s; }
                }
                var seg = b.Graph.Segments[lane];
                var (pos, heading) = seg.Path.Sample(System.Math.Min(s0, seg.Length - 1));
                return new CarState { X = pos.X, Z = pos.Z, Heading = heading };
            }
        }

        public double SpeedLimitMph(in CarState p)
        {
            if (_activeLimitMph > 0) return _activeLimitMph;
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
            var pedAsset = Resources.Load<TextAsset>($"Content/{resourcePrefix}_pedestrians");
            if (pedAsset != null)
            {
                _peds = new TownPedestrians(b.Graph, b.LoadPedestrianRoutes(pedAsset.text), 16, seed: 2027);
                _traffic.Pedestrians = _peds;
            }
            if (pedestrianRoot != null) foreach (Transform p in pedestrianRoot) { p.gameObject.SetActive(false); _freePeds.Add(p); }
            _rules = new TownRules(b.Graph, DrivingSide.Left);
            _school = new SchoolSlowRule(LoadSchoolZones());
            _keepLeft = new KeepLeftRule(b.Graph, DrivingSide.Left);
            for (int i = 0; i < b.Signals.GroupCount; i++) _lights.Add(LightState.Red);

            if (signalsRoot != null)
                foreach (var t in signalsRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("SignalHead_g") && int.TryParse(t.name.Substring("SignalHead_g".Length), out int group))
                        _heads.Add((group, Lamp(t, "Red"), Lamp(t, "Amber"), Lamp(t, "Green")));
        }

        /// <summary>School warning signs from the pack's warning_signs.json (none if the pack has no such file).</summary>
        private List<Vec2> LoadSchoolZones()
        {
            var zones = new List<Vec2>();
            var asset = Resources.Load<TextAsset>($"Content/{resourcePrefix}_warning_signs");
            if (asset != null && MiniJson.Parse(asset.text) is Dictionary<string, object> root && root.TryGetValue("warningSigns", out var list))
                foreach (var o in MiniJson.Arr(list))
                {
                    var s = MiniJson.Obj(o);
                    if (s.TryGetValue("kind", out var k) && (string)k == "school")
                        zones.Add(new Vec2(MiniJson.Num(s["x"]), MiniJson.Num(s["z"])));
                }
            return zones;
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
            _peds?.Reset();
            foreach (var kv in _pedViews) { kv.Value.gameObject.SetActive(false); _freePeds.Add(kv.Value); }
            _pedViews.Clear();
            _rules.Reset();
            _school?.Reset();
            _keepLeft.Reset();
            _redXGantry = _redXLane = _limitGantry = _watchGantry = -1;
            _activeLimitMph = 0;
            if (gantryRoot != null)
                foreach (var t in gantryRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "RedX" || t.name == "Limit") t.gameObject.SetActive(false);
            var start = StartState;
            for (int i = 0; i < 50 * 20; i++) { _peds?.Step(0.02, _traffic, start); _traffic.Step(0.02, start, i * 0.02); }
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
            _peds?.Step(dt, _traffic, player);
            _traffic.Step(dt, player, _time);
            UpdateLights();
            GetRoadUsers(_users);
            var frame = new DriverFrame(simTime, player, indicator);
            _rules.OnFrame(frame, _lights, _peds != null ? _peds.People : _noPeople, _users, events);
            _keepLeft.OnFrame(frame, _users, events);
            _school.OnFrame(frame, _peds != null ? _peds.People : _noPeople, events);
            WatchGantry(player, simTime, events);
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
            _pedPoses.Clear();
            if (_peds != null) foreach (var p in _peds.People) _pedPoses.Add((p.Id, p.Position.X, p.Position.Z, p.Heading));
            RenderPedestrians(_pedPoses);
        }

        private void RenderPedestrians(List<(int id, double x, double z, double heading)> people)
        {
            _pedSeen.Clear();
            foreach (var (id, x, z, heading) in people)
            {
                _pedSeen.Add(id);
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
            _pedRelease.Clear();
            foreach (var id in _pedViews.Keys) if (!_pedSeen.Contains(id)) _pedRelease.Add(id);
            foreach (var id in _pedRelease) { _pedViews[id].gameObject.SetActive(false); _freePeds.Add(_pedViews[id]); _pedViews.Remove(id); }
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
            if (_peds != null)
                foreach (var p in _peds.People)
                    actors.Add(new ActorPose { Id = PedIdOffset + p.Id, Kind = ActorKind.Pedestrian, X = (float)p.Position.X, Z = (float)p.Position.Z, Heading = (float)p.Heading });
            lights.Clear();
            lights.AddRange(_lights);
        }

        public void ShowRecorded(RecordedFrame frame, bool blink)
        {
            Replay.Vehicles(frame, _replay);
            if (trafficView != null) trafficView.Render(_replay, 1f, blink);
            if (frame.Lights.Length > 0) ShowLights(frame.Lights);
            _pedPoses.Clear();
            foreach (var a in frame.Actors) if (a.Kind == ActorKind.Pedestrian) _pedPoses.Add((a.Id - PedIdOffset, a.X, a.Z, a.Heading));
            RenderPedestrians(_pedPoses);
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
            if (name == "start" || name == "join" || name == "leave")
            {
                var a = name == "start" ? null : Built.Anchor(name);
                if (a.HasValue) (position, heading) = Built.Graph.Segments[a.Value.lane].Path.Sample(a.Value.s);
                else { var st = StartState; position = new Vec2(st.X, st.Z); heading = st.Heading; }
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

        // ---------------- motorway: gantries, red X, variable limits, slow vehicles, breakdowns ----------------

        public Transform gantryRoot;
        private int _redXGantry = -1, _redXLane = -1, _limitGantry = -1, _limitMph, _activeLimitMph;
        private double _lastGantryAlong = double.NaN;
        private int _watchGantry = -1;

        /// <summary>Lane number from the left (0) of the player's carriageway, or -1.</summary>
        private int LaneIndex(in CarState p)
        {
            var seg = Built.Graph.Nearest(new Vec2(p.X, p.Z), out _, out double d, lanesOnly: true);
            if (seg == null || d > 2.5 || !Built.LinkLanes.TryGetValue(seg.SourceId, out var lanes)) return -1;
            int i = lanes.forward.IndexOf(seg.Id);
            return i >= 0 ? i : lanes.backward.IndexOf(seg.Id);
        }

        /// <summary>The next gantry over the player's carriageway, 50–1500 m ahead.</summary>
        private int GantryAhead(in CarState p)
        {
            var me = new Vec2(p.X, p.Z);
            Vec2 f = Vec2.FromHeading(p.Heading);
            int best = -1;
            double bestAlong = double.MaxValue;
            for (int i = 0; i < Built.Pack.Gantries.Count; i++)
            {
                var (pos, heading, _) = Built.Pack.Gantries[i];
                Vec2 d = pos - me;
                double along = d.Dot(f), side = System.Math.Abs(d.Dot(f.LeftNormal));
                if (along < 50 || along > 1500 || side > 25 || System.Math.Cos(heading - p.Heading) < 0.7) continue;
                if (along < bestAlong) { bestAlong = along; best = i; }
            }
            return best;
        }

        public bool RedX(int lane, in CarState player)
        {
            int g = GantryAhead(player);
            if (g < 0) return false;
            _redXGantry = g; _redXLane = lane; _watchGantry = g; _lastGantryAlong = double.NaN;
            ShowGantry(g);
            return true;
        }

        public bool VariableLimit(int mph, in CarState player)
        {
            int g = GantryAhead(player);
            if (g < 0) return false;
            _limitGantry = g; _limitMph = mph; _watchGantry = g; _lastGantryAlong = double.NaN;
            ShowGantry(g);
            return true;
        }

        public void SlowVehicleAhead(int lane, double speedMph, double offsetM, in CarState player)
        {
            var seg = Built.Graph.Nearest(new Vec2(player.X, player.Z), out double s, out _, lanesOnly: true);
            if (seg == null || !Built.LinkLanes.TryGetValue(seg.SourceId, out var lanes)) return;
            var side = lanes.forward.Contains(seg.Id) ? lanes.forward : lanes.backward;
            int target = side[Mathf.Clamp(lane, 0, side.Count - 1)];
            var t = Built.Graph.Segments[target];
            double at = System.Math.Min(t.Length - 5, s + offsetM);
            if (at < 5) at = t.Length / 2;
            var car = _traffic.Add(target, at, speedMph / 2.236936);
            car.MaxSpeedMps = speedMph / 2.236936;
        }

        public bool SafeStopPlace(in CarState p)
        {
            var me = new Vec2(p.X, p.Z);
            foreach (var ea in Built.Pack.EmergencyAreas) if ((ea - me).Length < 45) return true;
            // Hard shoulder: left of the nearside lane by more than half a lane.
            var seg = Built.Graph.Nearest(me, out double s, out _, lanesOnly: true);
            if (seg == null || !Built.LinkLanes.TryGetValue(seg.SourceId, out var lanes)) return false;
            var side = lanes.forward.Contains(seg.Id) ? lanes.forward : lanes.backward;
            var nearside = Built.Graph.Segments[side[0]];
            var (_, lateral, _) = nearside.Path.Project(me);
            return lateral > Built.LaneWidthM * 0.55;
        }

        /// <summary>Judges driving under a red X (Highway Code rule 258) and switches on a variable limit once passed.</summary>
        private void WatchGantry(in CarState p, double simTime, List<RuleEvent> events)
        {
            if (_watchGantry < 0) return;
            var (pos, heading, _) = Built.Pack.Gantries[_watchGantry];
            double along = (pos - new Vec2(p.X, p.Z)).Dot(Vec2.FromHeading(heading));
            if (!double.IsNaN(_lastGantryAlong) && _lastGantryAlong > 0 && along <= 0)
            {
                if (_watchGantry == _redXGantry && LaneIndex(p) == _redXLane)
                    events.Add(new RuleEvent("GB.MWY.RED_X", RuleOutcome.Violation, Severity.Serious, simTime,
                        "You drove under a red X. That lane is closed. Move to an open lane in good time.", "Highway Code rule 258"));
                if (_watchGantry == _limitGantry) _activeLimitMph = _limitMph;
                _watchGantry = -1;
            }
            _lastGantryAlong = along;
        }

        private void ShowGantry(int g)
        {
            if (gantryRoot == null) return;
            var t = gantryRoot.Find($"Gantry{g}");
            if (t == null) return;
            foreach (Transform panel in t)
            {
                if (!panel.name.StartsWith("Panel")) continue;
                int lane = int.Parse(panel.name.Substring(5));
                var x = panel.Find("RedX");
                var lim = panel.Find("Limit");
                if (x != null) x.gameObject.SetActive(g == _redXGantry && lane == _redXLane);
                if (lim != null)
                {
                    bool showLimit = g == _limitGantry && !(g == _redXGantry && lane == _redXLane);
                    lim.gameObject.SetActive(showLimit);
                    var tm = lim.GetComponentInChildren<TextMesh>(true);
                    if (tm != null) tm.text = _limitMph.ToString();
                }
            }
        }
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
