using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Prototype on-screen controls (landscape):
    ///   bottom-left  – a fixed steering wheel: put a thumb on it and turn it (it springs back to centre)
    ///   above wheel  – two big indicator buttons, drawn by DriveGame at IndicatorLeftRect / IndicatorRightRect
    ///   bottom-right – hold GO to accelerate, BRAKE to slow down
    /// Keyboard fallback for the Unity editor: arrow keys or WASD.
    /// Drawn with IMGUI for now; replaced by the real HUD in the vertical slice.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [Tooltip("How far (degrees) you turn the wheel for full lock at the default sensitivity.")]
        public float fullLockDegrees = 80f;

        /// <summary>
        /// Steering sensitivity, 0.3–1.5 (1 = original). Default 0.6 after play-testing (steering felt too fast).
        /// Saved on the device; a Settings screen will expose it.
        /// </summary>
        public static float SteeringSensitivity
        {
            get => PlayerPrefs.GetFloat("steering_sensitivity", 0.6f);
            set { PlayerPrefs.SetFloat("steering_sensitivity", Mathf.Clamp(value, 0.3f, 1.5f)); PlayerPrefs.Save(); }
        }

        [Tooltip("How fast steering returns to centre when you let go (per second).")]
        public float selfCentreRate = 3f;

        private int _steerFingerId = -1;
        private float _grabAngle, _wheelDeg;
        private float _steer;
        private static Texture2D s_wheel;
        private bool _go, _brake;
        private int _look; // -1 over left shoulder, +1 over right shoulder, 0 ahead

        public CarControls Read() => new CarControls(_steer, _go ? 1 : 0, _brake ? 1 : 0);

        /// <summary>Shoulder check being held: -1 left, +1 right, 0 none.</summary>
        public int Look => _look;

        // Under each door mirror (mirrors are drawn by MirrorRig at the top corners).
        private static float MirrorBottom => Screen.height * 0.04f + Screen.width * 0.17f / 1.55f;
        public static Rect LookLeftRect => new Rect(Screen.width * 0.015f, MirrorBottom + Screen.height * 0.025f, Screen.width * 0.17f, Screen.height * 0.11f);
        public static Rect LookRightRect => new Rect(Screen.width * 0.815f, MirrorBottom + Screen.height * 0.025f, Screen.width * 0.17f, Screen.height * 0.11f);

        // The wheel never moves: bottom-left, where the left thumb rests. Indicators sit right above it.
        public static Vector2 WheelCentre => new Vector2(Screen.width * 0.13f, Screen.height * 0.79f);  // GUI coordinates (y down)
        public static float WheelRadius => Screen.height * 0.15f;
        public static Rect IndicatorLeftRect => new Rect(Screen.width * 0.03f, Screen.height * 0.46f, Screen.width * 0.075f, Screen.height * 0.13f);
        public static Rect IndicatorRightRect => new Rect(Screen.width * 0.115f, Screen.height * 0.46f, Screen.width * 0.075f, Screen.height * 0.13f);

        // Pedals: bigger and further apart so the thumb can't hit the wrong one.
        private Rect GoRect => new Rect(Screen.width * 0.86f, Screen.height * 0.45f, Screen.width * 0.12f, Screen.height * 0.50f);
        private Rect BrakeRect => new Rect(Screen.width * 0.70f, Screen.height * 0.62f, Screen.width * 0.12f, Screen.height * 0.33f);

        private static float AngleAround(Vector2 centre, Vector2 p) => Mathf.Atan2(p.x - centre.x, -(p.y - centre.y)) * Mathf.Rad2Deg; // 0 = straight up, + = clockwise
        private static float SensitivityFactor => Mathf.Clamp(SteeringSensitivity / 0.6f, 0.5f, 2.5f);

        private void Update()
        {
            bool steering = false;
            _go = false;
            _brake = false;
            _look = 0;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector2 gui = new Vector2(t.position.x, Screen.height - t.position.y); // IMGUI has y pointing down

                if (LookLeftRect.Contains(gui)) { _look = -1; continue; }
                if (LookRightRect.Contains(gui)) { _look = 1; continue; }
                if (GoRect.Contains(gui)) { _go = true; continue; }
                if (BrakeRect.Contains(gui)) { _brake = true; continue; }

                // Steering wheel: a finger that lands on the wheel grabs it, and turning it steers.
                Vector2 centre = WheelCentre;
                if (t.phase == TouchPhase.Began && _steerFingerId < 0 && Vector2.Distance(gui, centre) <= WheelRadius * 1.3f)
                {
                    _steerFingerId = t.fingerId;
                    _grabAngle = AngleAround(centre, gui);
                    _wheelDeg = 0f;
                }

                if (t.fingerId == _steerFingerId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        _steerFingerId = -1;
                    }
                    else
                    {
                        if (Vector2.Distance(gui, centre) > WheelRadius * 0.2f)   // near the hub the angle is jumpy: hold the last one
                            _wheelDeg = Mathf.Clamp(Mathf.DeltaAngle(_grabAngle, AngleAround(centre, gui)), -fullLockDegrees, fullLockDegrees);
                        _steer = Mathf.Clamp(_wheelDeg / fullLockDegrees * SensitivityFactor, -1f, 1f);
                        steering = true;
                    }
                }
            }

            // Keyboard (editor / desktop testing)
            float key = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) key -= 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) key += 1;
            if (key != 0) { _steer = Mathf.MoveTowards(_steer, key, 3f * SteeringSensitivity * Time.deltaTime); steering = true; }
            if (Input.GetKey(KeyCode.Q)) _look = -1;
            if (Input.GetKey(KeyCode.E)) _look = 1;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) _go = true;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space)) _brake = true;

            if (!steering) _steer = Mathf.MoveTowards(_steer, 0f, selfCentreRate * Time.deltaTime);
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.045f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };

            var lookStyle = new GUIStyle(style) { fontSize = Mathf.RoundToInt(Screen.height * 0.035f) };
            GUI.color = _look < 0 ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(LookLeftRect, "Look left", lookStyle);
            GUI.color = _look > 0 ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(LookRightRect, "Look right", lookStyle);

            GUI.color = _go ? new Color(0.5f, 1f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(GoRect, "GO", style);
            GUI.color = _brake ? new Color(1f, 0.55f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(BrakeRect, "BRAKE", style);

            // The wheel: fixed in place, turning with your thumb (or with the keyboard in the editor).
            if (s_wheel == null) s_wheel = MakeWheelTexture();
            Vector2 c = WheelCentre;
            float r = WheelRadius;
            GUI.color = _steerFingerId >= 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.8f);
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(_steer / SensitivityFactor * fullLockDegrees, c);
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2, r * 2), s_wheel);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        /// <summary>A steering wheel picture: dark rim, three spokes, a hub and a yellow mark at the top so you can see it turn.</summary>
        private static Texture2D MakeWheelTexture()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2f - 1f, ny = (y + 0.5f) / N * 2f - 1f;   // ny up
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = 0f; Color col = new Color(0.12f, 0.12f, 0.14f);
                    bool rim = d > 0.80f && d < 0.99f;
                    bool hub = d < 0.20f;
                    bool spoke = d < 0.85f && ((Mathf.Abs(ny) < 0.07f) || (nx > -0.07f && nx < 0.07f && ny < 0f));   // left, right and bottom spokes
                    if (rim || hub || spoke) a = 0.92f;
                    if (d > 0.97f && d < 0.99f) a *= 0.6f;                                        // soft outer edge
                    if (rim && ny > 0f && Mathf.Abs(nx) < 0.07f) col = new Color(1f, 0.85f, 0.2f);   // top marker
                    px[y * N + x] = new Color(col.r, col.g, col.b, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
