using System;
using System.Collections.Generic;
using System.Linq;
using LToLocal.Rules;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    /// <summary>Fixtures: drive north (+Z) along x = 0 unless stated. Lines are at given z.</summary>
    internal static class Drive
    {
        public const double Dt = 0.02;

        public static CarState At(double z, double speed, double x = 0, double heading = 0) =>
            new CarState { X = x, Z = z, Heading = heading, SpeedMps = speed };

        public static CarState Advance(CarState s, double dt = Dt)
        {
            s.X += Math.Sin(s.Heading) * s.SpeedMps * dt;
            s.Z += Math.Cos(s.Heading) * s.SpeedMps * dt;
            return s;
        }

        public static List<RuleEvent> Violations(List<RuleEvent> e) => e.Where(x => x.Outcome == RuleOutcome.Violation).ToList();
    }

    public class TrafficLightRuleTests
    {
        private static TrafficLightRule WithBox() => new TrafficLightRule(new List<TrafficLightRule.Approach>
        {
            new TrafficLightRule.Approach(0, LaneLine.Across(new Vec2(0, 0), 0), LaneLine.Across(new Vec2(0, 5), 0), new Vec2(0, 13)),
        });

        private static TrafficLightRule NoBox() => new TrafficLightRule(new List<TrafficLightRule.Approach>
        {
            new TrafficLightRule.Approach(0, LaneLine.Across(new Vec2(0, 0), 0), null, new Vec2(0, 8)),
        });

        private static List<RuleEvent> Run(TrafficLightRule rule, Func<double, LightState> light, CarState s, double seconds, List<RoadUser>? users = null)
        {
            var events = new List<RuleEvent>();
            var lights = new List<LightState> { LightState.Green };
            for (double t = 0; t < seconds; t += Drive.Dt)
            {
                lights[0] = light(t);
                rule.OnFrame(new DriverFrame(t, s, 0), lights, users ?? new List<RoadUser>(), events);
                s = Drive.Advance(s);
            }
            return events;
        }

        [Test]
        public void Pass_GreenLight()
        {
            var e = Run(WithBox(), _ => LightState.Green, Drive.At(-30, 12), 5);
            Assert.That(Drive.Violations(e), Is.Empty);
        }

        [Test]
        public void Serious_RedLightThroughTheCycleBoxGivesBothFaults()
        {
            var e = Drive.Violations(Run(WithBox(), _ => LightState.Red, Drive.At(-30, 12), 5));
            Assert.That(e.Select(x => x.RuleId), Is.EquivalentTo(new[] { TrafficLightRule.AslId, TrafficLightRule.RedId }));
            Assert.That(e.All(x => x.Severity == Severity.Serious));
        }

        [Test]
        public void Serious_StoppingInsideTheCycleBoxOnRed()
        {
            var rule = WithBox();
            var events = new List<RuleEvent>();
            var s = Drive.At(-20, 8);
            var lights = new List<LightState> { LightState.Red };
            for (double t = 0; t < 6; t += Drive.Dt)
            {
                rule.OnFrame(new DriverFrame(t, s, 0), lights, new List<RoadUser>(), events);
                if (CarBody.Front(s).Z > 2.5) s.SpeedMps = 0; // stops inside the box
                s = Drive.Advance(s);
            }
            var v = Drive.Violations(events);
            Assert.That(v.Single().RuleId, Is.EqualTo(TrafficLightRule.AslId));
        }

        [Test]
        public void Dangerous_RedLightWithCrossingTraffic()
        {
            var crossing = new List<RoadUser> { new RoadUser(1, new Vec2(-5, 8), Math.PI / 2, 10, false) };
            var e = Drive.Violations(Run(NoBox(), _ => LightState.Red, Drive.At(-30, 12), 5, crossing));
            Assert.That(e.Single().RuleId, Is.EqualTo(TrafficLightRule.RedId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Dangerous));
        }

        [Test]
        public void Serious_GoingOnRedAndAmber()
        {
            var e = Drive.Violations(Run(NoBox(), _ => LightState.RedAmber, Drive.At(-20, 10), 4));
            Assert.That(e.Single().RuleId, Is.EqualTo(TrafficLightRule.RedAmberId));
        }

        [Test]
        public void Minor_AmberWhenThereWasRoomToStop()
        {
            // 13 m/s needs about 28 m to stop at 3 m/s². Amber comes on 45 m before the line.
            double amberAt = (60 - 2.15 - 45) / 13.0;
            var e = Drive.Violations(Run(NoBox(), t => t < amberAt ? LightState.Green : LightState.Amber, Drive.At(-60, 13), 6));
            Assert.That(e.Single().RuleId, Is.EqualTo(TrafficLightRule.AmberId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void Pass_AmberWhenTooCloseToStopSafely()
        {
            double amberAt = (60 - 2.15 - 10) / 13.0; // 10 m from the line
            var e = Drive.Violations(Run(NoBox(), t => t < amberAt ? LightState.Green : LightState.Amber, Drive.At(-60, 13), 6));
            Assert.That(e, Is.Empty);
        }

        [Test]
        public void Good_StoppingAtTheFirstLine()
        {
            var rule = WithBox();
            var events = new List<RuleEvent>();
            var s = Drive.At(-20, 6);
            var lights = new List<LightState> { LightState.Red };
            for (double t = 0; t < 8; t += Drive.Dt)
            {
                rule.OnFrame(new DriverFrame(t, s, 0), lights, new List<RoadUser>(), events);
                if (CarBody.Front(s).Z > -1.0) s.SpeedMps = 0;
                s = Drive.Advance(s);
            }
            Assert.That(Drive.Violations(events), Is.Empty);
            Assert.That(events.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        }
    }

    public class SchoolSlowRuleTests
    {
        // School sign at (0, 0). The car drives north along x = 0 from z = -150 to z = 150.
        private static List<RuleEvent> Run(double mph, bool person = false)
        {
            var rule = new SchoolSlowRule(new List<Vec2> { new Vec2(0, 0) });
            var events = new List<RuleEvent>();
            var s = Drive.At(-150, mph / 2.236936);
            for (double t = 0; s.Z < 150; t += Drive.Dt)
            {
                var people = new List<Pedestrian>();
                if (person) people.Add(new Pedestrian { Position = new Vec2(3, s.Z + 5) });
                rule.OnFrame(new DriverFrame(t, s, 0), people, events);
                s = Drive.Advance(s);
            }
            return events;
        }

        [Test]
        public void Pass_TwentyMphGivesGoodPractice()
        {
            var e = Run(19);
            Assert.That(Drive.Violations(e), Is.Empty);
            Assert.That(e.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        }

        [Test]
        public void Minor_ThirtyMphPastTheSchool()
        {
            var e = Run(30);
            Assert.That(e.Single().RuleId, Is.EqualTo(SchoolSlowRule.RuleId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void Serious_ThirtyMphWithPeopleNearby() =>
            Assert.That(Run(30, person: true).Last().Severity, Is.EqualTo(Severity.Serious));

        [Test]
        public void Pass_FarFromTheSchoolNothingHappens()
        {
            var rule = new SchoolSlowRule(new List<Vec2> { new Vec2(500, 0) });
            var events = new List<RuleEvent>();
            var s = Drive.At(-150, 13);
            for (double t = 0; s.Z < 150; t += Drive.Dt) { rule.OnFrame(new DriverFrame(t, s, 0), new List<Pedestrian>(), events); s = Drive.Advance(s); }
            Assert.That(events, Is.Empty);
        }
    }

    public class LeftTurnSignalRuleTests
    {
        // Junction centre at the origin. Car drives east (heading +90 degrees) and turns left to head north (0).
        private static List<RuleEvent> Run(Func<double, int> indicator)
        {
            var rule = new LeftTurnSignalRule(new List<Vec2> { new Vec2(0, 0) });
            var events = new List<RuleEvent>();
            var s = new CarState { X = -30, Z = 0, Heading = Math.PI / 2, SpeedMps = 5 };
            for (double t = 0; t < 14; t += Drive.Dt)
            {
                if (s.X > -8) s.Heading = Math.Max(0, s.Heading - 1.2 * Drive.Dt); // turning left once in the junction
                rule.OnFrame(new DriverFrame(t, s, indicator(t)), events);
                s.X += Math.Sin(s.Heading) * s.SpeedMps * Drive.Dt;
                s.Z += Math.Cos(s.Heading) * s.SpeedMps * Drive.Dt;
            }
            return events;
        }

        [Test]
        public void Pass_SignalsLeftEarly()
        {
            var e = Run(t => t > 1 ? -1 : 0); // on from t = 1 s; the turn starts near t = 4.6 s
            Assert.That(Drive.Violations(e), Is.Empty);
            Assert.That(e.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        }

        [Test]
        public void Minor_NoSignalAtAll()
        {
            var e = Run(_ => 0);
            Assert.That(e.Single().RuleId, Is.EqualTo(LeftTurnSignalRule.RuleId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void Minor_SignalsOnlyAsTheTurnStarts()
        {
            var e = Run(t => t > 4.4 ? -1 : 0);
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void Pass_DrivingStraightAcrossNeedsNoSignal()
        {
            var rule = new LeftTurnSignalRule(new List<Vec2> { new Vec2(0, 0) });
            var events = new List<RuleEvent>();
            var s = new CarState { X = -30, Z = 0, Heading = Math.PI / 2, SpeedMps = 5 };
            for (double t = 0; t < 12; t += Drive.Dt) { rule.OnFrame(new DriverFrame(t, s, 0), events); s.X += 5 * Drive.Dt; }
            Assert.That(events, Is.Empty);
        }
    }

    public class StopSignRuleTests
    {
        // Car drives north along x = 0 towards a STOP line at z = 0; the front is 2.15 m ahead of the centre.
        private static List<RuleEvent> Run(Func<double, double> speedAt, double endZ = 8)
        {
            var rule = new StopSignRule(new List<LaneLine> { LaneLine.Across(new Vec2(0, 0), 0) });
            var events = new List<RuleEvent>();
            var s = Drive.At(-30, 6);
            for (double t = 0; t < 40 && s.Z < endZ; t += Drive.Dt)
            {
                s.SpeedMps = speedAt(CarBody.Front(s).Z);
                rule.OnFrame(new DriverFrame(t, s, 0), events);
                s = Drive.Advance(s);
            }
            return events;
        }

        [Test]
        public void Pass_StopsBehindTheLineThenGoes()
        {
            int ticks = 0;
            var e = Run(z => z < -1.5 ? 4 : (ticks++ < 100 ? 0 : 3));
            Assert.That(Drive.Violations(e), Is.Empty);
            Assert.That(e.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        }

        [Test]
        public void Serious_RollsThroughAtFiveMph()
        {
            var e = Run(z => 2.2);
            Assert.That(e.Single().RuleId, Is.EqualTo(StopSignRule.RuleId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void Serious_StopsOnlyAfterTheFrontIsPastTheLine()
        {
            var e = Run(z => z < 1 ? 3 : 0, endZ: 3);
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void Pass_OtherLanesLineIsIgnored()
        {
            var rule = new StopSignRule(new List<LaneLine> { LaneLine.Across(new Vec2(20, 0), 0) });
            var events = new List<RuleEvent>();
            var s = Drive.At(-30, 8);
            for (double t = 0; t < 8; t += Drive.Dt) { rule.OnFrame(new DriverFrame(t, s, 0), events); s = Drive.Advance(s); }
            Assert.That(events, Is.Empty);
        }
    }

    public class GiveWayRuleTests
    {
        private static List<RuleEvent> Run(double otherAccel, bool brakingForPlayer = true)
        {
            var rule = new GiveWayRule(new List<GiveWayRule.Line> { new GiveWayRule.Line(LaneLine.Across(new Vec2(0, 0), 0), new Vec2(0, 6)) });
            var events = new List<RuleEvent>();
            var s = Drive.At(-5, 5);
            for (double t = 0; t < 12; t += Drive.Dt)
            {
                bool afterLine = CarBody.Front(s).Z > 0 && t < 4;
                var users = new List<RoadUser> { new RoadUser(7, new Vec2(-25, 4), Math.PI / 2, 8, false, 0, afterLine ? otherAccel : 0, afterLine && brakingForPlayer) };
                rule.OnFrame(new DriverFrame(t, s, 0), users, events);
                s = Drive.Advance(s);
            }
            return events;
        }

        [Test]
        public void Pass_NobodyHadToSlowDown() => Assert.That(Run(-0.5), Is.Empty);

        [Test]
        public void Pass_BrakingForSomethingElseDoesNotCount() => Assert.That(Run(-5, brakingForPlayer: false), Is.Empty);

        [Test]
        public void Minor_SomeoneHadToSlowDown()
        {
            var e = Run(-2.0);
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
            Assert.That(e.Single().RuleId, Is.EqualTo(GiveWayRule.RuleId));
        }

        [Test]
        public void Serious_SomeoneHadToBrakeHard() => Assert.That(Run(-4.0).Single().Severity, Is.EqualTo(Severity.Serious));

        [Test]
        public void Dangerous_EmergencyStop() => Assert.That(Run(-7.0).Max(x => x.Severity), Is.EqualTo(Severity.Dangerous));
    }

    public class PedestrianCrossingRuleTests
    {
        private static List<RuleEvent> Run(CrossingKind kind, PedState? state, Vec2 pedAt, double speed, bool stopBefore = false)
        {
            var area = new CrossingArea { Id = 0, Kind = kind, Polygon = Geo2.Rect(new Vec2(0, 10), 0, 1.5, 3.6) };
            var rule = new PedestrianCrossingRule(new List<CrossingArea> { area });
            var people = new List<Pedestrian>();
            if (state.HasValue) people.Add(new Pedestrian { Id = 1, State = state.Value, Crossing = 0, Position = pedAt });
            var events = new List<RuleEvent>();
            var s = Drive.At(-20, speed);
            for (double t = 0; t < 6; t += Drive.Dt)
            {
                rule.OnFrame(new DriverFrame(t, s, 0), people, events);
                if (stopBefore && CarBody.Front(s).Z > 6.5) s.SpeedMps = 0;
                s = Drive.Advance(s);
            }
            return events;
        }

        [Test]
        public void Pass_EmptyCrossing() => Assert.That(Run(CrossingKind.Zebra, null, default, 12), Is.Empty);

        [Test]
        public void Good_StoppedForSomeoneWaiting()
        {
            var e = Run(CrossingKind.Zebra, PedState.Waiting, new Vec2(4.5, 10), 8, stopBefore: true);
            Assert.That(Drive.Violations(e), Is.Empty);
            Assert.That(e.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        }

        [Test]
        public void Minor_DroveThroughWhileSomeoneWaited()
        {
            var e = Drive.Violations(Run(CrossingKind.Zebra, PedState.Waiting, new Vec2(4.5, 10), 10));
            Assert.That(e.Single().RuleId, Is.EqualTo(PedestrianCrossingRule.ZebraId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Driving));
        }

        [Test]
        public void Serious_DroveOntoTheZebraWhileSomeoneWasOnIt()
        {
            var e = Drive.Violations(Run(CrossingKind.Zebra, PedState.Crossing, new Vec2(3.0, 10), 8));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void Dangerous_PassedRightNextToSomeoneCrossing()
        {
            var e = Drive.Violations(Run(CrossingKind.Zebra, PedState.Crossing, new Vec2(1.5, 10), 9));
            Assert.That(e.Max(x => x.Severity), Is.EqualTo(Severity.Dangerous));
        }

        [Test]
        public void SideRoadUsesRuleH2()
        {
            var e = Drive.Violations(Run(CrossingKind.JunctionMouth, PedState.Crossing, new Vec2(3.0, 10), 6));
            Assert.That(e.Single().RuleId, Is.EqualTo(PedestrianCrossingRule.H2Id));
            Assert.That(e.Single().Source, Does.Contain("H2"));
        }
    }

    public class ZigZagOvertakeRuleTests
    {
        private static List<RuleEvent> Run(Vec2 zoneCentre)
        {
            var rule = new ZigZagOvertakeRule(new List<Vec2[]> { Geo2.Rect(zoneCentre, 0, 7.5, 3.5) });
            var events = new List<RuleEvent>();
            var me = Drive.At(0, 12, x: 1.75);
            var other = new Vec2(-1.75, 10);
            for (double t = 0; t < 4; t += Drive.Dt)
            {
                rule.OnFrame(new DriverFrame(t, me, 1), new List<RoadUser> { new RoadUser(3, other, 0, 5, false) }, events);
                me = Drive.Advance(me);
                other = other + new Vec2(0, 5 * Drive.Dt);
            }
            return events;
        }

        [Test]
        public void Serious_OvertookOnTheZigZags() => Assert.That(Run(new Vec2(0, 20)).Single().Severity, Is.EqualTo(Severity.Serious));

        [Test]
        public void Pass_OvertookAwayFromTheCrossing() => Assert.That(Run(new Vec2(0, 300)), Is.Empty);
    }

    public class BoxJunctionRuleTests
    {
        private static List<RuleEvent> Run(int indicator, bool stop)
        {
            var rule = new BoxJunctionRule(new List<Vec2[]> { Geo2.Rect(new Vec2(0, 0), 0, 7, 7) }, DrivingSide.Left);
            var events = new List<RuleEvent>();
            var s = Drive.At(-20, 6);
            for (double t = 0; t < 10; t += Drive.Dt)
            {
                rule.OnFrame(new DriverFrame(t, s, indicator), events);
                if (stop && s.Z > 0 && t < 7) s.SpeedMps = 0;
                else if (stop && t >= 7) s.SpeedMps = 6;
                s = Drive.Advance(s);
            }
            return events;
        }

        [Test]
        public void Pass_DroveStraightThrough() => Assert.That(Run(0, stop: false), Is.Empty);

        [Test]
        public void Serious_StoppedInTheBox()
        {
            var e = Run(0, stop: true);
            Assert.That(e.Single().RuleId, Is.EqualTo(BoxJunctionRule.RuleId));
            Assert.That(e.Single().Severity, Is.EqualTo(Severity.Serious));
        }

        [Test]
        public void Pass_WaitingToTurnRightForOncomingTraffic() => Assert.That(Run(+1, stop: true), Is.Empty);
    }

    public class TownRulesIntegrationTests
    {
        [Test]
        public void RunningTheRedAtTheTownCrossroadsIsCaught()
        {
            var town = new TownGeometry();
            var rules = new TownRules(town.Graph);
            var (pos, heading) = town.AnchorPose("crossroads_signals");
            var s = new CarState { X = pos.X, Z = pos.Z, Heading = heading, SpeedMps = 12 };
            var events = new List<RuleEvent>();
            var lights = new List<LightState> { LightState.Red, LightState.Green };
            for (double t = 0; t < 8; t += Drive.Dt)
            {
                rules.OnFrame(new DriverFrame(t, s, 0), lights, new List<Pedestrian>(), new List<RoadUser>(), events);
                s = Drive.Advance(s);
            }
            Assert.That(Drive.Violations(events).Select(e => e.RuleId), Is.EquivalentTo(new[] { TrafficLightRule.AslId, TrafficLightRule.RedId }));
        }

        [Test]
        public void EmergingFromMillLaneIsJudgedAtTheGiveWayLine()
        {
            var town = new TownGeometry();
            var rules = new TownRules(town.Graph);
            var (pos, heading) = town.AnchorPose("tjunction_north");
            var s = new CarState { X = pos.X, Z = pos.Z, Heading = heading, SpeedMps = 6 };
            var events = new List<RuleEvent>();
            var lights = new List<LightState> { LightState.Green, LightState.Red };
            for (double t = 0; t < 12; t += Drive.Dt)
            {
                bool pastLine = s.Z > -6.5 + 0.1 - 2.15;
                var users = new List<RoadUser> { new RoadUser(1, new Vec2(-140, 1.75), Math.PI / 2, 12, false, 0, pastLine ? -4.5 : 0, pastLine) };
                rules.OnFrame(new DriverFrame(t, s, 1), lights, new List<Pedestrian>(), users, events);
                s = Drive.Advance(s);
            }
            Assert.That(Drive.Violations(events).Single().RuleId, Is.EqualTo(GiveWayRule.RuleId));
        }
    }
}
