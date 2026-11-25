using LToLocal.Rules;
using LToLocal.Sim;

namespace Rules.Core.Tests;

public class FollowingDistanceRuleTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;

    /// <summary>Follow a car at the same speed with a fixed gap (bumper to bumper) for a number of seconds.</summary>
    private static List<RuleEvent> Follow(double gapM, double speedMps, double seconds = 8, double lateral = 0)
    {
        var rule = new FollowingDistanceRule();
        var events = new List<RuleEvent>();
        for (double t = 0; t < seconds; t += Dt)
        {
            double z = speedMps * t;
            var lead = new RoadUser(1, new Vec2(lateral, z + gapM + 4.3), 0, speedMps, isCyclist: false);
            rule.OnFrame(new DriverFrame(t, new CarState { Z = z, SpeedMps = speedMps }, 0), new[] { lead }, events);
        }
        return events;
    }

    [Test]
    public void ThreeSecondGapIsFine() => Assert.That(Follow(gapM: 13.4 * 3, speedMps: 13.4), Is.Empty);

    [Test]
    public void OnePointFiveSecondsIsAMinorFault()
    {
        var e = Follow(gapM: 13.4 * 1.5, speedMps: 13.4).Single();
        Assert.That(e.Severity, Is.EqualTo(Severity.Driving));
        Assert.That(e.Message, Does.Contain("two-second"));
    }

    [Test]
    public void UnderOneSecondIsSerious()
    {
        var events = Follow(gapM: 13.4 * 0.6, speedMps: 13.4);
        Assert.That(events.Any(e => e.Severity == Severity.Serious), string.Join("\n", events));
    }

    [Test]
    public void ShortBurstIsIgnored() => Assert.That(Follow(gapM: 13.4 * 1.5, speedMps: 13.4, seconds: 2), Is.Empty);

    [Test]
    public void SlowQueuesAreNotJudged() => Assert.That(Follow(gapM: 3, speedMps: 4), Is.Empty); // ~9 mph

    [Test]
    public void CarInTheNextLaneDoesNotCount() => Assert.That(Follow(gapM: 8, speedMps: 13.4, lateral: 3.6), Is.Empty);
}
