using System;
using System.Collections.Generic;
using System.Linq;
using LToLocal.Rules;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    /// <summary>Fixtures on the town's High Street (west part): eastbound lane at z = +1.75, westbound at z = -1.75.</summary>
    public class KeepLeftRuleTests
    {
        private static readonly TownGeometry Town = new TownGeometry();
        private const double East = Math.PI / 2;

        /// <summary>Drives east from x = -200 at 10 m/s; z(t) gives the sideways position.</summary>
        private static List<RuleEvent> Run(Func<double, double> z, Func<double, int>? indicator = null, Func<double, List<RoadUser>>? users = null, double seconds = 6)
        {
            var rule = new KeepLeftRule(Town.Graph);
            var events = new List<RuleEvent>();
            for (double t = 0; t < seconds; t += 0.02)
            {
                var s = new CarState { X = -200 + 10 * t, Z = z(t), Heading = East, SpeedMps = 10 };
                rule.OnFrame(new DriverFrame(t, s, indicator?.Invoke(t) ?? 0), users?.Invoke(t) ?? new List<RoadUser>(), events);
            }
            return events;
        }

        private static double Ramp(double t, double from, double to, double start, double dur = 1.0) =>
            t < start ? from : t > start + dur ? to : from + (to - from) * (t - start) / dur;

        [Test]
        public void Pass_KeepingLeft() => Assert.That(Run(_ => 1.75), Is.Empty);

        [Test]
        public void Pass_DriftingNearTheCentreLine() => Assert.That(Run(_ => 0.3), Is.Empty);

        [Test]
        public void Serious_DrivingOnTheWrongSide()
        {
            var e = Run(t => Ramp(t, 1.75, -1.75, 1));
            Assert.That(e.Single().RuleId, Is.EqualTo(KeepLeftRule.KeepLeftId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void Dangerous_OnTheWrongSideWithOncomingTraffic()
        {
            var e = Run(t => Ramp(t, 1.75, -1.75, 1),
                users: t => new List<RoadUser> { new RoadUser(1, new Vec2(-200 + 10 * t + 40, -1.75), -East, 10, false) });
            Assert.That(e.Where(x => x.RuleId == KeepLeftRule.KeepLeftId).Max(x => x.Severity), Is.EqualTo(Severity.Dangerous));
        }

        [Test]
        public void Minor_PassingAParkedCarWithoutSignalling()
        {
            // A car stopped in our lane at x = -150; we pull out round it and back in.
            var parked = new List<RoadUser> { new RoadUser(2, new Vec2(-150, 1.75), East, 0, false) };
            var e = Run(t => t < 3.5 ? Ramp(t, 1.75, -1.2, 2.2, 0.8) : Ramp(t, -1.2, 1.75, 6.2, 0.8), users: _ => parked, seconds: 7.5);
            Assert.That(e.Single().RuleId, Is.EqualTo(KeepLeftRule.SignalId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void Pass_PassingAParkedCarAfterSignalling()
        {
            var parked = new List<RoadUser> { new RoadUser(2, new Vec2(-150, 1.75), East, 0, false) };
            var e = Run(t => t < 3.5 ? Ramp(t, 1.75, -1.2, 2.2, 0.8) : Ramp(t, -1.2, 1.75, 6.2, 0.8),
                indicator: t => t > 0.5 && t < 3.0 ? 1 : 0, users: _ => parked, seconds: 7.5);
            Assert.That(e, Is.Empty);
        }

        [Test]
        public void JunctionAreasAreLeftToTheTurnRules()
        {
            // Crossing the other lane inside the crossroads (e.g. turning right) is not "wrong side".
            var rule = new KeepLeftRule(Town.Graph);
            Assert.That(rule.OnWrongSide(new Vec2(-12, -1.75), East), Is.True, "geometry says wrong side…");
            var events = new List<RuleEvent>();
            for (double t = 0; t < 3; t += 0.02)
                rule.OnFrame(new DriverFrame(t, new CarState { X = -12 + t, Z = -1.75, Heading = East, SpeedMps = 1 }, 0), new List<RoadUser>(), events);
            Assert.That(events, Is.Empty, "…but inside the junction it isn't judged");
        }
    }
}
