using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>A lesson (content/schemas/lesson.v0): learn card → driving scenarios → quiz → recap.</summary>
    public sealed class LessonDef
    {
        public sealed class ScenarioRef
        {
            public string Id = "", Course = "", Anchor = "", Goal = "";
            public List<string> RulesJudged = new List<string>();
            public int MaxMinor = 2, MaxSerious = 0;
        }

        public string Id = "", Title = "", GovUkUrl = "", Diagram = "";
        public int Stage, Order;
        public bool FreeTier;
        public List<string> LearnBlocks = new List<string>();
        public List<string> RuleIds = new List<string>();
        public List<ScenarioRef> Scenarios = new List<ScenarioRef>();
        public List<string> Recap = new List<string>();

        public static LessonDef FromJson(string json)
        {
            var o = MiniJson.Obj(MiniJson.Parse(json));
            var l = new LessonDef
            {
                Id = S(o, "id"), Title = S(o, "title"), Stage = (int)N(o, "stage"), Order = (int)N(o, "order"),
                FreeTier = o.Get("freeTier") is bool b && b,
            };
            if (o.Get("learnCard") is Dictionary<string, object?> card)
            {
                l.GovUkUrl = S(card, "govUkUrl");
                l.Diagram = S(card, "diagram");
                Strings(card.Get("blocks"), l.LearnBlocks);
                Strings(card.Get("ruleIds"), l.RuleIds);
            }
            Strings(o.Get("recap"), l.Recap);
            if (o.Get("scenarios") is List<object?> scs)
                foreach (var x in scs)
                {
                    var so = MiniJson.Obj(x);
                    var r = new ScenarioRef { Id = S(so, "id"), Course = S(so, "course"), Anchor = S(so, "anchor"), Goal = S(so, "goal") };
                    Strings(so.Get("rulesJudged"), r.RulesJudged);
                    if (so.Get("passCriteria") is Dictionary<string, object?> pc)
                    {
                        r.MaxMinor = (int)N(pc, "maxMinor", 2);
                        r.MaxSerious = (int)N(pc, "maxSerious", 0);
                    }
                    l.Scenarios.Add(r);
                }
            return l;
        }

        internal static string S(Dictionary<string, object?> o, string k) => o.Get(k) as string ?? "";
        internal static double N(Dictionary<string, object?> o, string k, double fallback = 0) => o.Get(k) is double d ? d : fallback;
        internal static void Strings(object? v, List<string> into)
        {
            if (v is List<object?> list) foreach (var x in list) if (x is string s) into.Add(s);
        }
    }

    /// <summary>One scripted event of a scenario (content/schemas/scenario.v0); fields kept as parsed.</summary>
    public sealed class ScenarioEvent
    {
        public string Type = "";
        public Dictionary<string, object?> Fields = new Dictionary<string, object?>();
        public string Str(string k) => Fields.Get(k) as string ?? "";
        public double Num(string k, double fallback = 0) => Fields.Get(k) is double d ? d : fallback;
    }

    public sealed class ScenarioDef
    {
        public string Id = "", Course = "", Anchor = "";
        public double DurationS = 120;
        public List<ScenarioEvent> Events = new List<ScenarioEvent>();

        public static ScenarioDef FromJson(string json)
        {
            var o = MiniJson.Obj(MiniJson.Parse(json));
            var s = new ScenarioDef { Id = LessonDef.S(o, "scenarioId"), Course = LessonDef.S(o, "course"), Anchor = LessonDef.S(o, "anchor"), DurationS = LessonDef.N(o, "durationS", 120) };
            if (o.Get("events") is List<object?> evs)
                foreach (var e in evs)
                {
                    var eo = MiniJson.Obj(e);
                    s.Events.Add(new ScenarioEvent { Type = LessonDef.S(eo, "type"), Fields = eo });
                }
            return s;
        }
    }

    public sealed class QuizDef
    {
        public sealed class Question
        {
            public string Id = "", Text = "", Explanation = "", RuleId = "";
            public List<string> Options = new List<string>();
            public int AnswerIndex;
        }

        public string LessonId = "";
        public List<Question> Questions = new List<Question>();

        public static QuizDef FromJson(string json)
        {
            var o = MiniJson.Obj(MiniJson.Parse(json));
            var q = new QuizDef { LessonId = LessonDef.S(o, "lessonId") };
            if (o.Get("questions") is List<object?> qs)
                foreach (var x in qs)
                {
                    var qo = MiniJson.Obj(x);
                    var question = new Question
                    {
                        Id = LessonDef.S(qo, "id"), Text = LessonDef.S(qo, "question"), Explanation = LessonDef.S(qo, "explanation"),
                        RuleId = LessonDef.S(qo, "ruleId"), AnswerIndex = (int)LessonDef.N(qo, "answerIndex"),
                    };
                    LessonDef.Strings(qo.Get("options"), question.Options);
                    q.Questions.Add(question);
                }
            return q;
        }
    }

    /// <summary>How one scenario went.</summary>
    public sealed class ScenarioResult
    {
        public string ScenarioId = "";
        public bool Completed;
        public string Note = "";
        public DriveSummary Summary = new DriveSummary();

        /// <summary>Passed if it was completed within the scenario's fault limits (any dangerous fault fails).</summary>
        public bool Passed(LessonDef.ScenarioRef r) =>
            Completed && Summary.Dangerous == 0 && Summary.Serious <= r.MaxSerious && Summary.Driving <= r.MaxMinor;
    }

    /// <summary>
    /// Stars for a lesson attempt (until content/lessons/gb/progression.json defines them):
    /// 1 = finished every scenario; 2 = also passed every scenario; 3 = also no faults at all and the quiz all correct.
    /// </summary>
    public static class Stars
    {
        public static int For(LessonDef lesson, IReadOnlyList<ScenarioResult> results, bool quizAllCorrect)
        {
            if (results.Count < lesson.Scenarios.Count) return 0;
            bool all = true, passedAll = true, clean = true;
            for (int i = 0; i < lesson.Scenarios.Count; i++)
            {
                var r = results[i];
                if (!r.Completed) all = false;
                if (!r.Passed(lesson.Scenarios[i])) passedAll = false;
                if (r.Summary.Driving + r.Summary.Serious + r.Summary.Dangerous > 0) clean = false;
            }
            if (!all) return 0;
            if (!passedAll) return 1;
            return clean && quizAllCorrect ? 3 : 2;
        }
    }

    /// <summary>
    /// How stages unlock (content/lessons/gb/progression.json): a stage opens once the stage it names has earned at
    /// least <c>minStars</c> stars in total across its lessons.
    /// </summary>
    public sealed class Progression
    {
        public sealed class Stage
        {
            public int Number;
            public string Title = "";
            public List<string> LessonIds = new List<string>();
            public int UnlockAfterStage = -1, MinStars;
        }

        public readonly List<Stage> Stages = new List<Stage>();
        public readonly List<string> FreeTier = new List<string>();

        public static Progression FromJson(string json)
        {
            var o = MiniJson.Obj(MiniJson.Parse(json));
            var p = new Progression();
            if (o.Get("stages") is List<object?> stages)
                foreach (var x in stages)
                {
                    var so = MiniJson.Obj(x);
                    var st = new Stage { Number = (int)LessonDef.N(so, "stage"), Title = LessonDef.S(so, "title") };
                    LessonDef.Strings(so.Get("lessonIds"), st.LessonIds);
                    if (so.Get("unlockAfter") is Dictionary<string, object?> u)
                    {
                        st.UnlockAfterStage = (int)LessonDef.N(u, "stage", -1);
                        st.MinStars = (int)LessonDef.N(u, "minStars");
                    }
                    p.Stages.Add(st);
                }
            LessonDef.Strings(o.Get("freeTier"), p.FreeTier);
            return p;
        }

        public Stage? StageOf(int number) => Stages.Find(s => s.Number == number);
    }

    /// <summary>Best stars per lesson, saved on the device as "lessonId=stars;…".</summary>
    public sealed class LessonProgress
    {
        private readonly Dictionary<string, int> _stars = new Dictionary<string, int>();

        public int StarsFor(string lessonId) => _stars.TryGetValue(lessonId, out int s) ? s : 0;

        /// <summary>Keeps the best result; returns true if it improved.</summary>
        public bool Record(string lessonId, int stars)
        {
            if (stars <= StarsFor(lessonId)) return false;
            _stars[lessonId] = stars;
            return true;
        }

        /// <summary>Stars earned in a stage, and how many are needed to unlock it (from the progression data).</summary>
        public int StageStars(Progression.Stage stage)
        {
            int sum = 0;
            foreach (var id in stage.LessonIds) sum += StarsFor(id);
            return sum;
        }

        /// <summary>With progression data: the stage's unlock rule. Without: every lesson of the previous stage has a star.</summary>
        public bool Unlocked(LessonDef lesson, IReadOnlyList<LessonDef> all, Progression? progression)
        {
            var stage = progression?.StageOf(lesson.Stage);
            if (stage == null) return Unlocked(lesson, all);
            if (stage.UnlockAfterStage < 0) return true;
            var before = progression!.StageOf(stage.UnlockAfterStage);
            return before == null || StageStars(before) >= stage.MinStars;
        }

        /// <summary>Stage 0 is always open; a later stage opens when every lesson of the stage before has at least one star.</summary>
        public bool Unlocked(LessonDef lesson, IReadOnlyList<LessonDef> all)
        {
            if (lesson.Stage == 0) return true;
            int previous = -1;
            foreach (var l in all) if (l.Stage < lesson.Stage && l.Stage > previous) previous = l.Stage;
            if (previous < 0) return true;
            foreach (var l in all) if (l.Stage == previous && StarsFor(l.Id) == 0) return false;
            return true;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in _stars) sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            return sb.ToString();
        }

        public static LessonProgress Parse(string s)
        {
            var p = new LessonProgress();
            foreach (var part in (s ?? "").Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && int.TryParse(part.Substring(eq + 1), out int stars)) p._stars[part.Substring(0, eq)] = stars;
            }
            return p;
        }
    }
}
