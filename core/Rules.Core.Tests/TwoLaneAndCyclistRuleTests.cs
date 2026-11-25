using LToLocal.Rules;
using LToLocal.Sim;
using G = LToLocal.Sim.TwoLaneRoundaboutGeometry;

namespace Rules.Core.Tests;

public class TwoLaneRoundaboutRuleTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private const int N = 0, E = 1, S = 2, W = 3;

    /// <summary>
    /// Scripted drive: approach lane → ring (in a ring lane, optionally moving to the outer lane at a given
    /// fraction of the way round) → exit. Indicator scripted by section and progress.
    /// </summary>
    private static List<RuleEvent> Drive(int from, int approachLane, int to, int ringLaneStart, double moveOutAt,
        Func<string, double, int> indicator, double speed = 7.0)
    {
        var g = new G(new Vec2(0, 0));
        var traffic = new TwoLaneRoundaboutTraffic(g, new TwoLaneTrafficParams(), 3);
        var rule = new RoundaboutRule(new TwoLaneRoundaboutView(traffic));
        var events = new List<RuleEvent>();

        var entry = g.Entry[from, approachLane];
        double startB = g.MergeBearing[from, approachLane];
        double sweep = G.Cw(startB, g.DivergeBearing[to]);
        if (sweep < 0.05) sweep += 2 * Math.PI;
        double t = 0;

        void Frame(Vec2 pos, double heading, string section, double progress)
        {
            var ego = new CarState { X = pos.X, Z = pos.Z, Heading = heading, SpeedMps = speed };
            traffic.Step(Dt, ego, spawn: false);
            rule.OnFrame(new DriverFrame(t, ego, indicator(section, progress)), Dt, events);
            t += Dt;
        }

        for (double s = entry.Length - 80; s < entry.Length; s += speed * Dt)
        {
            var (p, h) = entry.Sample(s);
            Frame(p, h, "approach", s);
        }

        double theta = 0;
        while (theta < sweep)
        {
            double frac = theta / sweep;
            double r = G.LaneRadius(ringLaneStart);
            if (ringLaneStart == 1 && frac >= moveOutAt)
                r = Math.Min(G.OuterLaneR, G.InnerLaneR + (frac - moveOutAt) / 0.15 * (G.OuterLaneR - G.InnerLaneR));
            double b = startB + theta;
            Frame(g.RingPoint(b, r), b + Math.PI / 2, "ring", frac);
            theta += speed * Dt / r;
        }

        for (double s = 0; s < 40; s += speed * Dt)
        {
            var (p, h) = g.Exit[to].Sample(s);
            Frame(p, h, "exit", s);
        }
        return events;
    }

    [Test]
    public void RightTurnFromTheRightLaneMovingOutBeforeTheExitIsGood()
    {
        var events = Drive(S, 1, E, ringLaneStart: 1, moveOutAt: 0.75,
            (section, p) => section == "approach" ? 1 : section == "ring" ? (p < 0.7 ? 1 : -1) : 0);
        Assert.That(events, Has.Count.EqualTo(1), string.Join("\n", events));
        Assert.That(events[0].Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        Assert.That(events[0].Message, Does.Contain("right lane"));
    }

    [Test]
    public void RightTurnFromTheLeftLaneIsALaneChoiceFault()
    {
        var events = Drive(S, 0, E, ringLaneStart: 0, moveOutAt: 1,
            (section, p) => section == "approach" ? 1 : section == "ring" ? (p < 0.7 ? 1 : -1) : 0);
        var fault = events.Single(e => e.RuleId == RoundaboutRule.RuleApproachLane);
        Assert.That(fault.Message, Does.Contain("right lane"));
    }

    [Test]
    public void StraightOnFromTheRightLaneIsALaneChoiceFault()
    {
        var events = Drive(S, 1, N, ringLaneStart: 1, moveOutAt: 0.5,
            (section, p) => section == "ring" && p > 0.55 ? -1 : 0);
        Assert.That(events.Any(e => e.RuleId == RoundaboutRule.RuleApproachLane && e.Message.Contains("left lane")),
            string.Join("\n", events));
    }

    [Test]
    public void LeavingFromTheInnerLaneIsAFault()
    {
        var events = Drive(S, 1, E, ringLaneStart: 1, moveOutAt: 2, // never moves out
            (section, p) => section == "approach" ? 1 : section == "ring" ? (p < 0.7 ? 1 : -1) : 0);
        Assert.That(events.Any(e => e.RuleId == RoundaboutRule.RuleStayInLane), string.Join("\n", events));
    }

    [Test]
    public void LeftTurnFromTheLeftLaneIsGood()
    {
        var events = Drive(S, 0, W, ringLaneStart: 0, moveOutAt: 1, (section, _) => section != "exit" ? -1 : 0);
        Assert.That(events.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice), string.Join("\n", events));
    }
}

public class CyclistPassingRuleTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;

    /// <summary>Cyclist riding north at 5 m/s at x = 0; we overtake at 13 m/s with a given lateral gap (centre to centre).</summary>
    private static List<RuleEvent> Overtake(double centreOffset, double speed = 13.0)
    {
        var rule = new CyclistPassingRule();
        var events = new List<RuleEvent>();
        double bikeZ = 30, carZ = 0;
        for (double t = 0; t < 12; t += Dt)
        {
            bikeZ += 5 * Dt;
            carZ += speed * Dt;
            var users = new[] { new RoadUser(1, new Vec2(0, bikeZ), 0, 5, isCyclist: true) };
            var ego = new CarState { X = centreOffset, Z = carZ, SpeedMps = speed };
            rule.OnFrame(new DriverFrame(t, ego, 0), users, events);
        }
        return events;
    }

    [Test]
    public void PlentyOfRoomIsGood()
    {
        var e = Overtake(3.0).Single(); // 3.0 − 0.9 − 0.3 = 1.8 m clearance at 29 mph
        Assert.That(e.Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
    }

    [Test]
    public void UnderOnePointFiveMetresIsSerious()
    {
        var e = Overtake(2.4).Single(); // 1.2 m
        Assert.That(e.Severity, Is.EqualTo(Severity.Serious));
        Assert.That(e.Message, Does.Contain("1.5 m"));
    }

    [Test]
    public void UnderOneMetreIsDangerous()
    {
        Assert.That(Overtake(1.8).Single().Severity, Is.EqualTo(Severity.Dangerous)); // 0.6 m
    }

    [Test]
    public void AboveThirtyMphNeedsMoreThanOnePointFive()
    {
        var e = Overtake(2.9, speed: 17).Single(); // 1.7 m at 38 mph: not enough (needs ~2 m)
        Assert.That(e.Severity, Is.EqualTo(Severity.Serious), e.ToString());
    }

    [Test]
    public void OncomingCyclistIsNotAnOvertake()
    {
        var rule = new CyclistPassingRule();
        var events = new List<RuleEvent>();
        for (double t = 0; t < 8; t += Dt)
        {
            var users = new[] { new RoadUser(1, new Vec2(2, 60 - 5 * t), Math.PI, 5, isCyclist: true) };
            rule.OnFrame(new DriverFrame(t, new CarState { X = 0, Z = 13 * t, SpeedMps = 13 }, 0), users, events);
        }
        Assert.That(events, Is.Empty);
    }
}
