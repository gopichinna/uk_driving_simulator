using System;
using System.Collections.Generic;
using System.Linq;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// A mock theory test (content/quizzes/gb/tests/set-NN.json): 50 questions, a pass mark and a time limit,
    /// built from the question bank. Same shape as the real test: answers can be changed and questions flagged
    /// until you finish; nothing is marked until the end.
    /// </summary>
    public sealed class TheorySetDef
    {
        public sealed class Question
        {
            public string Id = "", Text = "", Explanation = "", RuleId = "", HighwayCodeRule = "";
            public List<string> Options = new List<string>();
            public int AnswerIndex;
            public int Difficulty;
        }

        public string Id = "";
        public int SetNumber, PassMark, TimeLimitMinutes;
        public List<Question> Questions = new List<Question>();

        public static TheorySetDef FromJson(string json)
        {
            var o = MiniJson.Obj(MiniJson.Parse(json));
            var set = new TheorySetDef
            {
                Id = LessonDef.S(o, "id"),
                SetNumber = (int)LessonDef.N(o, "setNumber"),
                PassMark = (int)LessonDef.N(o, "passMark"),
                TimeLimitMinutes = (int)LessonDef.N(o, "timeLimitMinutes"),
            };
            if (o.Get("questions") is List<object?> qs)
                foreach (var x in qs)
                {
                    var qo = MiniJson.Obj(x);
                    var q = new Question
                    {
                        Id = LessonDef.S(qo, "id"), Text = LessonDef.S(qo, "question"), Explanation = LessonDef.S(qo, "explain"),
                        RuleId = LessonDef.S(qo, "rule"), HighwayCodeRule = LessonDef.S(qo, "hc_rule"),
                        AnswerIndex = (int)LessonDef.N(qo, "answer"), Difficulty = (int)LessonDef.N(qo, "difficulty"),
                    };
                    LessonDef.Strings(qo.Get("options"), q.Options);
                    set.Questions.Add(q);
                }
            if (set.PassMark <= 0) set.PassMark = (int)Math.Ceiling(set.Questions.Count * 0.86);
            if (set.TimeLimitMinutes <= 0) set.TimeLimitMinutes = 57;
            return set;
        }
    }

    /// <summary>How well one topic (a rule) went in a finished test.</summary>
    public readonly struct TopicResult
    {
        public TopicResult(string ruleId, int correct, int total)
        {
            RuleId = ruleId;
            Correct = correct;
            Total = total;
        }

        public string RuleId { get; }
        public int Correct { get; }
        public int Total { get; }
        public int Wrong => Total - Correct;
    }

    /// <summary>One attempt at a <see cref="TheorySetDef"/>. Pure logic; the game draws it and calls <see cref="Tick"/>.</summary>
    public sealed class TheoryTestSession
    {
        public const int NoAnswer = -1;

        public TheoryTestSession(TheorySetDef set)
        {
            Set = set;
            Answers = Enumerable.Repeat(NoAnswer, set.Questions.Count).ToArray();
            Flags = new bool[set.Questions.Count];
            TimeLeftS = set.TimeLimitMinutes * 60.0;
        }

        public TheorySetDef Set { get; }
        public int[] Answers { get; }
        public bool[] Flags { get; }
        public int Index { get; private set; }
        public double TimeLeftS { get; private set; }
        public bool Finished { get; private set; }
        public bool TimedOut { get; private set; }
        public int Score { get; private set; }
        public bool Passed { get; private set; }

        public int Count => Set.Questions.Count;
        public TheorySetDef.Question Current => Set.Questions[Index];
        public int AnsweredCount => Answers.Count(a => a != NoAnswer);
        public int UnansweredCount => Count - AnsweredCount;
        public int FlaggedCount => Flags.Count(f => f);

        /// <summary>Advances the clock. Time running out finishes the test (unanswered questions are wrong).</summary>
        public void Tick(double dt)
        {
            if (Finished) return;
            TimeLeftS = Math.Max(0, TimeLeftS - dt);
            if (TimeLeftS <= 0) { TimedOut = true; Finish(); }
        }

        public void GoTo(int index) { if (!Finished) Index = Math.Max(0, Math.Min(Count - 1, index)); }
        public void Next() => GoTo(Index + 1);
        public void Previous() => GoTo(Index - 1);

        /// <summary>Picks an answer for the current question. Choosing the same one again clears it.</summary>
        public void Choose(int option)
        {
            if (Finished || option < 0 || option >= Current.Options.Count) return;
            Answers[Index] = Answers[Index] == option ? NoAnswer : option;
        }

        public void ToggleFlag() { if (!Finished) Flags[Index] = !Flags[Index]; }

        /// <summary>Question numbers (0-based) still unanswered or flagged, in order. Shown on the review screen.</summary>
        public List<int> NeedsReview() =>
            Enumerable.Range(0, Count).Where(i => Answers[i] == NoAnswer || Flags[i]).ToList();

        public bool IsCorrect(int i) => Answers[i] == Set.Questions[i].AnswerIndex;

        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            Score = Enumerable.Range(0, Count).Count(IsCorrect);
            Passed = Score >= Set.PassMark;
        }

        public List<int> WrongQuestions() =>
            Finished ? Enumerable.Range(0, Count).Where(i => !IsCorrect(i)).ToList() : new List<int>();

        /// <summary>Results by topic, weakest (most wrong) first. Only valid once finished.</summary>
        public List<TopicResult> Topics()
        {
            var by = new Dictionary<string, (int correct, int total)>();
            for (int i = 0; i < Count; i++)
            {
                string r = Set.Questions[i].RuleId;
                by.TryGetValue(r, out var v);
                by[r] = (v.correct + (IsCorrect(i) ? 1 : 0), v.total + 1);
            }
            return by.Select(kv => new TopicResult(kv.Key, kv.Value.correct, kv.Value.total))
                     .OrderByDescending(t => t.Wrong).ThenBy(t => t.RuleId, StringComparer.Ordinal).ToList();
        }
    }
}
