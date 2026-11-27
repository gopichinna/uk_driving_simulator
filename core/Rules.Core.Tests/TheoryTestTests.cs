using System.IO;
using System.Linq;
using LToLocal.Rules;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    public class TheoryTestTests
    {
        private static string Tests()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "content", "quizzes", "gb", "tests");
        }

        // Four questions, correct answers 0, 1, 2, 3; pass mark 3; one minute.
        private static TheorySetDef Tiny()
        {
            string Q(int i) => $"{{\"id\":\"q{i}\",\"rule\":\"{(i < 2 ? "GB.A" : "GB.B")}\",\"question\":\"Q{i}?\",\"options\":[\"a\",\"b\",\"c\",\"d\"],\"answer\":{i},\"explain\":\"e\",\"hc_rule\":\"1\",\"difficulty\":1}}";
            return TheorySetDef.FromJson("{\"id\":\"t\",\"setNumber\":1,\"passMark\":3,\"timeLimitMinutes\":1,\"questions\":[" + string.Join(",", Enumerable.Range(0, 4).Select(Q)) + "]}");
        }

        [Test]
        public void EveryShippedSetLoadsWithTheOfficialFormat()
        {
            var files = Directory.GetFiles(Tests(), "set-*.json");
            Assert.That(files.Length, Is.EqualTo(10));
            foreach (var f in files)
            {
                var set = TheorySetDef.FromJson(File.ReadAllText(f));
                Assert.That(set.Questions.Count, Is.EqualTo(50), f);
                Assert.That(set.PassMark, Is.EqualTo(43), f);
                Assert.That(set.TimeLimitMinutes, Is.EqualTo(57), f);
                foreach (var q in set.Questions)
                {
                    Assert.That(q.Options.Count, Is.EqualTo(4), q.Id);
                    Assert.That(q.AnswerIndex, Is.InRange(0, q.Options.Count - 1), q.Id);
                    Assert.That(q.Text, Is.Not.Empty, q.Id);
                }
            }
        }

        [Test]
        public void ScoresAndPassesAtThePassMark()
        {
            var t = new TheoryTestSession(Tiny());
            for (int i = 0; i < 4; i++) { t.GoTo(i); if (i != 3) t.Choose(i); else t.Choose(0); } // last one wrong
            t.Finish();
            Assert.That(t.Score, Is.EqualTo(3));
            Assert.That(t.Passed, Is.True);
            Assert.That(t.WrongQuestions(), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void FailsBelowThePassMark()
        {
            var t = new TheoryTestSession(Tiny());
            t.Choose(0); // only the first, correct
            t.Finish();
            Assert.That(t.Score, Is.EqualTo(1));
            Assert.That(t.Passed, Is.False);
        }

        [Test]
        public void AnswersCanBeChangedAndClearedUntilTheEnd()
        {
            var t = new TheoryTestSession(Tiny());
            t.Choose(2);
            t.Choose(0);
            Assert.That(t.Answers[0], Is.EqualTo(0));
            t.Choose(0); // same again clears
            Assert.That(t.Answers[0], Is.EqualTo(TheoryTestSession.NoAnswer));
            t.Choose(9); // not an option
            Assert.That(t.Answers[0], Is.EqualTo(TheoryTestSession.NoAnswer));
            t.Choose(0);
            t.Finish();
            t.Choose(1);
            Assert.That(t.Answers[0], Is.EqualTo(0), "no changes after finishing");
        }

        [Test]
        public void NavigationIsClampedAndFlagsAreRemembered()
        {
            var t = new TheoryTestSession(Tiny());
            t.Previous();
            Assert.That(t.Index, Is.EqualTo(0));
            t.ToggleFlag();
            t.GoTo(99);
            Assert.That(t.Index, Is.EqualTo(3));
            Assert.That(t.FlaggedCount, Is.EqualTo(1));
            t.GoTo(1);
            t.Choose(1);
            t.GoTo(0);
            t.Choose(0); // answered but flagged
            Assert.That(t.NeedsReview(), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(t.UnansweredCount, Is.EqualTo(2));
        }

        [Test]
        public void RunningOutOfTimeFinishesTheTestAndLeavesBlanksWrong()
        {
            var t = new TheoryTestSession(Tiny());
            t.Choose(0);
            t.Tick(59.0);
            Assert.That(t.Finished, Is.False);
            t.Tick(1.5);
            Assert.That(t.Finished, Is.True);
            Assert.That(t.TimedOut, Is.True);
            Assert.That(t.TimeLeftS, Is.EqualTo(0));
            Assert.That(t.Score, Is.EqualTo(1));
        }

        [Test]
        public void TopicsListTheWeakestFirst()
        {
            var t = new TheoryTestSession(Tiny());
            t.GoTo(2); t.Choose(2); // GB.B: q2 right, q3 blank
            t.Finish();
            var topics = t.Topics();
            Assert.That(topics.Select(x => x.RuleId), Is.EqualTo(new[] { "GB.A", "GB.B" }), "GB.A has 2 wrong, GB.B has 1");
            Assert.That(topics[0].Wrong, Is.EqualTo(2));
            Assert.That(topics[1].Correct, Is.EqualTo(1));
        }
    }
}
