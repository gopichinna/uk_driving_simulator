using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Prototype on-screen controls (landscape):
    ///   bottom-left  – a fixed steering wheel: put a thumb on it and turn it (it springs back to centre)
    ///   above wheel  – the left indicator; above the brake – the right indicator (both drawn by DriveGame)
    ///   bottom-right – two pedals: the wide BRAKE, and the tall GAS beside it
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

        /// <summary>How much the steering help is adding right now (smoothed), for the on-screen "Assist" marker.</summary>
        public float AssistAmount;

        public CarControls Read() => new CarControls(_steer, _go ? 1 : 0, _brake ? 1 : 0);

        /// <summary>Shoulder check being held: -1 left, +1 right, 0 none.</summary>
        public int Look => _look;

        // Under each door mirror (mirrors are drawn by MirrorRig at the top corners).
        private static float MirrorBottom => Screen.height * 0.04f + Screen.width * 0.17f / 1.55f;
        public static Rect LookLeftRect => new Rect(Screen.width * 0.015f, MirrorBottom + Screen.height * 0.025f, Screen.width * 0.17f, Screen.height * 0.11f);
        public static Rect LookRightRect => new Rect(Screen.width * 0.815f, MirrorBottom + Screen.height * 0.025f, Screen.width * 0.17f, Screen.height * 0.11f);

        // The wheel never moves: lower left, a little in from the edge. The left indicator sits right above it.
        public static Vector2 WheelCentre => new Vector2(Screen.width * 0.20f, Screen.height * 0.79f);  // GUI coordinates (y down)
        public static float WheelRadius => Screen.height * 0.15f;
        // Both indicators sit above the brake, side by side, where the right thumb already is.
        public static Rect IndicatorLeftRect => new Rect(Screen.width * 0.70f, Screen.height * 0.47f, Screen.width * 0.07f, Screen.height * 0.13f);

        // Pedals, like a real car: the wide brake on the left, the tall gas on the right, a gap between them.
        // The right indicator sits right above the brake.
        private static Rect GasRect => new Rect(Screen.width * 0.87f, Screen.height * 0.50f, Screen.width * 0.11f, Screen.height * 0.45f);
        private static Rect BrakeRect => new Rect(Screen.width * 0.72f, Screen.height * 0.68f, Screen.width * 0.12f, Screen.height * 0.27f);
        public static Rect IndicatorRightRect => new Rect(Screen.width * 0.775f, Screen.height * 0.47f, Screen.width * 0.07f, Screen.height * 0.13f);

        private int _indicatorTap;
        /// <summary>A tap on an indicator button since last asked: -1 left, +1 right, 0 none. Read from the raw touches so a tap works while the other thumb is steering.</summary>
        public int ConsumeIndicatorTap() { int t = _indicatorTap; _indicatorTap = 0; return t; }

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

                if (t.phase == TouchPhase.Began)
                {
                    if (IndicatorLeftRect.Contains(gui)) { _indicatorTap = -1; continue; }
                    if (IndicatorRightRect.Contains(gui)) { _indicatorTap = 1; continue; }
                }
                if (LookLeftRect.Contains(gui)) { _look = -1; continue; }
                if (LookRightRect.Contains(gui)) { _look = 1; continue; }
                if (GasRect.Contains(gui)) { _go = true; continue; }
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

            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))   // mouse, in the editor
            {
                var m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (IndicatorLeftRect.Contains(m)) _indicatorTap = -1;
                else if (IndicatorRightRect.Contains(m)) _indicatorTap = 1;
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

            DrawPedal(GasRect, "GAS", _go, new Color(0.35f, 0.85f, 0.45f), style);
            DrawPedal(BrakeRect, "BRAKE", _brake, new Color(0.95f, 0.4f, 0.4f), style);

            // The wheel: fixed in place, turning with your thumb (or with the keyboard in the editor).
            if (s_wheel == null) s_wheel = MakeWheelTexture();
            Vector2 c = WheelCentre;
            float r = WheelRadius;
            GUI.color = _steerFingerId >= 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.8f);
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(_steer / SensitivityFactor * fullLockDegrees, c);   // your wheel, as you turn it
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2, r * 2), s_wheel);
            GUI.matrix = old;

            // "Assist" marker beside the wheel: lights up while the game is helping hold your line.
            float glow = Mathf.Clamp01(AssistAmount * 6f);
            if (glow > 0.05f)
            {
                var tag = new GUIStyle(style) { fontSize = Mathf.RoundToInt(Screen.height * 0.034f) };
                GUI.color = new Color(0.4f, 1f, 0.5f, 0.35f + 0.65f * glow);
                GUI.Box(new Rect(c.x - r - Screen.width * 0.115f, c.y - Screen.height * 0.03f, Screen.width * 0.1f, Screen.height * 0.06f), "ASSIST", tag);
            }
            GUI.color = Color.white;
        }

        private static Texture2D s_pedal;

        /// <summary>A pedal: a rounded rubber pad with ridges across it; it sinks and lights up while pressed.</summary>
        private static void DrawPedal(Rect r, string label, bool pressed, Color tint, GUIStyle style)
        {
            if (s_pedal == null) s_pedal = MakePedalTexture();
            if (pressed) r = new Rect(r.x + r.width * 0.03f, r.y + r.height * 0.02f, r.width * 0.94f, r.height * 0.96f);
            GUI.color = pressed ? tint : Color.Lerp(Color.white, tint, 0.35f) * new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(r, s_pedal);
            GUI.color = pressed ? Color.black : new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.Label(r, label, style);
        }

        private static Texture2D MakePedalTexture()
        {
            const int W = 128, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            const float corner = 28f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float cx = Mathf.Max(corner - x, 0f, x - (W - 1 - corner)), cy = Mathf.Max(corner - y, 0f, y - (H - 1 - corner));
                    float d = Mathf.Sqrt(cx * cx + cy * cy);
                    if (d > corner) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    float edge = Mathf.Clamp01((corner - d) / 3f);                     // soft rounded edge
                    float ridge = (y % 22) < 5 ? 0.78f : 1f;                              // anti-slip ridges
                    float shade = (0.78f + 0.22f * (x / (float)W)) * ridge;               // light from the left
                    px[y * W + x] = new Color(shade, shade, shade, edge);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
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
