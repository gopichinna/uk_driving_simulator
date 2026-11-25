using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LToLocal.Game
{
    /// <summary>
    /// Three live mirrors on the player's car: interior rear-view, nearside (left) and offside (right) door mirrors.
    /// Each is a low-resolution camera rendering into a texture, drawn mirror-flipped on the HUD.
    /// The door mirrors use a narrow field of view, so a car alongside disappears from them:
    /// a realistic blind spot that the shoulder check (later milestone) will teach.
    /// </summary>
    public sealed class MirrorRig : MonoBehaviour
    {
        [Tooltip("The player's car. Mirror cameras are attached to it.")]
        public Transform car;

        public bool showMirrors = true;

        private Camera _rear, _left, _right;
        private RenderTexture _rearTex, _leftTex, _rightTex;

        private void Start()
        {
            _rear = MakeMirrorCamera("RearViewMirrorCam", new Vector3(0f, 1.32f, -0.6f), new Vector3(4f, 180f, 0f), 13f, 768, 224, out _rearTex);
            // Door mirrors look back and slightly outward. Yaw > 180 turns toward the car's left.
            _left = MakeMirrorCamera("NearsideMirrorCam", new Vector3(-1.04f, 1.0f, 0.78f), new Vector3(3f, 191f, 0f), 19f, 448, 288, out _leftTex);
            _right = MakeMirrorCamera("OffsideMirrorCam", new Vector3(1.04f, 1.0f, 0.78f), new Vector3(3f, 169f, 0f), 19f, 448, 288, out _rightTex);
        }

        private Camera MakeMirrorCamera(string name, Vector3 localPos, Vector3 localEuler, float verticalFov, int w, int h, out RenderTexture tex)
        {
            tex = new RenderTexture(w, h, 16) { name = name + "Tex", antiAliasing = 1 };
            var go = new GameObject(name);
            go.transform.SetParent(car, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;

            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = verticalFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            cam.targetTexture = tex;
            cam.depth = -10;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;

            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderShadows = false;
            urp.renderPostProcessing = false;
            return cam;
        }

        public void SetVisible(bool visible)
        {
            showMirrors = visible;
            if (_rear != null) _rear.enabled = _left.enabled = _right.enabled = visible;
        }

        private void OnGUI()
        {
            if (!showMirrors || _rearTex == null) return;
            float W = Screen.width, H = Screen.height;

            float sideW = W * 0.17f, sideH = sideW / 1.55f;
            DrawMirror(new Rect(W * 0.015f, H * 0.04f, sideW, sideH), _leftTex, 14f);
            DrawMirror(new Rect(W * 0.985f - sideW, H * 0.04f, sideW, sideH), _rightTex, 14f);

            float rearW = W * 0.30f, rearH = rearW / 3.4f;
            DrawMirror(new Rect((W - rearW) / 2f, H * 0.015f, rearW, rearH), _rearTex, 10f);
        }

        private static void DrawMirror(Rect r, Texture tex, float frame)
        {
            GUI.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(r.x - frame * 0.5f, r.y - frame * 0.5f, r.width + frame, r.height + frame), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // Flip horizontally: a mirror shows the world reversed left-to-right.
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(1f, 0f, -1f, 1f));
        }

        private void OnDestroy()
        {
            if (_rearTex != null) _rearTex.Release();
            if (_leftTex != null) _leftTex.Release();
            if (_rightTex != null) _rightTex.Release();
        }
    }
}
