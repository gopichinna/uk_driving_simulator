using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Practice drive on a 40 mph dual carriageway with traffic, mirrors, indicators,
    /// chase / driver's-seat cameras and an optional mini-map.
    /// The engine-free core (CarModel, TrafficSim) runs at a fixed 50 Hz; Unity only displays it.
    /// </summary>
    [RequireComponent(typeof(TouchControls))]
    public sealed class HelloCarGame : MonoBehaviour
    {
        public Transform car;
        public Camera chaseCamera;
        public TrafficView trafficView;
        public MirrorRig mirrors;
        public GameObject[] playerIndicatorLeft = new GameObject[0];
        public GameObject[] playerIndicatorRight = new GameObject[0];

        [Header("Road (dual carriageway along +Z; we drive north in lanes 0 and 1)")]
        public float startX = -1.825f;
        public float roadEndZ = 1900f;
        public int speedLimitMph = 40;

        [Header("Chase camera")]
        public float camDistance = 7.5f;
        public float camHeight = 3.2f;
        public float camLookAhead = 6f;

        [Header("Driver's seat camera (UK: driver sits on the RIGHT)")]
        public Vector3 cockpitOffset = new Vector3(0.37f, 1.2f, -0.15f);
        public float cockpitPitch = 0.5f;
        public float cockpitFov = 50f;

        [Header("Shoulder check (blind spot)")]
        public float shoulderYaw = 125f;
        public float shoulderPitch = 2f;
        public float shoulderFov = 55f;
        [Tooltip("Shoulder-check viewpoint, just outside each side window, so no interior is drawn.")]
        public Vector3 lookOffsetRight = new Vector3(1.02f, 1.15f, -0.25f);
        public Vector3 lookOffsetLeft = new Vector3(-1.02f, 1.15f, -0.25f);
        public float shoulderTurnSpeed = 6f;

        private enum View { Chase, Cockpit }

        private readonly FixedStepClock _clock = new FixedStepClock();
        private readonly CarParams _params = new CarParams();
        private TouchControls _controls;
        private CarState _prev, _curr;
        private TrafficSim _traffic;

        private View _view = View.Chase;
        private bool _showMap = false;     // mini-map removed from the HUD for now
        private GameObject _interior;
        private float _lookBlend;          // -1..1, eased toward the held look direction
        private GameObject[] _mirrorHousings = new GameObject[0];
        private int _indicator;            // -1 left, 0 off, +1 right
        private float _indicatorStartX;
        private float _indicatorTime;
        private GUIStyle _hudStyle, _signStyle, _buttonStyle, _smallStyle;

        private static readonly StraightLane[] Lanes =
        {
            new StraightLane(-1.825, +1, isNearside: true),   // lane 1 (left) – ours
            new StraightLane(+1.825, +1, isNearside: false),  // lane 2 (overtaking) – ours
            new StraightLane(8.475, -1, isNearside: false),   // oncoming, their lane 2
            new StraightLane(12.125, -1, isNearside: true),   // oncoming, their lane 1
        };

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _controls = GetComponent<TouchControls>();
            var housings = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in car) if (c.name.StartsWith("Mirror")) housings.Add(c.gameObject);
            _mirrorHousings = housings.ToArray();
            var interior = car.Find("Interior");
            if (interior != null) _interior = interior.gameObject;
            _traffic = new TrafficSim(Lanes, new TrafficParams { CarsPerLane = new[] { 5, 3, 4, 4 } }, seed: 2026);
            ResetDrive();
        }

        private void ResetDrive()
        {
            _curr = new CarState { X = startX, Z = 0 };
            _prev = _curr;
            _indicator = 0;
            if (trafficView != null) trafficView.Clear();
            _traffic.Reset(_curr.Z);
        }

        private void Update()
        {
            CarControls input = _controls.Read();
            int steps = _clock.Advance(Time.deltaTime);
            for (int i = 0; i < steps; i++)
            {
                _prev = _curr;
                if (trafficView != null) trafficView.CapturePrevious(_traffic);
                _curr = CarModel.Step(_curr, input, _params, _clock.StepSeconds);
                _traffic.Step(_clock.StepSeconds, _curr);
            }

            if (_curr.Z > roadEndZ || Mathf.Abs((float)_curr.X) > 60f) ResetDrive();

            float a = (float)_clock.Alpha;
            var pos = new Vector3(Mathf.Lerp((float)_prev.X, (float)_curr.X, a), 0f, Mathf.Lerp((float)_prev.Z, (float)_curr.Z, a));
            float yaw = Mathf.LerpAngle((float)(_prev.Heading * Mathf.Rad2Deg), (float)(_curr.Heading * Mathf.Rad2Deg), a);
            car.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            UpdateIndicator();
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            foreach (var l in playerIndicatorLeft) l.SetActive(_indicator < 0 && blink);
            foreach (var l in playerIndicatorRight) l.SetActive(_indicator > 0 && blink);
            if (trafficView != null) trafficView.Render(_traffic, a, blink);
        }

        /// <summary>Self-cancel like a real car: after a lane change (≥ 2.5 m sideways and straight again), or after 15 s.</summary>
        private void UpdateIndicator()
        {
            if (_indicator == 0) return;
            _indicatorTime += Time.deltaTime;
            bool movedOver = Mathf.Abs((float)_curr.X - _indicatorStartX) > 2.5f;
            bool straight = Mathf.Abs((float)_curr.Heading) < 0.05f;
            if ((movedOver && straight) || _indicatorTime > 15f) _indicator = 0;
        }

        private void ToggleIndicator(int side)
        {
            _indicator = _indicator == side ? 0 : side;
            _indicatorStartX = (float)_curr.X;
            _indicatorTime = 0f;
        }

        private void LateUpdate()
        {
            if (chaseCamera == null) return;
            Transform cam = chaseCamera.transform;

            // Door-mirror housings block the view from the driver's seat; the HUD mirrors replace them.
            bool inside = _view == View.Cockpit && Mathf.Abs(_lookBlend) < 0.05f;
            foreach (var h in _mirrorHousings) if (h.activeSelf == (_view == View.Cockpit)) h.SetActive(_view != View.Cockpit);
            if (_interior != null && _interior.activeSelf != inside) _interior.SetActive(inside);

            // 1. Normal pose for the current view
            Vector3 pos;
            Quaternion rot;
            if (_view == View.Cockpit)
            {
                chaseCamera.nearClipPlane = 0.05f;
                chaseCamera.fieldOfView = cockpitFov;
                pos = car.TransformPoint(cockpitOffset);
                rot = car.rotation * Quaternion.Euler(cockpitPitch, 0f, 0f);
            }
            else
            {
                chaseCamera.nearClipPlane = Mathf.Abs(_lookBlend) > 0.05f ? 0.05f : 0.3f;
                chaseCamera.fieldOfView = 60f;
                Vector3 fwd = car.forward;
                Vector3 target = car.position - fwd * camDistance + Vector3.up * camHeight;
                _chasePos = Vector3.Lerp(_chasePos == Vector3.zero ? target : _chasePos, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
                pos = _chasePos;
                rot = Quaternion.LookRotation(car.position + fwd * camLookAhead + Vector3.up * 1.0f - pos);
            }

            // 2. Shoulder check: turn the driver's head toward the blind spot while "Look" is held
            _lookBlend = Mathf.MoveTowards(_lookBlend, _controls.Look, shoulderTurnSpeed * Time.deltaTime);
            if (Mathf.Abs(_lookBlend) > 0.001f)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Abs(_lookBlend));
                bool right = _lookBlend > 0;
                Vector3 headPos = car.TransformPoint(right ? lookOffsetRight : lookOffsetLeft);
                Quaternion headRot = car.rotation * Quaternion.Euler(shoulderPitch, right ? shoulderYaw : -shoulderYaw, 0f);
                pos = Vector3.Lerp(pos, headPos, t);
                rot = Quaternion.Slerp(rot, headRot, t);
                chaseCamera.fieldOfView = Mathf.Lerp(chaseCamera.fieldOfView, shoulderFov, t);
            }

            cam.SetPositionAndRotation(pos, rot);
        }

        private Vector3 _chasePos;

        // ---------------- HUD ----------------

        private void OnGUI()
        {
            float W = Screen.width, H = Screen.height;
            _hudStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _signStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _buttonStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _smallStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
            _hudStyle.fontSize = Mathf.RoundToInt(H * 0.06f);
            _signStyle.fontSize = Mathf.RoundToInt(H * 0.05f);
            _buttonStyle.fontSize = Mathf.RoundToInt(H * 0.04f);
            _smallStyle.fontSize = Mathf.RoundToInt(H * 0.03f);

            // Speed
            GUI.Box(new Rect(W * 0.5f - H * 0.16f, H * 0.84f, H * 0.32f, H * 0.13f), $"{_curr.SpeedMph:0} mph", _hudStyle);

            // Speed limit (below the nearside mirror)
            GUI.color = _curr.SpeedMph > speedLimitMph + 0.5 ? new Color(1f, 0.5f, 0.5f) : Color.white;
            GUI.Box(new Rect(W * 0.02f, H * 0.47f, H * 0.14f, H * 0.14f), speedLimitMph.ToString(), _signStyle);
            GUI.color = Color.white;

            // Indicators either side of the rear-view mirror
            float iw = W * 0.075f, ih = H * 0.12f, iy = H * 0.03f;
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            GUI.color = _indicator < 0 && blink ? new Color(0.4f, 1f, 0.45f) : Color.white;
            if (GUI.Button(new Rect(W * 0.34f - iw - W * 0.01f, iy, iw, ih), "<<", _buttonStyle)) ToggleIndicator(-1);
            GUI.color = _indicator > 0 && blink ? new Color(0.4f, 1f, 0.45f) : Color.white;
            if (GUI.Button(new Rect(W * 0.66f + W * 0.01f, iy, iw, ih), ">>", _buttonStyle)) ToggleIndicator(+1);
            GUI.color = Color.white;

            // View / map / reset under the rear-view mirror
            float rearMirrorBottom = H * 0.015f + W * 0.30f / 3.4f + 6f; // matches MirrorRig layout
            float bw = W * 0.085f, bh = H * 0.085f, by = rearMirrorBottom + H * 0.02f;
            if (GUI.Button(new Rect(W * 0.5f - bw * 1.03f, by, bw, bh), _view == View.Chase ? "Seat" : "Chase", _buttonStyle))
                _view = _view == View.Chase ? View.Cockpit : View.Chase;
            if (GUI.Button(new Rect(W * 0.5f + bw * 0.03f, by, bw, bh), "Reset", _buttonStyle))
                ResetDrive();

            if (Mathf.Abs(_lookBlend) > 0.6f) DrawWindowFrame(W, H, Mathf.Abs(_lookBlend));
            if (Mathf.Abs(_lookBlend) > 0.6f)
                GUI.Box(new Rect(W * 0.5f - H * 0.4f, H * 0.62f, H * 0.8f, H * 0.1f),
                    _lookBlend < 0 ? "Checking left blind spot" : "Checking right blind spot", _buttonStyle);

            if (_showMap) DrawMiniMap(new Rect(W * 0.30f, H * 0.50f, W * 0.11f, H * 0.46f));
        }

        /// <summary>Top-down radar of nearby traffic: 40 m behind to 70 m ahead. A learning assist (off in test mode later).</summary>
        private void DrawMiniMap(Rect r)
        {
            const float behindM = 40f, aheadM = 70f, leftM = -4.5f, rightM = 14f;
            float pxPerM = r.height / (behindM + aheadM);
            float sx = r.width / (rightM - leftM);
            float zToY(double z) => r.yMax - ((float)(z - _curr.Z) + behindM) * pxPerM;
            float xToX(double x) => r.x + ((float)x - leftM) * sx;

            Fill(r, new Color(0.15f, 0.16f, 0.18f, 0.8f));
            Fill(new Rect(xToX(-3.65f), r.y, xToX(3.65f) - xToX(-3.65f), r.height), new Color(0.35f, 0.36f, 0.4f, 0.9f));
            Fill(new Rect(xToX(6.65f), r.y, xToX(13.95f) - xToX(6.65f), r.height), new Color(0.35f, 0.36f, 0.4f, 0.9f));
            Fill(new Rect(xToX(-0.05f), r.y, 2f, r.height), new Color(1f, 1f, 1f, 0.5f));

            float carW = 1.8f * sx, carL = 4.3f * pxPerM;
            foreach (var c in _traffic.Cars)
            {
                float y = zToY(c.Z);
                if (y < r.y || y > r.yMax) continue;
                bool oncoming = _traffic.Lanes[c.Lane].Direction < 0;
                Fill(new Rect(xToX(c.X) - carW / 2, y - carL / 2, carW, carL),
                    oncoming ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.95f, 0.45f, 0.35f));
            }
            Fill(new Rect(xToX(_curr.X) - carW / 2, zToY(_curr.Z) - carL / 2, carW, carL), new Color(0.3f, 0.6f, 1f));
            GUI.Label(new Rect(r.x, r.yMax - _smallStyle.fontSize * 1.4f, r.width, _smallStyle.fontSize * 1.4f), "map", _smallStyle);
        }

        /// <summary>Dark border so the shoulder-check view reads as "looking out of the side window".</summary>
        private static void DrawWindowFrame(float W, float H, float strength)
        {
            var c = new Color(0.08f, 0.09f, 0.11f, 0.85f * Mathf.Clamp01((strength - 0.6f) / 0.4f));
            float top = H * 0.30f, bottom = H * 0.22f, side = W * 0.04f;
            Fill(new Rect(0, 0, W, top), c);
            Fill(new Rect(0, H - bottom, W, bottom), c);
            Fill(new Rect(0, top, side, H - top - bottom), c);
            Fill(new Rect(W - side, top, side, H - top - bottom), c);
        }

        private static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
