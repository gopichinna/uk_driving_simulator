using System.Collections.Generic;
using LToLocal.Rules;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Mock theory test: pick one of the ready-made 50-question sets, answer against the clock (57 minutes, pass mark 43),
    /// flag and review, then see the score, the weakest topics and every wrong answer explained.
    /// Questions and marking live in Rules.Core (TheoryTestSession); this file only draws them.
    /// </summary>
    public sealed partial class DriveGame
    {
        private const int TheorySetCount = 10;
        private TheoryTestSession _theory;
        private int _theorySetNumber, _theoryWrongIndex;
        private bool _theoryConfirmFinish;
        private Vector2 _theoryScroll;

        private static string BestKey(int set) => $"theory_best_{set}";
        private static int TheoryBest(int set) => PlayerPrefs.GetInt(BestKey(set), -1);

        private void OpenTheoryMenu()
        {
            _theory = null;
            _theoryScroll = Vector2.zero;
            SetMode(Mode.TheoryMenu);
        }

        private void StartTheory(int setNumber)
        {
            var asset = Resources.Load<TextAsset>($"Content/theory/set-{setNumber:00}");
            if (asset == null) { Debug.LogWarning($"[LToLocal] Theory set {setNumber} not found."); return; }
            _theorySetNumber = setNumber;
            _theory = new TheoryTestSession(TheorySetDef.FromJson(asset.text));
            _theoryConfirmFinish = false;
            SetMode(Mode.Theory);
        }

        /// <summary>Called every frame outside driving: runs the test clock.</summary>
        private void UpdateTheoryClock()
        {
            if (_theory == null || _theory.Finished) return;
            if (_mode != Mode.Theory && _mode != Mode.TheoryReview) return;
            _theory.Tick(Time.unscaledDeltaTime);
            if (_theory.Finished) FinishTheory();
        }

        private void FinishTheory()
        {
            _theory.Finish();
            if (_theory.Score > TheoryBest(_theorySetNumber))
            {
                PlayerPrefs.SetInt(BestKey(_theorySetNumber), _theory.Score);
                PlayerPrefs.Save();
            }
            _theoryWrongIndex = 0;
            SetMode(Mode.TheoryResult);
        }

        private static string Clock(double s)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt((float)s));
            return $"{t / 60}:{t % 60:00}";
        }

        private void DrawTheoryMenu(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.95f));
            float x = W * 0.1f, w = W * 0.8f, y = H * 0.04f;
            GUI.Label(new Rect(x, y, w, H * 0.08f), "Theory test practice", _bigStyle);
            y += H * 0.09f;
            GUI.Label(new Rect(x, y, w, H * 0.1f), "50 questions, 57 minutes. You need 43 right to pass, like the real test. You can change answers and flag questions before you finish.", _smallStyle);
            y += H * 0.11f;

            float rowH = H * 0.08f;
            var view = new Rect(x, y, w, H * 0.86f - y);
            _theoryScroll = GUI.BeginScrollView(view, _theoryScroll, new Rect(0, 0, w - 30, TheorySetCount * (rowH + 6)));
            for (int n = 1; n <= TheorySetCount; n++)
            {
                int best = TheoryBest(n);
                string result = best < 0 ? "Not tried" : best >= 43 ? $"Best {best}/50  Passed" : $"Best {best}/50";
                if (best >= 43) GUI.color = new Color(0.5f, 1f, 0.6f);
                if (GUI.Button(new Rect(0, (n - 1) * (rowH + 6), w - 30, rowH), $"Practice test {n}     {result}", _rowStyle)) StartTheory(n);
                GUI.color = Color.white;
            }
            GUI.EndScrollView();

            float bw = W * 0.22f, bh = H * 0.09f;
            if (GUI.Button(new Rect(W * 0.5f - bw / 2, H - bh - H * 0.03f, bw, bh), "Back", _buttonStyle)) OpenLessonMenu();
        }

        private void DrawTheory(float W, float H)
        {
            var t = _theory;
            var q = t.Current;
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.97f));
            float x = W * 0.08f, w = W * 0.84f, y = H * 0.03f;

            bool low = t.TimeLeftS < 300;
            GUI.Label(new Rect(x, y, w * 0.6f, H * 0.06f), $"Question {t.Index + 1} of {t.Count}{(t.Flags[t.Index] ? "   (flagged)" : "")}", _smallStyle);
            if (low) GUI.color = new Color(1f, 0.5f, 0.45f);
            GUI.Label(new Rect(x + w * 0.7f, y, w * 0.3f, H * 0.06f), $"Time left {Clock(t.TimeLeftS)}", _smallStyle);
            GUI.color = Color.white;
            y += H * 0.07f;

            float qh = _textStyle.CalcHeight(new GUIContent(q.Text), w);
            GUI.Label(new Rect(x, y, w, qh), q.Text, _textStyle);
            y += qh + H * 0.03f;

            for (int i = 0; i < q.Options.Count; i++)
            {
                bool chosen = t.Answers[t.Index] == i;
                if (chosen) GUI.color = new Color(0.55f, 0.8f, 1f);
                if (GUI.Button(new Rect(x, y, w, H * 0.09f), $"{(char)('A' + i)}.  {q.Options[i]}", _rowStyle)) t.Choose(i);
                GUI.color = Color.white;
                y += H * 0.105f;
            }

            float bw = W * 0.18f, bh = H * 0.09f, by = H - bh - H * 0.03f, gap = W * 0.015f;
            float bx = x;
            GUI.enabled = t.Index > 0;
            if (GUI.Button(new Rect(bx, by, bw, bh), "Previous", _buttonStyle)) t.Previous();
            GUI.enabled = true;
            bx += bw + gap;
            if (GUI.Button(new Rect(bx, by, bw, bh), t.Flags[t.Index] ? "Unflag" : "Flag", _buttonStyle)) t.ToggleFlag();
            bx += bw + gap;
            if (GUI.Button(new Rect(bx, by, bw, bh), "Review", _buttonStyle)) { _theoryConfirmFinish = false; SetMode(Mode.TheoryReview); }
            bx += bw + gap;
            if (t.Index < t.Count - 1)
            {
                if (GUI.Button(new Rect(bx, by, bw, bh), "Next", _buttonStyle)) t.Next();
            }
            else if (GUI.Button(new Rect(bx, by, bw, bh), "Finish", _buttonStyle)) { _theoryConfirmFinish = false; SetMode(Mode.TheoryReview); }
        }

        private void DrawTheoryReview(float W, float H)
        {
            var t = _theory;
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.97f));
            float x = W * 0.08f, w = W * 0.84f, y = H * 0.03f;
            GUI.Label(new Rect(x, y, w * 0.6f, H * 0.08f), "Review your answers", _bigStyle);
            GUI.Label(new Rect(x + w * 0.7f, y, w * 0.3f, H * 0.06f), $"Time left {Clock(t.TimeLeftS)}", _smallStyle);
            y += H * 0.09f;
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"{t.AnsweredCount} answered, {t.UnansweredCount} not answered, {t.FlaggedCount} flagged. Tap a number to go back to that question.", _smallStyle);
            y += H * 0.08f;

            int cols = 10;
            float cell = w / cols, ch = H * 0.065f;
            for (int i = 0; i < t.Count; i++)
            {
                int col = i % cols, row = i / cols;
                GUI.color = t.Flags[i] ? new Color(1f, 0.85f, 0.4f) : t.Answers[i] == TheoryTestSession.NoAnswer ? new Color(1f, 0.5f, 0.45f) : new Color(0.55f, 0.8f, 1f);
                if (GUI.Button(new Rect(x + col * cell, y + row * (ch + 4), cell - 4, ch), (i + 1).ToString(), _buttonStyle))
                {
                    t.GoTo(i);
                    SetMode(Mode.Theory);
                }
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(x, y + 5 * (ch + 4) + H * 0.01f, w, H * 0.05f), "Blue: answered     Yellow: flagged     Red: not answered", _smallStyle);

            float bw = W * 0.26f, bh = H * 0.09f, by = H - bh - H * 0.03f;
            if (GUI.Button(new Rect(x, by, bw, bh), "Back to questions", _buttonStyle)) SetMode(Mode.Theory);
            if (!_theoryConfirmFinish)
            {
                if (GUI.Button(new Rect(x + w - bw, by, bw, bh), "Finish test", _buttonStyle)) _theoryConfirmFinish = true;
            }
            else
            {
                GUI.Label(new Rect(x + w * 0.3f, by - H * 0.07f, w * 0.7f, H * 0.06f),
                    t.UnansweredCount > 0 ? $"{t.UnansweredCount} unanswered will count as wrong. Finish now?" : "Finish and see your result?", _smallStyle);
                GUI.color = new Color(0.5f, 1f, 0.6f);
                if (GUI.Button(new Rect(x + w - bw, by, bw, bh), "Yes, finish", _buttonStyle)) FinishTheory();
                GUI.color = Color.white;
            }
        }

        private void DrawTheoryResult(float W, float H)
        {
            var t = _theory;
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.97f));
            float x = W * 0.1f, w = W * 0.8f, y = H * 0.04f;
            GUI.color = t.Passed ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f);
            GUI.Label(new Rect(x, y, w, H * 0.09f), t.Passed ? $"Passed: {t.Score} out of {t.Count}" : $"Not passed: {t.Score} out of {t.Count}", _bigStyle);
            GUI.color = Color.white;
            y += H * 0.1f;
            string why = t.TimedOut ? "Time ran out. " : "";
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"{why}You needed {t.Set.PassMark}. Best on this test: {TheoryBest(_theorySetNumber)}/{t.Count}.", _textStyle);
            y += H * 0.08f;

            var topics = t.Topics();
            GUI.Label(new Rect(x, y, w, H * 0.06f), "Topics to work on", _textStyle);
            y += H * 0.065f;
            int shown = 0;
            foreach (var tr in topics)
            {
                if (tr.Wrong == 0 || shown >= 5) break;
                string title = Rules.For(tr.RuleId)?.Title;
                GUI.Label(new Rect(x, y, w, H * 0.05f), $"•  {(string.IsNullOrEmpty(title) ? tr.RuleId : title)}: {tr.Correct} of {tr.Total} right", _smallStyle);
                y += H * 0.05f;
                shown++;
            }
            if (shown == 0) GUI.Label(new Rect(x, y, w, H * 0.05f), "Nothing. Every answer was right.", _smallStyle);

            float bw = W * 0.24f, bh = H * 0.09f, by = H - bh - H * 0.03f, gap = W * 0.02f;
            GUI.enabled = t.WrongQuestions().Count > 0;
            if (GUI.Button(new Rect(x, by, bw, bh), "See wrong answers", _buttonStyle)) { _theoryWrongIndex = 0; SetMode(Mode.TheoryWrong); }
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + bw + gap, by, bw, bh), "Try again", _buttonStyle)) StartTheory(_theorySetNumber);
            if (GUI.Button(new Rect(x + 2 * (bw + gap), by, bw, bh), "All tests", _buttonStyle)) OpenTheoryMenu();
        }

        private void DrawTheoryWrong(float W, float H)
        {
            var t = _theory;
            var wrong = t.WrongQuestions();
            if (wrong.Count == 0) { SetMode(Mode.TheoryResult); return; }
            _theoryWrongIndex = Mathf.Clamp(_theoryWrongIndex, 0, wrong.Count - 1);
            int qi = wrong[_theoryWrongIndex];
            var q = t.Set.Questions[qi];
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.97f));
            float x = W * 0.08f, w = W * 0.84f, y = H * 0.03f;
            GUI.Label(new Rect(x, y, w, H * 0.06f), $"Wrong answer {_theoryWrongIndex + 1} of {wrong.Count} (question {qi + 1})", _smallStyle);
            y += H * 0.07f;
            float qh = _textStyle.CalcHeight(new GUIContent(q.Text), w);
            GUI.Label(new Rect(x, y, w, qh), q.Text, _textStyle);
            y += qh + H * 0.03f;
            for (int i = 0; i < q.Options.Count; i++)
            {
                GUI.color = i == q.AnswerIndex ? new Color(0.5f, 1f, 0.6f) : i == t.Answers[qi] ? new Color(1f, 0.5f, 0.45f) : Color.white;
                string tag = i == q.AnswerIndex ? "   (right answer)" : i == t.Answers[qi] ? "   (your answer)" : "";
                GUI.Button(new Rect(x, y, w, H * 0.08f), $"{(char)('A' + i)}.  {q.Options[i]}{tag}", _rowStyle);
                GUI.color = Color.white;
                y += H * 0.092f;
            }
            if (t.Answers[qi] == TheoryTestSession.NoAnswer) { GUI.Label(new Rect(x, y, w, H * 0.05f), "You did not answer this one.", _smallStyle); y += H * 0.05f; }
            GUI.Label(new Rect(x, y, w, H * 0.14f), q.Explanation + (q.HighwayCodeRule.Length > 0 ? $"  (Highway Code rule {q.HighwayCodeRule})" : ""), _textStyle);

            float bw = W * 0.2f, bh = H * 0.09f, by = H - bh - H * 0.03f;
            GUI.enabled = _theoryWrongIndex > 0;
            if (GUI.Button(new Rect(x, by, bw, bh), "Previous", _buttonStyle)) _theoryWrongIndex--;
            GUI.enabled = true;
            if (GUI.Button(new Rect(W * 0.5f - bw / 2, by, bw, bh), "Result", _buttonStyle)) SetMode(Mode.TheoryResult);
            GUI.enabled = _theoryWrongIndex < wrong.Count - 1;
            if (GUI.Button(new Rect(x + w - bw, by, bw, bh), "Next", _buttonStyle)) _theoryWrongIndex++;
            GUI.enabled = true;
        }
    }
}
