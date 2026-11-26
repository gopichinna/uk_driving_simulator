using System.IO;
using System.Linq;
using LToLocal.Rules;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    public class ReviewTests
    {
        private static string Content(string rel)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return File.ReadAllText(Path.Combine(dir!.FullName, "content", rel));
        }

        [Test]
        public void FaultsComeBackOnTheSpacedSchedule()
        {
            var (intervals, max) = ReviewSchedule.SettingsFromProgression(Content("lessons/gb/progression.json"));
            Assert.That(intervals, Is.EqualTo(new[] { 1, 3, 7, 14 }));
            var r = ReviewSchedule.Parse("", intervals, max);
            r.RecordFaults(new[] { "GB.TL.RED" }, today: 100);
            Assert.That(r.Due(100, 0), Is.Empty, "not due the same day");
            Assert.That(r.Due(101, 0), Is.EqualTo(new[] { "GB.TL.RED" }));
            r.RecordReview("GB.TL.RED", clean: true, today: 101);
            Assert.That(r.Due(103, 0), Is.Empty);
            Assert.That(r.Due(104, 0), Is.EqualTo(new[] { "GB.TL.RED" }), "next interval is 3 days");
            r.RecordReview("GB.TL.RED", clean: false, today: 104);
            Assert.That(r.Due(105, 0), Is.EqualTo(new[] { "GB.TL.RED" }), "a fault starts the schedule again");
            foreach (int day in new[] { 105, 108, 115, 129 }) r.RecordReview("GB.TL.RED", true, day);
            Assert.That(r.Count, Is.EqualTo(0), "learned after the last interval");
        }

        [Test]
        public void CapsReviewsPerDayAndSurvivesSaving()
        {
            var r = new ReviewSchedule { MaxPerDay = 3 };
            r.RecordFaults(new[] { "A", "B", "C", "D" }, 10);
            Assert.That(r.Due(11, 0).Count, Is.EqualTo(3));
            Assert.That(r.Due(11, 2).Count, Is.EqualTo(1));
            var back = ReviewSchedule.Parse(r.Serialize());
            Assert.That(back.Due(11, 0), Is.EquivalentTo(r.Due(11, 0)));
        }

        [Test]
        public void FindsAShortPracticeDriveForEachRule()
        {
            var idx = RuleIndex.FromJson(Content("lessons/gb/rule_index.json"));
            var e = idx.PracticeFor("GB.JCT.BOX", _ => true)!;
            Assert.That(e.ScenarioId, Does.StartWith("s2_10"));
            var gap = idx.PracticeFor("GB.GAP.TWO_SECOND", x => !x.ScenarioId.StartsWith("s0_"));
            Assert.That(gap!.LessonId, Does.Not.Contain("mock"));
            Assert.That(idx.PracticeFor("GB.NOT.A_RULE", _ => true), Is.Null);
        }
    }
}
