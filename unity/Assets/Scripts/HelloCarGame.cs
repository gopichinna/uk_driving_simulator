using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Setup milestone "hello car": drives the engine-free CarModel at a fixed 50 Hz
    /// and shows it with render interpolation and a chase camera. Unity only displays
    /// what the simulation decides (docs/04-architecture.md, principle 1).
    /// </summary>
    [RequireComponent(typeof(TouchControls))]
    public sealed class HelloCarGame : MonoBehaviour
    {
        public Transform car;
        public Camera chaseCamera;

        [Header("Start position: left lane of the road (UK keeps left)")]
        public float startX = -1.8f;
        public float startZ = 0f;
        public float roadEndZ = 1000f;

        [Header("Chase camera")]
        public float camDistance = 7.5f;
        public float camHeight = 3.2f;
        public float camLookAhead = 6f;

        private readonly FixedStepClock _clock = new FixedStepClock();
        private readonly CarParams _params = new CarParams();
        private TouchControls _controls;
        private CarState _prev, _curr;
        private GUIStyle _hudStyle, _signStyle;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _controls = GetComponent<TouchControls>();
            ResetCar();
        }

        private void ResetCar()
        {
            _curr = new CarState { X = startX, Z = startZ };
            _prev = _curr;
        }

        private void Update()
        {
            CarControls input = _controls.Read();
            int steps = _clock.Advance(Time.deltaTime);
            for (int i = 0; i < steps; i++)
            {
                _prev = _curr;
                _curr = CarModel.Step(_curr, input, _params, _clock.StepSeconds);
            }

            if (_curr.Z > roadEndZ || Mathf.Abs((float)_curr.X) > 400f) ResetCar();

            float a = (float)_clock.Alpha;
            var pos = new Vector3(
                Mathf.Lerp((float)_prev.X, (float)_curr.X, a),
                0f,
                Mathf.Lerp((float)_prev.Z, (float)_curr.Z, a));
            float yaw = Mathf.LerpAngle((float)(_prev.Heading * Mathf.Rad2Deg), (float)(_curr.Heading * Mathf.Rad2Deg), a);
            car.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        private void LateUpdate()
        {
            if (chaseCamera == null) return;
            Vector3 fwd = car.forward;
            Vector3 target = car.position - fwd * camDistance + Vector3.up * camHeight;
            Transform cam = chaseCamera.transform;
            cam.position = Vector3.Lerp(cam.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            cam.LookAt(car.position + fwd * camLookAhead + Vector3.up * 1.0f);
        }

        private void OnGUI()
        {
            _hudStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _signStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _hudStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.06f);
            _signStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.05f);

            float h = Screen.height;
            GUI.Box(new Rect(Screen.width * 0.5f - h * 0.16f, h * 0.84f, h * 0.32f, h * 0.13f),
                $"{_curr.SpeedMph:0} mph", _hudStyle);

            GUI.color = _curr.SpeedMph > 30.5 ? new Color(1f, 0.5f, 0.5f) : Color.white;
            GUI.Box(new Rect(h * 0.04f, h * 0.04f, h * 0.15f, h * 0.15f), "30", _signStyle);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(Screen.width - h * 0.3f, h * 0.04f, h * 0.26f, h * 0.1f), "Reset")) ResetCar();
        }
    }
}
