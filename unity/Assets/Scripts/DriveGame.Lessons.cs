using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LToLocal.Game
{
    /// <summary>
    /// Lesson flow: lesson menu → learn card (diagram + Highway Code link) → scripted driving scenarios
    /// (ScenarioDirector) → quiz → stars. Also the final mock test: about 35 minutes of sat-nav driving in
    /// Wolverhampton, marked like the practical test.
    /// </summary>
    public sealed partial class DriveGame
    {
        private ScenarioDirector _director;
        private ScenarioResult _lastResult;
        private QuizDef _quiz;
        private int _quizIndex, _quizCorrect, _quizChoice = -1, _lessonStars;
        private Mode _debriefReturn = Mode.Driving;
        private Vector2 _menuScroll;

        private SatNav _nav;
        private MockTestPlan _plan;
        private double _mockStart;
        private bool _mockDebrief;

        private IScenarioWorld World => _course as IScenarioWorld;

        /// <summary>Before the first reset: put the car at the scenario's anchor.</summary>
        private void PrepareLessonStart()
        {
            var r = LessonSession.CurrentRef;
            if (LessonSession.ScenarioPending && r != null && _course is TownCourse town) town.startAnchor = r.Anchor;
        }

        /// <summary>After every reset: (re)start the scenario or mock test this scene is running.</summary>
        private void StartLessonPart()
        {
            _director = null;
            _nav = null;
            bool here = LessonSession.CurrentRef != null && SceneManager.GetActiveScene().name == LessonSession.SceneFor(LessonSession.CurrentRef.Course);
            if (LessonSession.Lesson != null && here && World != null)
            {
                var def = LessonSession.LoadScenario(LessonSession.CurrentRef.Id);
                if (def != null)
                {
                    _director = new ScenarioDirector(def);
                    _director.Start(World, _curr);
                }
                LessonSession.ScenarioPending = false;
            }
            else if (LessonSession.MockTest && _course is WolverhamptonCourse wolv)
            {
                _nav = new SatNav(wolv.Graph);
                _plan = new MockTestPlan(wolv.Graph, (ulong)System.DateTime.Now.Ticks);
                _nav.SetDestination(_plan.NextDestination(new Vec2(_curr.X, _curr.Z)), _curr);
                _mockStart = _clock.SimTime;
                LessonSession.ScenarioPending = false;
            }
        }

        private void LessonStep(double dt)
        {
            if (_director != null && !_director.Finished)
            {
                _director.Step(dt, World, _curr);
                if (_director.Finished) EndScenario(_director.Completed, _director.Note);
            }
            if (_nav != null)
            {
                _nav.Update(_curr, _clock.SimTime);
                if (_nav.Arrived) _nav.SetDestination(_plan.NextDestination(new Vec2(_curr.X, _curr.Z)), _curr);
                if (_clock.SimTime - _mockStart >= _plan.DurationS) EndMockTest();
            }
        }

        private void EndScenario(bool completed, string note)
        {
            _lastResult = new ScenarioResult
            {
                ScenarioId = LessonSession.CurrentRef?.Id ?? "",
                Completed = completed,
                Note = completed ? note : "You ended this drive early.",
                Summary = DriveSummary.From(_driveEvents, Marking),
            };
            _director = null;
            SetMode(Mode.ScenarioDone);
        }

        private void EndMockTest()
        {
            _nav = null;
            _mockDebrief = true;
            OpenDebrief();
            if (_summary.Passed) LessonSession.AwardLicence();
        }

        /// <summary>"Finish" during a lesson ends the scenario (or the mock test) instead of the free drive.</summary>
        private bool FinishLessonPart()
        {
            if (_director != null) { EndScenario(false, ""); return true; }
            if (_nav != null) { EndMockTest(); return true; }
            return false;
        }

        // ---------------- HUD during lessons ----------------

        private void DrawLessonHud(float W, float H)
        {
            string line = null;
            if (_director != null && LessonSession.CurrentRef != null)
            {
                var r = LessonSession.CurrentRef;
                line = $"Drive {LessonSession.ScenarioIndex + 1} of {LessonSession.Lesson.Scenarios.Count}: {r.Goal}";
                if (_director.RequiredExit > 0) line += $"  Take exit {_director.RequiredExit}.";
                line += $"   ({(int)_director.TimeLeft / 60}:{(int)_director.TimeLeft % 60:00} left)";
            }
            else if (_nav != null)
            {
                var (what, dist) = _nav.Next(_curr);
                double left = System.Math.Max(0, _plan.DurationS - (_clock.SimTime - _mockStart));
                line = $"Mock test · independent driving · {(int)left / 60}:{(int)left % 60:00} left\n{SatNav.Describe(what, dist)}";
            }
            if (line == null) return;
            Styles(H);
            float w = W * 0.62f, h = H * (_nav != null ? 0.13f : 0.09f);
            var rect = new Rect(W * 0.5f - w / 2, H * 0.69f, w, h);
            Fill(rect, new Color(0.05f, 0.06f, 0.08f, 0.75f));
            var style = new GUIStyle(_textStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(H * (_nav != null ? 0.038f : 0.03f)) };
            GUI.Label(rect, line, style);
        }

        // ---------------- screens ----------------

        private void OpenLessonMenu()
        {
            _menuScroll = Vector2.zero;
            SetMode(Mode.LessonMenu);
        }

        private void DrawLessonMenu(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.95f));
            float x = W * 0.1f, w = W * 0.8f, y = H * 0.04f;
            GUI.Label(new Rect(x, y, w * 0.6f, H * 0.08f), "Lessons", _bigStyle);
            if (LessonSession.HasLicence)
            {
                GUI.color = new Color(0.5f, 1f, 0.6f);
                GUI.Label(new Rect(x + w * 0.55f, y, w * 0.45f, H * 0.08f), "Full Licence (practice) earned", _textStyle);
                GUI.color = Color.white;
            }
            y += H * 0.1f;

            var lessons = LessonSession.Lessons;
            float rowH = H * 0.085f, headH = H * 0.06f;
            int stages = 0, last = -1;
            foreach (var l in lessons) if (l.Stage != last) { stages++; last = l.Stage; }
            float content = lessons.Count * (rowH + 6) + stages * headH + rowH * 2;
            var view = new Rect(x, y, w, H * 0.72f);
            _menuScroll = GUI.BeginScrollView(view, _menuScroll, new Rect(0, 0, w - 30, content));
            float cy = 0;
            last = -1;
            foreach (var l in lessons)
            {
                if (l.Stage != last)
                {
                    last = l.Stage;
                    GUI.Label(new Rect(0, cy, w - 30, headH), StageName(l.Stage), _textStyle);
                    cy += headH;
                }
                bool open = LessonSession.Progress.Unlocked(l, lessons);
                int stars = LessonSession.Progress.StarsFor(l.Id);
                string label = $"{l.Order}. {l.Title}{(l.FreeTier ? "  (free)" : "")}   {new string('★', stars)}{new string('☆', 3 - stars)}{(open ? "" : "   Locked")}";
                GUI.enabled = open;
                if (GUI.Button(new Rect(0, cy, w - 30, rowH), label, _rowStyle))
                {
                    LessonSession.Begin(l);
                    SetMode(Mode.LearnCard);
                }
                GUI.enabled = true;
                cy += rowH + 6;
            }
            cy += 10;
            if (GUI.Button(new Rect(0, cy, w - 30, rowH), "Final: mock driving test in Wolverhampton (about 35 minutes)", _rowStyle))
            {
                SetMode(Mode.Driving);
                LessonSession.BeginMockTest();
            }
            GUI.EndScrollView();

            float bw = W * 0.22f, bh = H * 0.09f;
            if (GUI.Button(new Rect(W * 0.5f - bw / 2, H - bh - H * 0.03f, bw, bh), "Free drive", _buttonStyle))
            {
                LessonSession.End();
                _director = null;
                _nav = null;
                SetMode(Mode.Driving);
            }
        }

        private static string StageName(int stage) => stage switch
        {
            0 => "Stage 0: Getting started",
            1 => "Stage 1: Quiet roads",
            2 => "Stage 2: Lights and crossings",
            3 => "Stage 3: Roundabouts",
            4 => "Stage 4: Busy town",
            5 => "Stage 5: Faster roads",
            _ => $"Stage {stage}",
        };

        private void DrawLearnCard(float W, float H)
        {
            var l = LessonSession.Lesson;
            if (l == null) { SetMode(Mode.LessonMenu); return; }
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.95f));
            GUI.Label(new Rect(W * 0.05f, H * 0.03f, W * 0.9f, H * 0.08f), l.Title, _bigStyle);

            float size = H * 0.66f;
            var tex = LessonSession.Diagram(l.Id);
            if (tex != null) GUI.DrawTexture(new Rect(W * 0.05f, H * 0.13f, size, size), tex, ScaleMode.ScaleToFit);

            float tx = W * 0.05f + size + W * 0.03f, tw = W * 0.92f - tx, ty = H * 0.14f;
            foreach (var b in l.LearnBlocks)
            {
                float bh = _textStyle.CalcHeight(new GUIContent("• " + b), tw);
                GUI.Label(new Rect(tx, ty, tw, bh), "• " + b, _textStyle);
                ty += bh + H * 0.02f;
            }
            ty += H * 0.02f;
            GUI.Label(new Rect(tx, ty, tw, H * 0.06f), $"{l.Scenarios.Count} short drives, then a quick quiz.", _smallStyle);

            float bw = W * 0.2f, bh2 = H * 0.09f, by = H - bh2 - H * 0.03f;
            if (GUI.Button(new Rect(W * 0.05f, by, bw, bh2), "Back", _buttonStyle)) { LessonSession.End(); SetMode(Mode.LessonMenu); }
            if (!string.IsNullOrEmpty(l.GovUkUrl) && GUI.Button(new Rect(W * 0.5f - bw / 2, by, bw, bh2), "Highway Code", _buttonStyle))
                Application.OpenURL(l.GovUkUrl);
            if (GUI.Button(new Rect(W * 0.95f - bw, by, bw, bh2), "Start driving", _buttonStyle))
            {
                SetMode(Mode.Driving);
                LessonSession.StartScenario();
            }
        }

        private void DrawScenarioDone(float W, float H)
        {
            var r = _lastResult;
            var sref = LessonSession.CurrentRef;
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.9f));
            float x = W * 0.1f, w = W * 0.8f, y = H * 0.06f;
            bool passed = sref != null && r.Passed(sref);
            GUI.color = passed ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.75f, 0.45f);
            GUI.Label(new Rect(x, y, w, H * 0.08f), passed ? "Well driven" : "Not quite yet", _bigStyle);
            GUI.color = Color.white;
            y += H * 0.1f;
            if (sref != null) { GUI.Label(new Rect(x, y, w, H * 0.08f), sref.Goal, _textStyle); y += H * 0.09f; }
            var s = r.Summary;
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"Minor {s.Driving}   ·   Serious {s.Serious}   ·   Dangerous {s.Dangerous}   ·   Good {s.Good}", _textStyle);
            y += H * 0.07f;
            if (sref != null) GUI.Label(new Rect(x, y, w, H * 0.06f), $"To pass this drive: no more than {sref.MaxMinor} minor and {sref.MaxSerious} serious faults, and no dangerous faults.", _smallStyle);
            y += H * 0.07f;
            if (!string.IsNullOrEmpty(r.Note)) GUI.Label(new Rect(x, y, w, H * 0.06f), r.Note, _smallStyle);

            float bw = W * 0.2f, bh = H * 0.09f, by = H - bh - H * 0.04f;
            if (s.Groups.Count > 0 && GUI.Button(new Rect(W * 0.5f - bw * 1.6f, by, bw, bh), "See faults", _buttonStyle))
            {
                OpenDebrief();
                _debriefReturn = Mode.ScenarioDone;
            }
            if (GUI.Button(new Rect(W * 0.5f - bw / 2, by, bw, bh), "Try again", _buttonStyle))
            {
                SetMode(Mode.Driving);
                LessonSession.StartScenario();
            }
            if (GUI.Button(new Rect(W * 0.5f + bw * 0.6f, by, bw, bh), "Next", _buttonStyle))
            {
                LessonSession.RecordResult(r);
                if (LessonSession.MoreScenarios) { SetMode(Mode.Driving); LessonSession.StartScenario(); }
                else StartQuiz();
            }
        }

        private void StartQuiz()
        {
            _quiz = LessonSession.LoadQuiz(LessonSession.Lesson.Id);
            _quizIndex = 0;
            _quizCorrect = 0;
            _quizChoice = -1;
            if (_quiz == null || _quiz.Questions.Count == 0) { FinishLesson(true); return; }
            SetMode(Mode.Quiz);
        }

        private void DrawQuiz(float W, float H)
        {
            var q = _quiz.Questions[_quizIndex];
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.95f));
            float x = W * 0.1f, w = W * 0.8f, y = H * 0.05f;
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"Quick quiz: question {_quizIndex + 1} of {_quiz.Questions.Count}", _smallStyle);
            y += H * 0.07f;
            float qh = _textStyle.CalcHeight(new GUIContent(q.Text), w);
            GUI.Label(new Rect(x, y, w, qh), q.Text, _textStyle);
            y += qh + H * 0.04f;
            for (int i = 0; i < q.Options.Count; i++)
            {
                if (_quizChoice >= 0) GUI.color = i == q.AnswerIndex ? new Color(0.5f, 1f, 0.6f) : i == _quizChoice ? new Color(1f, 0.5f, 0.45f) : Color.white;
                GUI.enabled = _quizChoice < 0;
                if (GUI.Button(new Rect(x, y, w, H * 0.09f), q.Options[i], _rowStyle))
                {
                    _quizChoice = i;
                    if (i == q.AnswerIndex) _quizCorrect++;
                }
                GUI.enabled = true;
                GUI.color = Color.white;
                y += H * 0.105f;
            }
            if (_quizChoice < 0) return;
            bool right = _quizChoice == q.AnswerIndex;
            GUI.Label(new Rect(x, y, w, H * 0.12f), (right ? "Correct. " : "Not quite. ") + q.Explanation, _textStyle);
            float bw = W * 0.2f, bh = H * 0.09f;
            if (GUI.Button(new Rect(W * 0.9f - bw, H - bh - H * 0.03f, bw, bh), "Next", _buttonStyle))
            {
                _quizChoice = -1;
                if (++_quizIndex >= _quiz.Questions.Count) FinishLesson(_quizCorrect == _quiz.Questions.Count);
            }
        }

        private void FinishLesson(bool quizAllCorrect)
        {
            _lessonStars = LessonSession.Finish(quizAllCorrect);
            SetMode(Mode.LessonDone);
        }

        private void DrawLessonDone(float W, float H)
        {
            var l = LessonSession.Lesson;
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.95f));
            float x = W * 0.1f, w = W * 0.8f, y = H * 0.06f;
            GUI.Label(new Rect(x, y, w, H * 0.08f), l != null ? l.Title : "Lesson complete", _bigStyle);
            y += H * 0.1f;
            var stars = new GUIStyle(_bigStyle) { fontSize = Mathf.RoundToInt(H * 0.12f) };
            GUI.color = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(x, y, w, H * 0.15f), new string('★', _lessonStars) + new string('☆', 3 - _lessonStars), stars);
            GUI.color = Color.white;
            y += H * 0.17f;
            GUI.Label(new Rect(x, y, w, H * 0.06f), _lessonStars switch
            {
                3 => "Perfect: no faults and every quiz answer right.",
                2 => "Passed. For 3 stars, drive with no faults and get every quiz answer right.",
                1 => "Finished. Pass each drive within its fault limits for 2 stars.",
                _ => "Not finished yet.",
            }, _smallStyle);
            y += H * 0.08f;
            if (l != null)
                foreach (var line in l.Recap)
                {
                    GUI.Label(new Rect(x, y, w, H * 0.06f), "• " + line, _textStyle);
                    y += H * 0.07f;
                }

            float bw = W * 0.22f, bh = H * 0.09f, by = H - bh - H * 0.04f;
            if (GUI.Button(new Rect(W * 0.5f - bw - W * 0.01f, by, bw, bh), "Try again", _buttonStyle) && l != null)
            {
                LessonSession.Begin(l);
                SetMode(Mode.LearnCard);
            }
            if (GUI.Button(new Rect(W * 0.5f + W * 0.01f, by, bw, bh), "Back to lessons", _buttonStyle))
            {
                LessonSession.End();
                OpenLessonMenu();
            }
        }
    }
}
