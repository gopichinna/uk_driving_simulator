using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// End-of-drive debrief (faults grouped by rule, test-style result), bird's-eye replays of each fault
    /// (6 s before to 3 s after), and the Settings screen. Text comes from the content pack (strings.v0, test marking).
    /// </summary>
    public sealed partial class DriveGame
    {
        private enum Mode { Driving, Debrief, Replay, Settings, LessonMenu, LearnCard, ScenarioDone, Quiz, LessonDone }
        private Mode _mode = Mode.Driving;

        private readonly DriveRecorder _recorder = new DriveRecorder();
        private readonly List<RuleEvent> _driveEvents = new List<RuleEvent>();
        private readonly List<ActorPose> _actors = new List<ActorPose>();
        private readonly List<LightState> _recLights = new List<LightState>();
        private double _driveStart, _nextRecord;

        private DriveSummary _summary;
        private RuleEvent _replayEvent;
        private double _replayFrom, _replayTo, _replayTime;
        private Vector2 _scroll;
        private bool _confirmReset;
        private GUIStyle _panelStyle, _textStyle, _smallStyle, _bigStyle, _rowStyle;

        private static UiStrings s_ui;
        private static RuleStrings s_rules;
        private static TestMarking s_marking;

        private static UiStrings Ui => s_ui ??= Load("Content/ui", UiStrings.FromJson, UiStrings.Empty);
        private static RuleStrings Rules => s_rules ??= Load("Content/rules", RuleStrings.FromJson, RuleStrings.Empty);
        private static TestMarking Marking => s_marking ??= Load("Content/test_marking", TestMarking.FromYaml, new TestMarking());

        private static T Load<T>(string path, System.Func<string, T> parse, T fallback)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return fallback;
            try { return parse(asset.text); }
            catch (System.Exception e) { Debug.LogWarning($"[LToLocal] Could not read {path}: {e.Message}"); return fallback; }
        }

        // ---------------- recording ----------------

        private void ResetRecording()
        {
            _recorder.Clear();
            _driveEvents.Clear();
            _driveStart = _clock.SimTime;
            _nextRecord = _clock.SimTime;
        }

        private void RecordStep()
        {
            if (_clock.SimTime < _nextRecord) return;
            _nextRecord = _clock.SimTime + _recorder.IntervalS;
            _course.Record(_actors, _recLights);
            _recorder.Record(_clock.SimTime, _curr, _indicator, _actors, _recLights);
        }

        // ---------------- modes ----------------

        private void SetMode(Mode m)
        {
            _mode = m;
            bool driving = m == Mode.Driving;
            _controls.enabled = driving;
            if (feedback != null) feedback.enabled = driving;
            if (mirrors != null) mirrors.enabled = driving;
            _confirmReset = false;
        }

        private void OpenDebrief()
        {
            _summary = DriveSummary.From(_driveEvents, Marking);
            _scroll = Vector2.zero;
            _debriefReturn = Mode.Driving;
            SetMode(Mode.Debrief);
        }

        private void StartReplay(RuleEvent e)
        {
            _replayEvent = e;
            _replayFrom = System.Math.Max(_recorder.StartTime, e.Time - 6);
            _replayTo = System.Math.Min(_recorder.EndTime, e.Time + 3);
            _replayTime = _replayFrom;
            SetMode(Mode.Replay);
        }

        private void UpdatePaused()
        {
            if (_mode != Mode.Replay) return;
            _replayTime += Time.deltaTime;
            if (_replayTime > _replayTo + 0.8) _replayTime = _replayFrom; // loop, with a short pause at the end
            var f = _recorder.SampleAt(System.Math.Min(_replayTime, _replayTo));
            if (f == null) return;
            car.SetPositionAndRotation(new Vector3((float)f.Player.X, 0f, (float)f.Player.Z), Quaternion.Euler(0f, (float)(f.Player.Heading * Mathf.Rad2Deg), 0f));
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            foreach (var l in playerIndicatorLeft) l.SetActive(f.PlayerIndicator < 0 && blink);
            foreach (var l in playerIndicatorRight) l.SetActive(f.PlayerIndicator > 0 && blink);
            _course.ShowRecorded(f, blink);
        }

        private void ReplayCamera()
        {
            foreach (var h in _mirrorHousings) h.SetActive(true);
            if (_interior != null) _interior.SetActive(false);
            chaseCamera.nearClipPlane = 0.3f;
            chaseCamera.fieldOfView = 55f;
            Vector3 fwd = car.forward;
            Vector3 pos = car.position - fwd * 12f + Vector3.up * 26f;
            chaseCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(car.position + fwd * 6f - pos));
        }

        // ---------------- drawing ----------------

        private void Styles(float H)
        {
            _panelStyle ??= new GUIStyle(GUI.skin.box);
            _textStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            _smallStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Italic };
            _bigStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _rowStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _textStyle.fontSize = Mathf.RoundToInt(H * 0.036f);
            _smallStyle.fontSize = Mathf.RoundToInt(H * 0.03f);
            _bigStyle.fontSize = Mathf.RoundToInt(H * 0.055f);
            _rowStyle.fontSize = Mathf.RoundToInt(H * 0.033f);
            _rowStyle.padding = new RectOffset(Mathf.RoundToInt(H * 0.02f), 8, 6, 6);
        }

        private void DrawOverlay(float W, float H)
        {
            Styles(H);
            switch (_mode)
            {
                case Mode.Debrief: DrawDebrief(W, H); break;
                case Mode.Replay: DrawReplay(W, H); break;
                case Mode.Settings: DrawSettings(W, H); break;
                case Mode.LessonMenu: DrawLessonMenu(W, H); break;
                case Mode.LearnCard: DrawLearnCard(W, H); break;
                case Mode.ScenarioDone: DrawScenarioDone(W, H); break;
                case Mode.Quiz: DrawQuiz(W, H); break;
                case Mode.LessonDone: DrawLessonDone(W, H); break;
            }
        }

        private static string SeverityWord(Severity s) => s switch
        {
            Severity.Driving => "Minor",
            Severity.Serious => "Serious",
            Severity.Dangerous => "Dangerous",
            _ => "",
        };

        private static Color SeverityColour(Severity s) => s switch
        {
            Severity.Driving => new Color(1f, 0.85f, 0.4f),
            Severity.Serious => new Color(1f, 0.55f, 0.35f),
            Severity.Dangerous => new Color(1f, 0.35f, 0.35f),
            _ => Color.white,
        };

        private string Clock(double t)
        {
            int s = Mathf.Max(0, (int)(t - _driveStart));
            return $"{s / 60}:{s % 60:00}";
        }

        private void DrawDebrief(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.92f));
            float x = W * 0.08f, w = W * 0.84f, y = H * 0.04f;
            var s = _summary;

            GUI.Label(new Rect(x, y, w * 0.6f, H * 0.08f), _mockDebrief ? "Mock test result" : Ui.Get("debrief.title", "Drive debrief"), _bigStyle);
            GUI.color = s.Passed ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f);
            GUI.Label(new Rect(x + w * 0.6f, y, w * 0.4f, H * 0.08f), s.Passed ? Ui.Get("debrief.result.pass", "Practice pass") : Ui.Get("debrief.result.fail", "Practice fail"), _bigStyle);
            GUI.color = Color.white;
            y += H * 0.085f;

            string counts = string.Join("   ·   ", new[]
            {
                Ui.Get("debrief.count.driving", "{count} driving faults", ("count", s.Driving)),
                Ui.Get("debrief.count.serious", "{count} serious faults", ("count", s.Serious)),
                Ui.Get("debrief.count.dangerous", "{count} dangerous faults", ("count", s.Dangerous)),
                Ui.Get("debrief.count.good", "{count} good points", ("count", s.Good)),
            });
            GUI.Label(new Rect(x, y, w, H * 0.05f), counts, _textStyle);
            y += H * 0.05f;
            GUI.Label(new Rect(x, y, w, H * 0.05f), (s.Passed ? Marking.PassSummary : Marking.FailSummary) + " " + Ui.Get("debrief.result.note", "Practice feedback only."), _smallStyle);
            y += H * 0.07f;

            float listH = H * 0.62f;
            var view = new Rect(x, y, w, listH);
            if (s.Groups.Count == 0)
            {
                GUI.Label(view, Ui.Get("debrief.no_faults", "No faults recorded on this drive."), _textStyle);
            }
            else
            {
                float rowH = H * 0.085f, headH = H * 0.06f;
                float content = 0;
                foreach (var g in s.Groups) content += headH + g.Faults.Count * (rowH + 4) + 8;
                _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, w - 30, content));
                float cy = 0;
                foreach (var g in s.Groups)
                {
                    string title = Rules.For(g.RuleId)?.Title;
                    GUI.color = SeverityColour(g.Worst);
                    GUI.Label(new Rect(0, cy, w - 30, headH), $"{(string.IsNullOrEmpty(title) ? g.RuleId : title)}  ({SeverityWord(g.Worst)})", _textStyle);
                    GUI.color = Color.white;
                    cy += headH;
                    foreach (var e in g.Faults)
                    {
                        if (GUI.Button(new Rect(H * 0.03f, cy, w - 30 - H * 0.03f, rowH), $"{Clock(e.Time)}   {SeverityWord(e.Severity)}: {e.Message}", _rowStyle))
                            StartReplay(e);
                        cy += rowH + 4;
                    }
                    cy += 8;
                }
                GUI.EndScrollView();
                GUI.Label(new Rect(x, y + listH, w, H * 0.05f), Ui.Get("debrief.tap_to_replay", "Tap any fault to watch a replay."), _smallStyle);
            }

            float bw = W * 0.2f, bh = H * 0.09f, by = H - bh - H * 0.03f;
            if (_mockDebrief && s.Passed)
            {
                GUI.color = new Color(0.5f, 1f, 0.6f);
                GUI.Label(new Rect(x, by - H * 0.07f, w, H * 0.06f), "You earned the Full Licence (practice) badge. Book your real test when your instructor agrees.", _smallStyle);
                GUI.color = Color.white;
            }
            if (_debriefReturn != Mode.Driving)
            {
                if (GUI.Button(new Rect(W * 0.5f - bw / 2, by, bw, bh), "Back", _buttonStyle)) SetMode(_debriefReturn);
                return;
            }
            if (_mockDebrief)
            {
                if (GUI.Button(new Rect(W * 0.5f - bw / 2, by, bw, bh), "Back to lessons", _buttonStyle)) { _mockDebrief = false; LessonSession.End(); SetMode(Mode.Driving); ResetDrive(); OpenLessonMenu(); }
                return;
            }
            if (GUI.Button(new Rect(W * 0.5f - bw - W * 0.01f, by, bw, bh), Ui.Get("debrief.drive_again", "Drive again"), _buttonStyle)) { SetMode(Mode.Driving); ResetDrive(); }
            if (GUI.Button(new Rect(W * 0.5f + W * 0.01f, by, bw, bh), Ui.Get("debrief.keep_driving", "Keep driving"), _buttonStyle)) SetMode(Mode.Driving);
        }

        private void DrawReplay(float W, float H)
        {
            var e = _replayEvent;
            var text = Rules.For(e.RuleId);
            float x = W * 0.05f, w = W * 0.9f, y = H * 0.03f, h = H * 0.3f;
            Fill(new Rect(x - 10, y - 6, w + 20, h), new Color(0.05f, 0.06f, 0.08f, 0.82f));
            GUI.color = SeverityColour(e.Severity);
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"{SeverityWord(e.Severity)}: {(text != null && text.Title.Length > 0 ? text.Title : e.RuleId)}", _textStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + H * 0.06f, w, H * 0.09f), e.Message, _textStyle);
            if (text != null)
                GUI.Label(new Rect(x, y + H * 0.15f, w, H * 0.1f), $"{text.Why} {text.Tip}", _smallStyle);
            GUI.Label(new Rect(x, y + H * 0.245f, w, H * 0.05f), e.Source, _smallStyle);

            double rel = _replayTime - e.Time;
            string when = rel < -0.2 ? $"{-rel:0.0} s before" : rel < 0.6 ? "This moment" : $"{rel:0.0} s after";
            GUI.Box(new Rect(W * 0.5f - H * 0.25f, H * 0.36f, H * 0.5f, H * 0.07f), when, _hudStyle);

            float bw = W * 0.2f, bh = H * 0.09f;
            if (GUI.Button(new Rect(W * 0.5f - bw / 2, H - bh - H * 0.03f, bw, bh), Ui.Get("debrief.replay_close", "Close replay"), _buttonStyle))
                SetMode(Mode.Debrief);
        }

        private void DrawSettings(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.92f));
            float x = W * 0.15f, w = W * 0.7f, y = H * 0.06f;
            GUI.Label(new Rect(x, y, w, H * 0.08f), Ui.Get("settings.title", "Settings"), _bigStyle);
            y += H * 0.13f;

            float sens = TouchControls.SteeringSensitivity;
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"{Ui.Get("settings.steering.label", "Steering sensitivity")}: {Mathf.RoundToInt(sens * 100)}%", _textStyle);
            y += H * 0.07f;
            var thumb = GUI.skin.horizontalSliderThumb;
            float oldW = thumb.fixedWidth, oldH = thumb.fixedHeight;
            thumb.fixedWidth = H * 0.07f;
            thumb.fixedHeight = H * 0.07f;
            var slider = GUI.skin.horizontalSlider;
            float oldSH = slider.fixedHeight;
            slider.fixedHeight = H * 0.07f;
            float v = GUI.HorizontalSlider(new Rect(x, y, w, H * 0.07f), sens, 0.3f, 1.5f);
            thumb.fixedWidth = oldW; thumb.fixedHeight = oldH; slider.fixedHeight = oldSH;
            v = Mathf.Round(v * 20f) / 20f; // 5% steps
            if (!Mathf.Approximately(v, sens)) TouchControls.SteeringSensitivity = v;
            y += H * 0.09f;
            GUI.Label(new Rect(x, y, w, H * 0.06f), Ui.Get("settings.steering.hint", "Higher values make steering react faster.") + " (60% is the default.)", _smallStyle);
            y += H * 0.1f;
            GUI.Label(new Rect(x, y, w, H * 0.06f), Ui.Get("settings.units.hint", "UK roads use mph (30 mph ≈ 48 km/h)."), _textStyle);
            y += H * 0.12f;

            float bw = W * 0.26f, bh = H * 0.09f;
            string resetLabel = _confirmReset ? Ui.Get("settings.reset_progress.confirm", "Clear all saved progress?") : Ui.Get("settings.reset_progress.label", "Reset progress");
            if (GUI.Button(new Rect(x, y, bw * (_confirmReset ? 2f : 1f), bh), resetLabel, _buttonStyle))
            {
                if (_confirmReset) { ResetProgress(); _confirmReset = false; }
                else _confirmReset = true;
            }

            if (GUI.Button(new Rect(W * 0.5f - bw / 2, H - bh - H * 0.04f, bw, bh), Ui.Get("settings.close", "Close"), _buttonStyle))
                SetMode(Mode.Driving);
        }

        /// <summary>Clears saved progress (everything except settings). Lessons will store progress under "progress_" keys.</summary>
        private static void ResetProgress()
        {
            float sens = TouchControls.SteeringSensitivity;
            PlayerPrefs.DeleteAll();
            TouchControls.SteeringSensitivity = sens;
            LessonSession.ResetAll();
        }
    }
}
