using System.Collections.Generic;
using LToLocal.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LToLocal.Game
{
    /// <summary>
    /// The lesson in progress, kept across course scenes (each scenario may use a different course).
    /// Lessons, scenarios, quizzes and diagrams come from the content pack, copied into Resources/Content.
    /// </summary>
    public static class LessonSession
    {
        public const string ProgressKey = "progress_lessons";
        public const string LicenceKey = "progress_licence";
        public const string MockTestId = "gb-s8-mock-test";

        private static List<LessonDef> s_lessons;
        private static LessonProgress s_progress;

        public static LessonDef Lesson { get; private set; }
        public static int ScenarioIndex { get; private set; }
        public static readonly List<ScenarioResult> Results = new List<ScenarioResult>();
        public static bool MockTest { get; private set; }
        /// <summary>Set when a scenario scene should start its scenario; cleared once it has.</summary>
        public static bool ScenarioPending { get; set; }
        /// <summary>Show the lesson menu when the next scene starts.</summary>
        public static bool OpenMenuOnLoad { get; set; }

        public static bool Active => Lesson != null || MockTest;
        public static LessonDef.ScenarioRef CurrentRef => Lesson != null && ScenarioIndex < Lesson.Scenarios.Count ? Lesson.Scenarios[ScenarioIndex] : null;

        public static IReadOnlyList<LessonDef> Lessons
        {
            get
            {
                if (s_lessons != null) return s_lessons;
                s_lessons = new List<LessonDef>();
                foreach (var a in Resources.LoadAll<TextAsset>("Content/lessons"))
                {
                    try { s_lessons.Add(LessonDef.FromJson(a.text)); }
                    catch (System.Exception e) { Debug.LogWarning($"[LToLocal] Lesson {a.name}: {e.Message}"); }
                }
                s_lessons.Sort((x, y) => x.Order.CompareTo(y.Order));
                return s_lessons;
            }
        }

        private static Progression s_progression;
        private static bool s_progressionLoaded;

        /// <summary>Unlock rules from the content pack (null if missing: then each previous stage needs a star on every lesson).</summary>
        public static Progression Progression
        {
            get
            {
                if (s_progressionLoaded) return s_progression;
                s_progressionLoaded = true;
                var a = Resources.Load<TextAsset>("Content/progression");
                if (a != null) s_progression = Progression.FromJson(a.text);
                return s_progression;
            }
        }

        public static LessonProgress Progress => s_progress ??= LessonProgress.Parse(PlayerPrefs.GetString(ProgressKey, ""));

        public static bool HasLicence => PlayerPrefs.GetInt(LicenceKey, 0) == 1;

        public static ScenarioDef LoadScenario(string id)
        {
            var a = Resources.Load<TextAsset>($"Content/scenarios/{id}");
            return a != null ? ScenarioDef.FromJson(a.text) : null;
        }

        public static QuizDef LoadQuiz(string lessonId)
        {
            var a = Resources.Load<TextAsset>($"Content/quiz/{lessonId}");
            return a != null ? QuizDef.FromJson(a.text) : null;
        }

        public static Texture2D Diagram(string lessonId) => Resources.Load<Texture2D>($"Content/diagrams/{lessonId}");

        public static void Begin(LessonDef lesson)
        {
            Lesson = lesson;
            MockTest = false;
            ScenarioIndex = 0;
            Results.Clear();
        }

        public static void BeginMockTest()
        {
            Lesson = null;
            MockTest = true;
            Results.Clear();
            ScenarioPending = true;
            SceneManager.LoadScene("Wolverhampton");
        }

        /// <summary>Loads the course for the current scenario; the scene picks it up via <see cref="ScenarioPending"/>.</summary>
        public static void StartScenario()
        {
            var r = CurrentRef;
            if (r == null) return;
            ScenarioPending = true;
            SceneManager.LoadScene(SceneFor(r.Course));
        }

        public static void RecordResult(ScenarioResult r)
        {
            Results.Add(r);
            ScenarioIndex++;
        }

        public static bool MoreScenarios => Lesson != null && ScenarioIndex < Lesson.Scenarios.Count;

        public static int Finish(bool quizAllCorrect)
        {
            int stars = Stars.For(Lesson, Results, quizAllCorrect);
            if (Progress.Record(Lesson.Id, stars)) Save();
            return stars;
        }

        public static void AwardLicence()
        {
            PlayerPrefs.SetInt(LicenceKey, 1);
            PlayerPrefs.Save();
        }

        public static void End()
        {
            Lesson = null;
            MockTest = false;
            ScenarioPending = false;
            Results.Clear();
        }

        public static void ResetAll()
        {
            s_progress = new LessonProgress();
            End();
        }

        private static void Save()
        {
            PlayerPrefs.SetString(ProgressKey, Progress.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Is the course for this lesson's drives in the app yet? (Motorways arrive later.)</summary>
        public static bool Playable(LessonDef lesson)
        {
            foreach (var s in lesson.Scenarios)
                if (!Application.CanStreamedLevelBeLoaded(SceneFor(s.Course))) return false;
            return true;
        }

        /// <summary>Does the stage have at least one lesson whose courses are all in the app?</summary>
        public static bool StagePlayable(int stage)
        {
            foreach (var l in Lessons) if (l.Stage == stage && Playable(l)) return true;
            return false;
        }

        public static string SceneFor(string course) => course switch
        {
            "town" => "Town",
            "two_lane_roundabout" => "TwoLaneRoundabout",
            "roundabout" => "Roundabout",
            "dual_carriageway" => "DualCarriageway",
            "wolverhampton" => "Wolverhampton",
            "wolverhampton-test-area" => "WolverhamptonTestArea",
            "motorway" => "Motorway",
            _ => "Town",
        };
    }
}
