using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LToLocal.Game
{
    /// <summary>
    /// The player's drive: car model at a fixed 50 Hz, indicators, chase / driver's-seat cameras,
    /// optional shoulder-check view, the speed rule and HUD. The road, traffic and course-specific
    /// rules come from the <see cref="ICourse"/> component on the same GameObject.
    /// Unity only displays what the engine-free core decides (docs/04 principle 1).
    /// </summary>
    [RequireComponent(typeof(TouchControls))]
    public sealed partial class DriveGame : MonoBehaviour
    {
        public Transform car;
        public Camera chaseCamera;
        public MirrorRig mirrors;
        public FeedbackView feedback;
        public GameObject[] playerIndicatorLeft = new GameObject[0];
        public GameObject[] playerIndicatorRight = new GameObject[0];

        [Header("Chase camera")]
        public float camDistance = 7.5f;
        public float camHeight = 3.2f;
        public float camLookAhead = 6f;

        [Header("Driver's seat camera (UK: driver sits on the RIGHT)")]
        public Vector3 cockpitOffset = new Vector3(0.37f, 1.2f, -0.15f);
        public float cockpitPitch = 0.5f;
        public float cockpitFov = 50f;

        [Header("Optional look over the shoulder (not marked)")]
        public float shoulderYaw = 125f;
        public float shoulderPitch = 2f;
        public float shoulderFov = 55f;
        public Vector3 lookOffsetRight = new Vector3(1.02f, 1.15f, -0.25f);
        public Vector3 lookOffsetLeft = new Vector3(-1.02f, 1.15f, -0.25f);
        public float shoulderTurnSpeed = 6f;

        private enum View { Chase, Cockpit }
        private static View s_view = View.Chase; // remembered across course changes

        private readonly FixedStepClock _clock = new FixedStepClock();
        private readonly CarParams _params = new CarParams();
        private readonly SpeedRule _speedRule = new SpeedRule();
        private readonly CyclistPassingRule _cyclistRule = new CyclistPassingRule();
        private readonly FollowingDistanceRule _gapRule = new FollowingDistanceRule();
        private readonly List<RoadUser> _roadUsers = new List<RoadUser>();
        private readonly List<RuleEvent> _events = new List<RuleEvent>();
        private ICourse _course;
        private TouchControls _controls;
        private CarState _prev, _curr;

        private int _indicator;
        private float _indicatorStartX, _indicatorStartHeading, _indicatorTime;
        private float _lookBlend;
        private Vector3 _chasePos;
        private GameObject[] _mirrorHousings = new GameObject[0];
        private GameObject _interior;
        private GUIStyle _hudStyle, _signStyle, _buttonStyle, _titleStyle;
        private float _titleUntil;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _controls = GetComponent<TouchControls>();
            _course = GetComponent<ICourse>();

            var housings = new List<GameObject>();
            foreach (Transform c in car) if (c.name.StartsWith("Mirror")) housings.Add(c.gameObject);
            _mirrorHousings = housings.ToArray();
            var interior = car.Find("Interior");
            if (interior != null) _interior = interior.gameObject;
            SetUpAudio();
        }

        private void Start()
        {
            PrepareLessonStart();
            ResetDrive();
            if (LessonSession.OpenMenuOnLoad) { LessonSession.OpenMenuOnLoad = false; OpenLessonMenu(); }
        }

        private void ResetDrive()
        {
            _curr = _course.StartState;
            _prev = _curr;
            _indicator = 0;
            _speedRule.Reset();
            _cyclistRule.Reset();
            _gapRule.Reset();
            _course.ResetCourse();
            if (feedback != null) feedback.ResetTally();
            _chasePos = Vector3.zero;
            _titleUntil = Time.time + 3f;
            ResetRecording();
            StartLessonPart();
        }

        private void Update()
        {
            if (_mode != Mode.Driving) { UpdatePaused(); return; }
            CarControls input = _controls.Read();
            if (BrokenDown) input = new CarControls(input.Steer, 0, input.Brake); // breakdown scenario: no power
            int steps = _clock.Advance(Time.deltaTime);
            for (int i = 0; i < steps; i++)
            {
                _prev = _curr;
                _course.CapturePrevious();
                _curr = CarModel.Step(_curr, input, _params, _clock.StepSeconds);

                _events.Clear();
                _course.Step(_clock.StepSeconds, _curr, _clock.SimTime, _indicator, _events);
                var frame = new DriverFrame(_clock.SimTime, _curr, _indicator);
                _speedRule.OnFrame(frame, _course.SpeedLimitMph(_curr), _events);
                _course.GetRoadUsers(_roadUsers);
                _cyclistRule.OnFrame(frame, _roadUsers, _events);
                _gapRule.OnFrame(frame, _roadUsers, _events);
                if (feedback != null) foreach (var e in _events) feedback.Push(e);
                foreach (var e in _events) RuleAudio(e);
                _driveEvents.AddRange(_events);
                RecordStep();
                LessonStep(_clock.StepSeconds);
                if (_mode != Mode.Driving) break;
            }

            if (_course.OutOfBounds(_curr)) ResetDrive();

            float a = (float)_clock.Alpha;
            var pos = new Vector3(Mathf.Lerp((float)_prev.X, (float)_curr.X, a), 0f, Mathf.Lerp((float)_prev.Z, (float)_curr.Z, a));
            float yaw = Mathf.LerpAngle((float)(_prev.Heading * Mathf.Rad2Deg), (float)(_curr.Heading * Mathf.Rad2Deg), a);
            car.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            UpdateIndicator();
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            IndicatorAudio(blink);
            EngineAudio((float)_curr.SpeedMph, (float)input.Throttle);
            foreach (var l in playerIndicatorLeft) l.SetActive(_indicator < 0 && blink);
            foreach (var l in playerIndicatorRight) l.SetActive(_indicator > 0 && blink);
            _course.Render(a, blink);
        }

        /// <summary>
        /// Self-cancel like a real car: after the steering returns from a turn (heading changed by more than 30°
        /// and is now steady), after a lane change (≥ 2.5 m sideways and straight again), or after 20 s.
        /// </summary>
        private void UpdateIndicator()
        {
            if (_indicator == 0) return;
            _indicatorTime += Time.deltaTime;
            float turned = Mathf.Abs(Mathf.DeltaAngle(_indicatorStartHeading, (float)(_curr.Heading * Mathf.Rad2Deg)));
            bool steady = Mathf.Abs((float)_curr.SteerAngleRad) < 0.04f;
            bool movedOver = Mathf.Abs((float)_curr.X - _indicatorStartX) > 2.5f && Mathf.Abs((float)_curr.Heading) < 0.05f && turned < 10f;
            if ((turned > 30f && steady) || movedOver || _indicatorTime > 20f) _indicator = 0;
        }

        private void ToggleIndicator(int side)
        {
            _indicator = _indicator == side ? 0 : side;
            _indicatorStartX = (float)_curr.X;
            _indicatorStartHeading = (float)(_curr.Heading * Mathf.Rad2Deg);
            _indicatorTime = 0f;
        }

        // ---------------- cameras ----------------

        private void LateUpdate()
        {
            if (chaseCamera == null) return;
            if (_mode == Mode.Replay) { ReplayCamera(); return; }

            bool inside = s_view == View.Cockpit && Mathf.Abs(_lookBlend) < 0.05f;
            foreach (var h in _mirrorHousings) if (h.activeSelf == (s_view == View.Cockpit)) h.SetActive(s_view != View.Cockpit);
            if (_interior != null && _interior.activeSelf != inside) _interior.SetActive(inside);

            Vector3 pos;
            Quaternion rot;
            if (s_view == View.Cockpit)
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
                _chasePos = _chasePos == Vector3.zero ? target : Vector3.Lerp(_chasePos, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
                pos = _chasePos;
                rot = Quaternion.LookRotation(car.position + fwd * camLookAhead + Vector3.up * 1.0f - pos);
            }

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

            chaseCamera.transform.SetPositionAndRotation(pos, rot);
        }

        // ---------------- HUD ----------------

        private void OnGUI()
        {
            float W = Screen.width, H = Screen.height;
            _hudStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _signStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _buttonStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            _titleStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _hudStyle.fontSize = Mathf.RoundToInt(H * 0.06f);
            _signStyle.fontSize = Mathf.RoundToInt(H * 0.05f);
            _buttonStyle.fontSize = Mathf.RoundToInt(H * 0.03f);
            _titleStyle.fontSize = Mathf.RoundToInt(H * 0.045f);
            if (_mode != Mode.Driving) { DrawOverlay(W, H); return; }

            // Speed
            GUI.Box(new Rect(W * 0.5f - H * 0.16f, H * 0.84f, H * 0.32f, H * 0.13f), $"{_curr.SpeedMph:0} mph", _hudStyle);

            // Speed limit (below the nearside mirror)
            double limit = _course.SpeedLimitMph(_curr);
            GUI.color = _curr.SpeedMph > limit + 0.5 ? new Color(1f, 0.5f, 0.5f) : Color.white;
            GUI.Box(new Rect(W * 0.02f, H * 0.47f, H * 0.14f, H * 0.14f), limit.ToString("0"), _signStyle);
            GUI.color = Color.white;

            // Indicators either side of the rear-view mirror
            float iw = W * 0.075f, ih = H * 0.12f, iy = H * 0.03f;
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            GUI.color = _indicator < 0 && blink ? new Color(0.4f, 1f, 0.45f) : Color.white;
            if (GUI.Button(new Rect(W * 0.34f - iw - W * 0.01f, iy, iw, ih), "<<", _buttonStyle)) ToggleIndicator(-1);
            GUI.color = _indicator > 0 && blink ? new Color(0.4f, 1f, 0.45f) : Color.white;
            if (GUI.Button(new Rect(W * 0.66f + W * 0.01f, iy, iw, ih), ">>", _buttonStyle)) ToggleIndicator(+1);
            GUI.color = Color.white;

            // View / reset / course / finish / settings under the rear-view mirror
            float rearMirrorBottom = H * 0.015f + W * 0.30f / 3.4f + 6f; // matches MirrorRig layout
            float bw = W * 0.082f, bh = H * 0.085f, by = rearMirrorBottom + H * 0.02f, gap = bw * 1.06f;
            bw = W * 0.072f; gap = bw * 1.06f;
            Rect Slot(int i) => new Rect(W * 0.5f + (i - 2.5f) * gap - bw / 2, by, bw, bh);
            if (GUI.Button(Slot(0), s_view == View.Chase ? Ui.Get("hud.view_seat", "Seat") : Ui.Get("hud.view_chase", "Chase"), _buttonStyle))
                s_view = s_view == View.Chase ? View.Cockpit : View.Chase;
            if (GUI.Button(Slot(1), Ui.Get("hud.reset", "Reset"), _buttonStyle))
                ResetDrive();
            if (GUI.Button(Slot(2), Ui.Get("hud.course", "Course"), _buttonStyle))
                NextCourse();
            if (GUI.Button(Slot(3), "Lessons", _buttonStyle))
                OpenLessonMenu();
            if (GUI.Button(Slot(4), Ui.Get("hud.finish_drive", "Finish"), _buttonStyle))
                if (!FinishLessonPart()) OpenDebrief();
            if (GUI.Button(Slot(5), Ui.Get("hud.settings", "Settings"), _buttonStyle))
                SetMode(Mode.Settings);
            DrawLessonHud(W, H);

            if (Time.time < _titleUntil)
                GUI.Box(new Rect(W * 0.5f - H * 0.5f, H * 0.62f, H * 1.0f, H * 0.1f), _course.DisplayName, _titleStyle);

            if (Mathf.Abs(_lookBlend) > 0.6f) DrawWindowFrame(W, H, Mathf.Abs(_lookBlend));
        }

        private static void NextCourse()
        {
            int count = SceneManager.sceneCountInBuildSettings;
            int next = (SceneManager.GetActiveScene().buildIndex + 1) % Mathf.Max(1, count);
            SceneManager.LoadScene(next);
        }

        /// <summary>Dark border so the shoulder view reads as "looking out of the side window".</summary>
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
