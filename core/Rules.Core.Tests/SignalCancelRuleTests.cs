using System;
using System.Collections.Generic;
using System.Linq;
using LToLocal.Rules;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    public class SignalCancelRuleTests
    {
        // Drives east at 8 m/s; turns left (to north) between t = 2 s and t = 3.4 s; the indicator follows ind(t).
        private static List<RuleEvent> Run(Func<double, int> ind, bool turn = true, double seconds = 40, double speed = 8)
        {
            var rule = new SignalCancelRule();
            var events = new List<RuleEvent>();
            var s = new CarState { X = 0, Z = 0, Heading = Math.PI / 2, SpeedMps = speed };
            for (double t = 0; t < seconds; t += Drive.Dt)
            {
                if (turn && t > 2 && t < 3.4) s.Heading -= (Math.PI / 2) / 1.4 * Drive.Dt;
                rule.OnFrame(new DriverFrame(t, s, ind(t)), events);
                s.X += Math.Sin(s.Heading) * s.SpeedMps * Drive.Dt;
                s.Z += Math.Cos(s.Heading) * s.SpeedMps * Drive.Dt;
            }
            return events;
        }

        [Test]
        public void Pass_CancelsSoonAfterTheTurn() => Assert.That(Run(t => t > 0.5 && t < 5.0 ? -1 : 0), Is.Empty);

        [Test]
        public void Minor_LeavesTheIndicatorOnAfterTheTurn()
        {
            var e = Run(t => t > 0.5 ? -1 : 0);
            Assert.That(e.Single().RuleId, Is.EqualTo(SignalCancelRule.RuleId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
            Assert.That(e.Single().Time, Is.InRange(8.0, 10.5), "about 5 s (or 40 m) after the turn finished at 3.4 s");
        }

        [Test]
        public void Minor_ForgottenIndicatorOnAStraightRoad()
        {
            var e = Run(t => t > 1 ? 1 : 0, turn: false);
            Assert.That(e.Single().Time, Is.InRange(10.0, 11.5));
        }

        [Test]
        public void Pass_ASignalOnForFourSecondsBeforeATurnIsNotForgotten()
        {
            var e = Run(t => t > 0.5 && t < 5 ? 1 : 0, turn: false, seconds: 20);
            Assert.That(e, Is.Empty);
        }

        [Test]
        public void Pass_StandingStillAtLightsDoesNotCount() => Assert.That(Run(_ => -1, turn: false, seconds: 60, speed: 0), Is.Empty);

        [Test]
        public void Pass_TurningAllTheWayRoundADriftingRoundaboutIsNotAStraightRun()
        {
            var rule = new SignalCancelRule();
            var events = new List<RuleEvent>();
            var s = new CarState { X = 0, Z = 0, Heading = 0, SpeedMps = 8 };
            for (double t = 0; t < 12; t += Drive.Dt)
            {
                s.Heading += 0.3 * Drive.Dt; // circulating at about 0.3 rad/s
                rule.OnFrame(new DriverFrame(t, s, 1), events);
            }
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void ReportsOnceEachTimeTheIndicatorIsSwitchedOn()
        {
            var e = Run(t => t > 0.5 && t < 20 ? -1 : t > 25 ? 1 : 0, turn: true, seconds: 55);
            Assert.That(e.Count, Is.EqualTo(2));
        }

        [Test]
        public void ResetClearsEverything()
        {
            var rule = new SignalCancelRule();
            var events = new List<RuleEvent>();
            var s = new CarState { Heading = 0, SpeedMps = 8 };
            for (double t = 0; t < 20; t += Drive.Dt) rule.OnFrame(new DriverFrame(t, s, 1), events);
            Assert.That(events.Count, Is.EqualTo(1));
            rule.Reset();
            for (double t = 20; t < 25; t += Drive.Dt) rule.OnFrame(new DriverFrame(t, s, 1), events);
            Assert.That(events.Count, Is.EqualTo(1), "5 s is not yet long enough after a reset");
        }
    }
}
