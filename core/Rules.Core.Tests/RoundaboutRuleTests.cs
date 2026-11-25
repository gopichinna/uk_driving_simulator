using LToLocal.Rules;
using LToLocal.Sim;

namespace Rules.Core.Tests;

/// <summary>Fixtures for the roundabout rules (Highway Code rules 185–186) and the speed rule (124).</summary>
public class RoundaboutRuleTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private const int N = 0, E = 1, S = 2, W = 3;

    /// <summary>
    /// Drives the player along entry[from] → ring → exit[to] at a steady speed, scripting the indicator
    /// by route section. Returns all rule events.
    /// </summary>
    private static List<RuleEvent> DriveThrough(int from, int to, Func<string, double, int> indicator,
        Action<RoundaboutTraffic, double>? onTick = null, double speed = 7.0, bool reverseOnRing = false)
    {
        var g = new RoundaboutGeometry(new Vec2(0, 0));
        var traffic = new RoundaboutTraffic(g, new RoundaboutTrafficParams(), seed: 3);
        var rule = new RoundaboutRule(traffic);
        var events = new List<RuleEvent>();

        // Build the route as (path, from s, to s, section name)
        var entry = g.Entry[from];
        double ringStart = g.EntryMergeS[from];
        double ringLen = g.RingForward(ringStart, g.ExitDivergeS[to]);
        if (ringLen < 1) ringLen += g.Ring.Length;

        var sections = new List<(Func<double, (Vec2, double)> sample, double length, string name)>
        {
            (s => entry.Sample(entry.Length - 80 + s), 80, "approach"),
            (s => reverseOnRing ? Reverse(g.Ring.Sample(ringStart - s)) : g.Ring.Sample(ringStart + s), ringLen, "ring"),
            (s => g.Exit[to].Sample(s), 40, "exit"),
        };

        double t = 0;
        foreach (var (sample, length, name) in sections)
        {
            for (double s = 0; s < length; s += speed * Dt, t += Dt)
            {
                var (pos, heading) = sample(s);
                var ego = new CarState { X = pos.X, Z = pos.Z, Heading = heading, SpeedMps = speed };
                onTick?.Invoke(traffic, t);
                traffic.Step(Dt, ego, spawn: false);
                double progress = name == "ring" ? s / length : s;
                rule.OnFrame(new DriverFrame(t, ego, indicator(name, progress)), Dt, events);
            }
        }
        return events;
    }

    private static (Vec2, double) Reverse((Vec2 pos, double heading) p) => (p.pos, p.heading + Math.PI);

    [Test]
    public void LeftTurnWithLeftSignalIsGood()
    {
        var events = DriveThrough(S, W, (section, _) => section != "exit" ? -1 : 0);
        Assert.That(events, Has.Count.EqualTo(1), string.Join("\n", events));
        Assert.That(events[0].Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
        Assert.That(events[0].Message, Does.Contain("first exit"));
    }

    [Test]
    public void RightTurnWithCorrectSignalsIsGood()
    {
        // Right on approach and round to past the north exit (2/3 of the way), then left.
        var events = DriveThrough(S, E, (section, p) =>
            section == "approach" ? 1 :
            section == "ring" ? (p < 0.72 ? 1 : -1) : 0);
        Assert.That(events.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice), string.Join("\n", events));
    }

    [Test]
    public void RightTurnWithNoSignalsGivesApproachAndExitFaults()
    {
        var events = DriveThrough(S, E, (_, _) => 0);
        Assert.That(events.Select(e => e.RuleId), Is.EquivalentTo(new[] { RoundaboutRule.RuleSignalApproach, RoundaboutRule.RuleSignalExit }),
            string.Join("\n", events));
        Assert.That(events.All(e => e.Severity == Severity.Driving));
    }

    [Test]
    public void StraightOnNeedsNoApproachSignalButLeftBeforeExit()
    {
        var good = DriveThrough(S, N, (section, p) => section == "ring" && p > 0.6 ? -1 : 0);
        Assert.That(good.Single().Outcome, Is.EqualTo(RuleOutcome.GoodPractice), string.Join("\n", good));

        var missingExitSignal = DriveThrough(S, N, (_, _) => 0);
        Assert.That(missingExitSignal.Single().RuleId, Is.EqualTo(RoundaboutRule.RuleSignalExit));
    }

    [Test]
    public void SignallingRightForStraightOnIsMisleading()
    {
        var events = DriveThrough(S, N, (section, p) => section == "approach" ? 1 : section == "ring" && p > 0.6 ? -1 : 0);
        Assert.That(events.Any(e => e.Message.Contains("straight on")), string.Join("\n", events));
    }

    [Test]
    public void PullingOutInFrontOfACirculatingCarIsAGiveWayFault()
    {
        bool added = false;
        var events = DriveThrough(S, W, (section, _) => section != "exit" ? -1 : 0, (traffic, t) =>
        {
            // We reach our merge point at t ≈ 80 m / 7 m/s ≈ 11.4 s. A car joins from the east arm at t = 9.0 s,
            // ~22 m (≈3.1 s) upstream of our merge point: it is coming from our right as we pull out.
            if (added || t < 9.0) return;
            added = true;
            var g = traffic.Geometry;
            traffic.AddCar(E, W, RoundaboutSegment.Ring, g.Ring.Wrap(g.EntryMergeS[E] + 1), 7);
        });

        var fault = events.FirstOrDefault(e => e.RuleId == RoundaboutRule.RuleGiveWay);
        Assert.That(fault, Is.Not.Null, string.Join("\n", events));
        Assert.That(fault!.Severity, Is.GreaterThanOrEqualTo(Severity.Serious));
        Assert.That(events.Any(e => e.Outcome == RuleOutcome.GoodPractice), Is.False);
    }

    [Test]
    public void GoingRoundAnticlockwiseIsDangerous()
    {
        var events = DriveThrough(S, W, (_, _) => 0, reverseOnRing: true);
        Assert.That(events.Any(e => e.RuleId == RoundaboutRule.RuleWrongWay && e.Severity == Severity.Dangerous),
            string.Join("\n", events));
    }

    // ---------- speed (rule 124) ----------

    [TestCase(32, Severity.Driving)]
    [TestCase(37, Severity.Serious)]
    [TestCase(46, Severity.Dangerous)]
    public void SpeedingSeverityGrowsWithExcess(double mph, Severity expected)
    {
        var rule = new SpeedRule();
        var events = new List<RuleEvent>();
        var ego = new CarState { SpeedMps = mph / 2.236936 };
        for (double t = 0; t < 3; t += Dt) rule.OnFrame(new DriverFrame(t, ego, 0), 30, events);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Severity, Is.EqualTo(expected));
        Assert.That(events[0].Message, Does.Contain("30 mph"));
    }

    [Test]
    public void BriefOverspeedIsIgnoredAndOneEventPerEpisode()
    {
        var rule = new SpeedRule();
        var events = new List<RuleEvent>();
        var fast = new CarState { SpeedMps = 33 / 2.236936 };
        var ok = new CarState { SpeedMps = 28 / 2.236936 };

        for (double t = 0; t < 1.0; t += Dt) rule.OnFrame(new DriverFrame(t, fast, 0), 30, events);  // too short
        Assert.That(events, Is.Empty);
        for (double t = 1.0; t < 1.5; t += Dt) rule.OnFrame(new DriverFrame(t, ok, 0), 30, events);
        for (double t = 1.5; t < 10; t += Dt) rule.OnFrame(new DriverFrame(t, fast, 0), 30, events); // one long episode
        Assert.That(events, Has.Count.EqualTo(1));
    }
}
