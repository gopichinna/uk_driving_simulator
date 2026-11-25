using System.Collections.Generic;
using LToLocal.Rules;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Instant feedback (docs/03 §5.1): short colour-coded messages naming the rule,
    /// plus a running tally of faults for this drive. Prototype IMGUI version.
    /// </summary>
    public sealed class FeedbackView : MonoBehaviour
    {
        public float showSeconds = 4.5f;
        public int maxVisible = 3;

        private readonly List<(RuleEvent evt, float shownAt)> _active = new List<(RuleEvent, float)>();
        private int _good, _minor, _serious, _dangerous;
        private GUIStyle _msg, _src, _tally;

        public void Push(RuleEvent e)
        {
            _active.Add((e, Time.time));
            if (_active.Count > maxVisible) _active.RemoveAt(0);

            if (e.Outcome == RuleOutcome.GoodPractice) _good++;
            else if (e.Severity == Severity.Driving) _minor++;
            else if (e.Severity == Severity.Serious) _serious++;
            else if (e.Severity == Severity.Dangerous) _dangerous++;

#if UNITY_ANDROID || UNITY_IOS
            if (e.Severity >= Severity.Serious) Handheld.Vibrate();
#endif
        }

        public void ResetTally()
        {
            _active.Clear();
            _good = _minor = _serious = _dangerous = 0;
        }

        private static Color ColourFor(RuleEvent e) => e.Outcome == RuleOutcome.GoodPractice
            ? new Color(0.16f, 0.55f, 0.30f, 0.95f)
            : e.Severity switch
            {
                Severity.Dangerous => new Color(0.55f, 0.08f, 0.10f, 0.97f),
                Severity.Serious => new Color(0.80f, 0.20f, 0.18f, 0.95f),
                _ => new Color(0.85f, 0.55f, 0.10f, 0.95f),
            };

        private static string LabelFor(RuleEvent e) => e.Outcome == RuleOutcome.GoodPractice
            ? "Good:"
            : e.Severity switch
            {
                Severity.Dangerous => "DANGEROUS",
                Severity.Serious => "SERIOUS",
                _ => "Minor",
            };

        private void OnGUI()
        {
            float W = Screen.width, H = Screen.height;
            _msg ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            _src ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            _tally ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft };
            _msg.fontSize = Mathf.RoundToInt(H * 0.034f);
            _src.fontSize = Mathf.RoundToInt(H * 0.024f);
            _tally.fontSize = Mathf.RoundToInt(H * 0.026f);
            _msg.normal.textColor = Color.white;
            _src.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            _active.RemoveAll(a => Time.time - a.shownAt > showSeconds);

            float w = W * 0.40f, x = (W - w) / 2f, y = H * 0.34f;
            float h = H * 0.12f;
            foreach (var (e, shownAt) in _active)
            {
                float fade = Mathf.Clamp01((showSeconds - (Time.time - shownAt)) / 0.5f);
                var c = ColourFor(e);
                c.a *= fade;
                GUI.color = c;
                GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(x + H * 0.02f, y + H * 0.008f, w - H * 0.04f, h * 0.7f), $"{LabelFor(e)}  {e.Message}", _msg);
                GUI.Label(new Rect(x + H * 0.02f, y + h * 0.72f, w - H * 0.04f, h * 0.3f), e.Source, _src);
                GUI.color = Color.white;
                y += h + H * 0.012f;
            }

            // Tally for this drive, bottom left of the speed readout
            GUI.Box(new Rect(W * 0.24f, H * 0.84f, W * 0.17f, H * 0.12f),
                $" Good {_good}    Minor {_minor}\n Serious {_serious}    Dangerous {_dangerous}", _tally);
        }
    }
}
