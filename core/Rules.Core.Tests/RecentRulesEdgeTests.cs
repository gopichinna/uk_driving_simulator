using System;
using System.Collections.Generic;
using System.Linq;
using LToLocal.Rules;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    /// <summary>Edge cases for the rules added on 2026-11-27: STOP_SIGN, SCH.SLOW, TURN_NEAR and the theory test.</summary>
    public class StopSignEdgeTests
    {
        /// <summary>Drives north at 3 m/s; if (stopLo, stopHi) is given, holds still for 1 s the first time the front is in that window.</summary>
        private static List<RuleEvent> Drive_(StopSignRule rule, double? stopLo = null, double? stopHi = null, double startZ = -30, double endZ = 8, double heading = 0, double cruise = 3)
        {
            var events = new List<RuleEvent>();
            var s = Drive.At(startZ, cruise, 0, heading);
            double held = 0;
            for (double t = 0; t < 60 && CarBody.Front(s).Z < endZ; t += Drive.Dt)
            {
                double fz = CarBody.Front(s).Z;
                bool inWindow = stopLo.HasValue && fz >= stopLo && fz <= stopHi;
                s.SpeedMps = inWindow && held < 1.0 ? 0 : cruise;
                if (inWindow && held < 1.0) held += Drive.Dt;
                rule.OnFrame(new DriverFrame(t, s, 0), events);
                s = Drive.Advance(s);
            }
            return events;
        }

        private static StopSignRule Rule() => new StopSignRule(new List<LaneLine> { LaneLine.Across(new Vec2(0, 0), 0) });

        [Test]
        public void StoppingTooFarBackDoesNotCount()
        {
            // Halts 12 m before the line (outside the 6 m approach zone) then rolls through.
            var e = Drive_(Rule(), -13, -11);
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void StoppingExactlyAtTheEdgeOfTheApproachZoneCounts()
        {
            var e = Drive_(Rule(), -5.5, -4.5);
            Assert.That(e.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        }

        [Test]
        public void CrossTrafficLineAcrossAnotherHeadingIsIgnored()
        {
            // A line meant for eastbound traffic must not judge a northbound car crossing it.
            var rule = new StopSignRule(new List<LaneLine> { LaneLine.Across(new Vec2(0, 0), Math.PI / 2) });
            Assert.That(Drive_(rule, cruise: 6), Is.Empty);
        }

        [Test]
        public void ResetForgetsAnEarlierStop()
        {
            var rule = Rule();
            var stopFirst = Drive_(rule, -5, -3);
            Assert.That(stopFirst.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
            rule.Reset();
            var rolling = Drive_(rule);
            Assert.That(rolling.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void EachCrossingIsJudgedOnItsOwn()
        {
            var rule = Rule();
            var first = Drive_(rule, -5, -3);
            var second = Drive_(rule); // no Reset: the earlier stop must not carry over
            Assert.That(first.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
            Assert.That(second.Single().Severity, Is.EqualTo(Severity.Serious));
        }
    }

    public class SchoolSlowEdgeTests
    {
        private static List<RuleEvent> Pass(SchoolSlowRule rule, Func<double, double> mphAtZ, double startZ = -150, double endZ = 150, List<Pedestrian>? people = null)
        {
            var events = new List<RuleEvent>();
            var s = Drive.At(startZ, 10);
            for (double t = 0; s.Z < endZ; t += Drive.Dt)
            {
                s.SpeedMps = mphAtZ(s.Z) / 2.236936;
                rule.OnFrame(new DriverFrame(t, s, 0), people ?? new List<Pedestrian>(), events);
                s = Drive.Advance(s);
            }
            return events;
        }

        private static SchoolSlowRule Rule(params double[] zs) => new SchoolSlowRule(zs.Select(z => new Vec2(0, z)).ToList());

        [Test]
        public void ExactlyTwentyMphIsFine() => Assert.That(Drive.Violations(Pass(Rule(0), _ => 20.0)), Is.Empty);

        [Test]
        public void ABriefBurstUnderTheSustainTimeIsIgnored()
        {
            // 25 mph for about 0.5 s only.
            var e = Pass(Rule(0), z => z > 0 && z < 3.5 ? 25 : 18);
            Assert.That(Drive.Violations(e), Is.Empty);
        }

        [Test]
        public void TwoSchoolsGiveTwoGoodPracticeNotes()
        {
            var e = Pass(Rule(0, 400), _ => 18, endZ: 550, startZ: -150);
            Assert.That(e.Count(x => x.Outcome == RuleOutcome.GoodPractice), Is.EqualTo(2));
        }

        [Test]
        public void PeopleArrivingLaterRaiseAMinorFaultToSerious()
        {
            var rule = Rule(0);
            var events = new List<RuleEvent>();
            var s = Drive.At(-90, 30 / 2.236936);
            for (double t = 0; s.Z < 60; t += Drive.Dt)
            {
                var people = new List<Pedestrian>();
                if (s.Z > 20) people.Add(new Pedestrian { Position = new Vec2(2, s.Z + 3) });
                rule.OnFrame(new DriverFrame(t, s, 0), people, events);
                s = Drive.Advance(s);
            }
            Assert.That(events.Select(e => e.Severity), Is.EqualTo(new[] { Severity.Driving, Severity.Serious }));
        }

        [Test]
        public void SlowingBeforeTheZoneAndSpeedingAfterIsNotAFault()
        {
            var e = Pass(Rule(0), z => z < 100 ? 18 : 35);
            Assert.That(Drive.Violations(e), Is.Empty);
        }

        [Test]
        public void ResetStartsAFreshZone()
        {
            var rule = Rule(0);
            var first = Pass(rule, _ => 30);
            Assert.That(first, Is.Not.Empty);
            rule.Reset();
            Assert.That(Pass(rule, _ => 30).Count, Is.EqualTo(first.Count));
        }
    }

    public class LeftTurnSignalEdgeTests
    {
        private static List<RuleEvent> Turn(DrivingSide side, int dirSign, Func<double, int> indicator)
        {
            // dirSign = -1 turns towards heading 0 from east (a left turn); +1 turns the other way, south of east.
            var rule = new LeftTurnSignalRule(new List<Vec2> { new Vec2(0, 0) }, side);
            var events = new List<RuleEvent>();
            var s = new CarState { X = -30, Z = 0, Heading = Math.PI / 2, SpeedMps = 5 };
            for (double t = 0; t < 14; t += Drive.Dt)
            {
                if (s.X > -8 && Math.Abs(s.Heading - Math.PI / 2) < Math.PI / 2) s.Heading += dirSign * 1.2 * Drive.Dt;
                rule.OnFrame(new DriverFrame(t, s, indicator(t)), new List<RoadUser>(), events);
                s.X += Math.Sin(s.Heading) * 5 * Drive.Dt;
                s.Z += Math.Cos(s.Heading) * 5 * Drive.Dt;
            }
            return events;
        }

        [Test]
        public void ARightTurnIsNotJudgedByTheLeftTurnRule()
        {
            Assert.That(Turn(DrivingSide.Left, +1, _ => 0), Is.Empty);
        }

        [Test]
        public void RightHandTrafficJudgesTheMirrorImage()
        {
            // In right-hand traffic the nearside turn is to the right (+1) and needs the right indicator (+1).
            var good = Turn(DrivingSide.Right, +1, t => t > 1 ? 1 : 0);
            Assert.That(good.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
            var bad = Turn(DrivingSide.Right, +1, _ => 0);
            Assert.That(bad.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void WrongWayIndicatorCountsAsNoSignal()
        {
            var e = Turn(DrivingSide.Left, -1, t => t > 1 ? 1 : 0); // right indicator on while turning left
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void SignalCancelledBeforeTheTurnStartsCountsAsLate()
        {
            var e = Turn(DrivingSide.Left, -1, t => t > 1 && t < 3 ? -1 : 0);
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }
    }

    public class TurnRuleRefinementTests
    {
        [Test]
        public void LeftTurnWithoutSignalNextToACyclistIsSerious()
        {
            var rule = new LeftTurnSignalRule(new List<Vec2> { new Vec2(0, 0) });
            var events = new List<RuleEvent>();
            var s = new CarState { X = -30, Z = 0, Heading = Math.PI / 2, SpeedMps = 5 };
            for (double t = 0; t < 14; t += Drive.Dt)
            {
                if (s.X > -8 && s.Heading > 0) s.Heading = Math.Max(0, s.Heading - 1.2 * Drive.Dt);
                var users = new List<RoadUser> { new RoadUser(1, new Vec2(s.X + 6, 2.5), Math.PI / 2, 4, true) }; // cyclist ahead, on the nearside (north)
                rule.OnFrame(new DriverFrame(t, s, 0), users, events);
                s.X += Math.Sin(s.Heading) * 5 * Drive.Dt;
                s.Z += Math.Cos(s.Heading) * 5 * Drive.Dt;
            }
            Assert.That(events.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void RightTurnGapIsJudgedWhenTheTurnIsCommittedNotWhileYouFinishIt()
        {
            // A car 3.8 s away when the turn starts closes to under 2 s while the player is still crossing: no fault, nobody braked.
            var rule = new RightTurnGapRule(new List<Vec2> { new Vec2(0, 0) });
            var events = new List<RuleEvent>();
            var s = new CarState { X = -30, Z = 0, Heading = Math.PI / 2, SpeedMps = 5 };
            double ox = 120;
            for (double t = 0; t < 14; t += Drive.Dt)
            {
                if (s.X > -8 && s.Heading < Math.PI) s.Heading = Math.Min(Math.PI, s.Heading + 0.6 * Drive.Dt);
                ox -= 10 * Drive.Dt;
                var users = new List<RoadUser> { new RoadUser(1, new Vec2(ox, 0), -Math.PI / 2, 10, false) };
                rule.OnFrame(new DriverFrame(t, s, 1), users, events);
                s.X += Math.Sin(s.Heading) * 5 * Drive.Dt;
                s.Z += Math.Cos(s.Heading) * 5 * Drive.Dt;
            }
            Assert.That(Drive.Violations(events), Is.Empty);
        }
    }

    public class TheoryTestEdgeTests
    {
        private static TheorySetDef Set(int n = 3, int pass = 2, int minutes = 1)
        {
            string Q(int i) => $"{{\"id\":\"q{i}\",\"rule\":\"GB.R{i % 2}\",\"question\":\"Q?\",\"options\":[\"a\",\"b\"],\"answer\":0,\"explain\":\"e\"}}";
            return TheorySetDef.FromJson("{\"id\":\"s\",\"setNumber\":2,\"passMark\":" + pass + ",\"timeLimitMinutes\":" + minutes + ",\"questions\":[" + string.Join(",", Enumerable.Range(0, n).Select(Q)) + "]}");
        }

        [Test]
        public void MissingPassMarkAndTimeFallBackToTheOfficialRules()
        {
            var s = TheorySetDef.FromJson("{\"id\":\"s\",\"questions\":[" + string.Join(",", Enumerable.Range(0, 50).Select(i => "{\"id\":\"q" + i + "\",\"question\":\"?\",\"options\":[\"a\",\"b\"],\"answer\":0}")) + "]}");
            Assert.That(s.PassMark, Is.EqualTo(43));
            Assert.That(s.TimeLimitMinutes, Is.EqualTo(57));
        }

        [Test]
        public void NothingChangesAfterFinishing()
        {
            var t = new TheoryTestSession(Set());
            t.Choose(0);
            t.Finish();
            double left = t.TimeLeftS;
            t.Tick(30);
            t.GoTo(2);
            t.ToggleFlag();
            Assert.That(t.TimeLeftS, Is.EqualTo(left));
            Assert.That(t.Index, Is.EqualTo(0));
            Assert.That(t.FlaggedCount, Is.EqualTo(0));
            t.Finish();
            Assert.That(t.Score, Is.EqualTo(1), "finishing twice does not change the score");
        }

        [Test]
        public void ScoreIsExactlyThePassMarkPasses()
        {
            var t = new TheoryTestSession(Set(3, 2));
            t.Choose(0); t.GoTo(1); t.Choose(0); // two right, one blank
            t.Finish();
            Assert.That(t.Score, Is.EqualTo(2));
            Assert.That(t.Passed, Is.True);
        }

        [Test]
        public void WrongQuestionsAndTopicsAreEmptyBeforeFinishing()
        {
            var t = new TheoryTestSession(Set());
            Assert.That(t.WrongQuestions(), Is.Empty);
            Assert.That(t.Finished, Is.False);
        }

        [Test]
        public void TimeCountsDownAndIsNeverNegative()
        {
            var t = new TheoryTestSession(Set(minutes: 1));
            t.Tick(10);
            Assert.That(t.TimeLeftS, Is.EqualTo(50).Within(1e-9));
            t.Tick(1000);
            Assert.That(t.TimeLeftS, Is.EqualTo(0));
            Assert.That(t.TimedOut, Is.True);
        }

        [Test]
        public void ManuallyFinishingIsNotATimeout()
        {
            var t = new TheoryTestSession(Set());
            t.Tick(5);
            t.Finish();
            Assert.That(t.TimedOut, Is.False);
        }
    }

    public class NewRulesInTownTests
    {
        [Test]
        public void StraightDrivingThroughTheTownRaisesNoNewRuleFaults()
        {
            var town = new TownGeometry();
            var rules = new TownRules(town.Graph, DrivingSide.Left);
            var events = new List<RuleEvent>();
            var s = new CarState { X = -150, Z = 1.75, Heading = Math.PI / 2, SpeedMps = 8 };
            var lights = Enumerable.Repeat(LightState.Green, town.Graph.Nodes.Count + 4).ToList();
            for (double t = 0; t < 12; t += Drive.Dt)
            {
                rules.OnFrame(new DriverFrame(t, s, 0), lights, new List<Pedestrian>(), new List<RoadUser>(), events);
                s.X += Math.Sin(s.Heading) * s.SpeedMps * Drive.Dt;
            }
            Assert.That(events.Where(e => e.RuleId == LeftTurnSignalRule.RuleId || e.RuleId == StopSignRule.RuleId), Is.Empty);
        }

        [Test]
        public void TheTownRuleSetExposesTheNewRules()
        {
            var rules = new TownRules(new TownGeometry().Graph, DrivingSide.Left);
            Assert.That(rules.StopSign, Is.Not.Null);
            Assert.That(rules.LeftTurn, Is.Not.Null);
            rules.Reset();
        }
    }
}
