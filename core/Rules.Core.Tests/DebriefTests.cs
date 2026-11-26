using System.Collections.Generic;
using System.IO;
using LToLocal.Rules;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    public class DebriefTests
    {
        private static string Content(string rel)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "repo root with content/");
            return File.ReadAllText(Path.Combine(dir!.FullName, "content", rel));
        }

        private static RuleEvent Ev(string id, Severity s, double t, RuleOutcome o = RuleOutcome.Violation) => new RuleEvent(id, o, s, t, "m", "src");

        [Test]
        public void IcuPluralsAndPlaceholders()
        {
            const string p = "{count, plural, one {# minor fault} other {# minor faults}}";
            Assert.That(Icu.Format(p, ("count", 1)), Is.EqualTo("1 minor fault"));
            Assert.That(Icu.Format(p, ("count", 3)), Is.EqualTo("3 minor faults"));
            Assert.That(Icu.Format("Limit {limit} mph", ("limit", 30)), Is.EqualTo("Limit 30 mph"));
        }

        [Test]
        public void ReadsTheRealUiStringsAndRuleStrings()
        {
            var ui = UiStrings.FromJson(Content("countries/gb/strings/en/ui.json"));
            foreach (var key in new[] { "hud.finish_drive", "debrief.title", "debrief.count.driving", "debrief.result.pass", "debrief.result.fail", "settings.steering.label", "course.town" })
                Assert.That(ui.Has(key), key);
            Assert.That(ui.Get("debrief.count.serious", "x", ("count", 2)), Does.StartWith("2 "));

            var rules = RuleStrings.FromJson(Content("countries/gb/strings/en/rules.json"));
            foreach (var id in new[] { "GB.TL.RED", "GB.JCT.GIVE_WAY", "GB.XNG.ZEBRA", "GB.POS.KEEP_LEFT" })
                Assert.That(rules.For(id)?.Title, Is.Not.Empty, id);
        }

        [Test]
        public void TestMarkingMatchesGovUk()
        {
            var m = TestMarking.FromYaml(Content("countries/gb/test_marking.yaml"));
            Assert.That(m.MaxDrivingFaults, Is.EqualTo(15));
            Assert.That(m.FailOnSerious && m.FailOnDangerous);
            Assert.That(m.PassSummary, Does.Contain("15"));
            Assert.That(m.Passed(15, 0, 0), Is.True);
            Assert.That(m.Passed(16, 0, 0), Is.False);
            Assert.That(m.Passed(0, 1, 0), Is.False);
            Assert.That(m.Passed(0, 0, 1), Is.False);
        }

        [Test]
        public void SummaryGroupsFaultsWorstFirst()
        {
            var events = new List<RuleEvent>
            {
                Ev("GB.SPD.OVER_LIMIT", Severity.Driving, 5),
                Ev("GB.TL.RED", Severity.Serious, 20),
                Ev("GB.SPD.OVER_LIMIT", Severity.Driving, 30),
                Ev("GB.TL.RED", Severity.None, 40, RuleOutcome.GoodPractice),
            };
            var s = DriveSummary.From(events, new TestMarking());
            Assert.That((s.Driving, s.Serious, s.Dangerous, s.Good), Is.EqualTo((2, 1, 0, 1)));
            Assert.That(s.Passed, Is.False);
            Assert.That(s.Groups[0].RuleId, Is.EqualTo("GB.TL.RED"));
            Assert.That(s.Groups[1].Faults.Count, Is.EqualTo(2));
        }
    }
}
