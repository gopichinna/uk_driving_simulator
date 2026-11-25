using LToLocal.Rules;
using LToLocal.Sim;

namespace Rules.Core.Tests;

/// <summary>Fixtures for GB.OBS.LANE_CHANGE / GB.SIG.TIMELY (Highway Code rules 133 and 103).</summary>
public class LaneChangeRuleTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private const double Lane1 = -1.825, Lane2 = 1.825;

    private static TrafficSim NewTraffic() => new TrafficSim(new[]
    {
        new StraightLane(Lane1, +1, isNearside: true),
        new StraightLane(Lane2, +1, isNearside: false),
        new StraightLane(8.475, -1, isNearside: false),
        new StraightLane(12.125, -1, isNearside: true),
    }, new TrafficParams(), seed: 1);

    /// <summary>
    /// Drives straight in lane 1 for <paramref name="leadIn"/> seconds, then moves to lane 2 over 2 s,
    /// then carries on for 4 s. Observations and indicator are scripted by time.
    /// </summary>
    private static List<RuleEvent> Drive(
        TrafficSim traffic,
        Func<double, int> indicatorAt,
        (ObservationKind kind, double time)[] observations,
        double leadIn = 4.0,
        double speed = 15.0)
    {
        var rule = new LaneChangeRule(traffic);
        var events = new List<RuleEvent>();
        var ego = new CarState { X = Lane1, Z = 0, SpeedMps = speed };
        var pendingObs = new List<(ObservationKind kind, double time)>(observations);

        double total = leadIn + 2.0 + 4.0;
        for (double t = 0; t < total; t += Dt)
        {
            double move = Math.Clamp((t - leadIn) / 2.0, 0, 1);
            ego.X = Lane1 + (Lane2 - Lane1) * move;
            ego.Z += speed * Dt;

            foreach (var o in pendingObs.Where(o => o.time <= t).ToList())
            {
                rule.Observe(new ObservationEvent(o.kind, o.time));
                pendingObs.Remove(o);
            }

            traffic.Step(Dt, ego, respawn: false);
            rule.OnFrame(new DriverFrame(t, ego, indicatorAt(t)), events);
        }
        return events;
    }


    [Test]
    public void FullRoutineIsGoodPractice()
    {
        var traffic = NewTraffic();
        traffic.AddCar(1, -70, speed: 15, desired: 15); // someone around, but far enough back

        var events = Drive(traffic,
            t => t >= 1.5 && t < 7 ? 1 : 0,
            new[] { (ObservationKind.MirrorOffside, 1.0), (ObservationKind.ShoulderOffside, 3.8) });

        Assert.That(events, Has.Count.EqualTo(1), string.Join("\n", events));
        Assert.That(events[0].Outcome, Is.EqualTo(RuleOutcome.GoodPractice));
    }

    [Test]
    public void NoMirrorNoSignalNoShoulderGivesThreeDrivingFaults()
    {
        var traffic = NewTraffic();
        traffic.AddCar(0, 40, speed: 15, desired: 15); // a car nearby, so a signal is needed

        var events = Drive(traffic, _ => 0, Array.Empty<(ObservationKind, double)>());

        Assert.That(events.Count(e => e.Severity == Severity.Driving), Is.EqualTo(3), string.Join("\n", events));
        Assert.That(events.Any(e => e.Message.Contains("mirror")));
        Assert.That(events.Any(e => e.RuleId == LaneChangeRule.RuleIdSignal));
        Assert.That(events.Any(e => e.Message.Contains("shoulder")));
        Assert.That(events.All(e => e.Outcome == RuleOutcome.Violation));
    }

    [Test]
    public void SignalNotNeededOnAnEmptyRoad()
    {
        var traffic = NewTraffic(); // nobody around

        var events = Drive(traffic, _ => 0,
            new[] { (ObservationKind.MirrorInterior, 1.0), (ObservationKind.ShoulderOffside, 3.5) });

        Assert.That(events.Any(e => e.RuleId == LaneChangeRule.RuleIdSignal), Is.False, string.Join("\n", events));
    }

    [Test]
    public void LateSignalIsADrivingFault()
    {
        var traffic = NewTraffic();
        traffic.AddCar(0, 40, speed: 15, desired: 15);

        // The car crosses the line ~1 s after starting to move (t ≈ 5.0); signal comes on only at 4.5.
        var events = Drive(traffic, t => t >= 4.5 ? 1 : 0,
            new[] { (ObservationKind.MirrorOffside, 1.0), (ObservationKind.ShoulderOffside, 3.8) });

        var signal = events.Single(e => e.RuleId == LaneChangeRule.RuleIdSignal);
        Assert.That(signal.Message, Does.Contain("earlier"));
    }

    [Test]
    public void WrongWaySignalIsAFault()
    {
        var traffic = NewTraffic();
        var events = Drive(traffic, t => t >= 1.0 ? -1 : 0,
            new[] { (ObservationKind.MirrorOffside, 1.0), (ObservationKind.ShoulderOffside, 3.8) });

        Assert.That(events.Any(e => e.Message.Contains("wrong way")), string.Join("\n", events));
    }

    [Test]
    public void OldMirrorCheckDoesNotCount()
    {
        var traffic = NewTraffic();
        // Mirror checked at t=0.1; with an 8 s lead-in the crossing is at ≈9 s, outside the 6 s window.
        var events = Drive(traffic, t => t >= 6 ? 1 : 0,
            new[] { (ObservationKind.MirrorOffside, 0.1), (ObservationKind.ShoulderOffside, 8.5) }, leadIn: 8.0);

        Assert.That(events.Any(e => e.Message.Contains("mirror")), string.Join("\n", events));
    }

    [Test]
    public void CuttingInFrontOfACarIsSeriousOrDangerous()
    {
        var traffic = NewTraffic();
        // A much faster car in lane 2 (30 m/s vs our 15): when we move over at t≈5 it is ~15 m behind
        // and closing fast, so it has to brake hard.
        traffic.AddCar(1, -90, speed: 30, desired: 30);

        var events = Drive(traffic, t => t >= 1.5 ? 1 : 0,
            new[] { (ObservationKind.MirrorOffside, 1.0), (ObservationKind.ShoulderOffside, 3.8) });

        Assert.That(events.Any(e => e.Severity >= Severity.Serious), string.Join("\n", events));
        Assert.That(events.Any(e => e.Outcome == RuleOutcome.GoodPractice), Is.False);
    }

    [Test]
    public void ShoulderCheckOnTheWrongSideDoesNotCount()
    {
        var traffic = NewTraffic();
        var events = Drive(traffic, t => t >= 1.5 ? 1 : 0,
            new[] { (ObservationKind.MirrorOffside, 1.0), (ObservationKind.ShoulderNearside, 3.8) });

        Assert.That(events.Any(e => e.Message.Contains("right shoulder")), string.Join("\n", events));
    }

    [Test]
    public void SideMappingDependsOnDrivingSide()
    {
        Assert.That(Sides.FromDirection(+1, DrivingSide.Left), Is.EqualTo(CarSide.Offside));   // UK: right = offside
        Assert.That(Sides.FromDirection(-1, DrivingSide.Left), Is.EqualTo(CarSide.Nearside));
        Assert.That(Sides.FromDirection(+1, DrivingSide.Right), Is.EqualTo(CarSide.Nearside)); // US: right = nearside
    }
}
