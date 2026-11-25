using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Prototype on-screen controls (landscape):
    ///   left side  – drag sideways anywhere to steer (a wheel appears under your thumb)
    ///   right side – hold GO to accelerate, BRAKE to slow down
    /// Keyboard fallback for the Unity editor: arrow keys or WASD.
    /// Drawn with IMGUI for now; replaced by the real HUD in the vertical slice.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [Tooltip("Sideways drag, as a fraction of screen width, that gives full steering lock.")]
        public float fullLockDragFraction = 0.12f;

        [Tooltip("How fast steering returns to centre when you let go (per second).")]
        public float selfCentreRate = 3f;

        private int _steerFingerId = -1;
        private Vector2 _steerAnchor;
        private float _steer;
        private bool _go, _brake;

        public CarControls Read() => new CarControls(_steer, _go ? 1 : 0, _brake ? 1 : 0);

        private Rect GoRect => new Rect(Screen.width * 0.86f, Screen.height * 0.45f, Screen.width * 0.12f, Screen.height * 0.45f);
        private Rect BrakeRect => new Rect(Screen.width * 0.72f, Screen.height * 0.62f, Screen.width * 0.12f, Screen.height * 0.28f);

        private void Update()
        {
            bool steering = false;
            _go = false;
            _brake = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector2 gui = new Vector2(t.position.x, Screen.height - t.position.y); // IMGUI has y pointing down

                if (GoRect.Contains(gui)) { _go = true; continue; }
                if (BrakeRect.Contains(gui)) { _brake = true; continue; }

                // Left half, below the mirrors/buttons strip (touch y is measured from the bottom).
                bool leftHalf = t.position.x < Screen.width * 0.5f && t.position.y < Screen.height * 0.62f;
                if (t.phase == TouchPhase.Began && leftHalf && _steerFingerId < 0)
                {
                    _steerFingerId = t.fingerId;
                    _steerAnchor = t.position;
                }

                if (t.fingerId == _steerFingerId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        _steerFingerId = -1;
                    }
                    else
                    {
                        float dx = t.position.x - _steerAnchor.x;
                        _steer = Mathf.Clamp(dx / (Screen.width * fullLockDragFraction), -1f, 1f);
                        steering = true;
                    }
                }
            }

            // Keyboard (editor / desktop testing)
            float key = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) key -= 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) key += 1;
            if (key != 0) { _steer = Mathf.MoveTowards(_steer, key, 3f * Time.deltaTime); steering = true; }
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

            GUI.color = _go ? new Color(0.5f, 1f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(GoRect, "GO", style);
            GUI.color = _brake ? new Color(1f, 0.55f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(BrakeRect, "BRAKE", style);

            // Steering wheel indicator: under the thumb while steering, resting bottom-left otherwise
            float size = Screen.height * 0.28f;
            Vector2 centre = _steerFingerId >= 0
                ? new Vector2(_steerAnchor.x, Screen.height - _steerAnchor.y)
                : new Vector2(Screen.width * 0.14f, Screen.height * 0.75f);
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(_steer * 120f, centre);
            GUI.Box(new Rect(centre.x - size / 2, centre.y - size / 2, size, size), "◯\n━━━", style);
            GUI.matrix = old;
            GUI.color = Color.white;
        }
    }
}
