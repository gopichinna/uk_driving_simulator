using LToLocal.Sim;

namespace Sim.Core.Tests;

public class RoundaboutTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private const int N = 0, E = 1, S = 2, W = 3;

    private static RoundaboutGeometry Geo() => new RoundaboutGeometry(new Vec2(0, 0));
    private static CarState FarPlayer => new CarState { X = 500, Z = 500 };

    [Test]
    public void EntriesEndAndExitsStartOnTheRing()
    {
        var g = Geo();
        for (int k = 0; k < g.ArmCount; k++)
        {
            var (entryEnd, _) = g.Entry[k].Sample(g.Entry[k].Length);
            var (exitStart, _) = g.Exit[k].Sample(0);
            Assert.That((entryEnd - g.Centre).Length, Is.EqualTo(g.RingRadius).Within(0.05));
            Assert.That((exitStart - g.Centre).Length, Is.EqualTo(g.RingRadius).Within(0.05));
        }
    }

    [Test]
    public void UkCarsApproachOnTheLeftAndCirculateClockwise()
    {
        var g = Geo();
        // Coming north up the south arm, you are on the west (left) half of the road.
        var (start, heading) = g.Entry[S].Sample(0);
        Assert.That(start.X, Is.LessThan(0));
        Assert.That(Math.Cos(heading), Is.GreaterThan(0.99), "heading north");

        // At the top of the ring the clockwise direction is east.
        var (_, ringHeading) = g.Ring.Sample(0.5);
        Assert.That(Math.Sin(ringHeading), Is.GreaterThan(0.99));
    }

    [Test]
    public void ExitNumbersCountClockwise()
    {
        var g = Geo();
        Assert.That(g.ExitNumber(S, W), Is.EqualTo(1), "from the south, west is the first exit (left)");
        Assert.That(g.ExitNumber(S, N), Is.EqualTo(2), "straight on");
        Assert.That(g.ExitNumber(S, E), Is.EqualTo(3), "right");
        Assert.That(g.ExitNumber(S, S), Is.EqualTo(4), "full circle");
    }

    [Test]
    public void SingleCarCompletesItsRouteAndSignalsCorrectly()
    {
        var g = Geo();
        var t = new RoundaboutTraffic(g, new RoundaboutTrafficParams(), seed: 1);
        var car = t.AddCar(S, E, RoundaboutSegment.Entry, 0, 12); // right turn = 3rd exit

        bool rightOnApproach = false, leftBeforeExit = false, finished = false;
        for (int i = 0; i < 50 * 60 && !finished; i++)
        {
            t.Step(Dt, FarPlayer, spawn: false);
            if (car.Segment == RoundaboutSegment.Entry && car.Indicator == 1) rightOnApproach = true;
            if (car.Segment == RoundaboutSegment.Ring && car.Indicator == -1) leftBeforeExit = true;
            finished = !t.Cars.Contains(car);
        }

        Assert.That(finished, "car should leave by the east arm");
        Assert.That(rightOnApproach, "signal right on the approach for a right turn (rule 186)");
        Assert.That(leftBeforeExit, "signal left after passing the exit before (rule 186)");
    }

    [Test]
    public void EnteringCarGivesWayToTrafficFromTheRight()
    {
        var g = Geo();
        var t = new RoundaboutTraffic(g, new RoundaboutTrafficParams(), seed: 1);
        // Car about to reach the south give-way line…
        var entering = t.AddCar(S, N, RoundaboutSegment.Entry, g.GiveWayS[S] - 8, 4);
        // …and a car on the ring coming from the right (east side), heading for the west exit,
        // so it passes the south entry: inside the critical gap, so the entering car must wait.
        double upstream = g.Ring.Wrap(g.EntryMergeS[S] - 20); // ~2.9 s away at 7 m/s, still after its own east merge point
        var circulating = t.AddCar(E, W, RoundaboutSegment.Ring, upstream, 7);

        bool waited = false;
        for (int i = 0; i < 50 * 10; i++)
        {
            t.Step(Dt, FarPlayer, spawn: false);
            if (entering.Segment == RoundaboutSegment.Entry && entering.S > g.GiveWayS[S] - 6 && entering.SpeedMps < 1.0) waited = true;
            if (entering.Segment != RoundaboutSegment.Entry)
            {
                Assert.That(circulating.Segment != RoundaboutSegment.Ring
                            || g.RingForward(g.EntryMergeS[S], circulating.S) > 6,
                    "must not enter before the circulating car has passed");
            }
        }
        Assert.That(waited, "should stop at the give-way line");
        Assert.That(entering.Segment, Is.Not.EqualTo(RoundaboutSegment.Entry), "should enter once the gap is clear");
    }

    [TestCase(42UL)]
    [TestCase(7UL)]
    [TestCase(2026UL)]
    public void TenMinutesOfTrafficWithoutCollisionsOrDeadlock(ulong seed)
    {
        var g = Geo();
        var p = new RoundaboutTrafficParams { TargetCars = 12 };
        var t = new RoundaboutTraffic(g, p, seed);
        var completed = new HashSet<int>();
        var seen = new HashSet<int>();

        for (int i = 0; i < 50 * 600; i++)
        {
            foreach (var c in t.Cars) seen.Add(c.Id);
            var before = t.Cars.Select(c => c.Id).ToHashSet();
            t.Step(Dt, FarPlayer);
            foreach (int id in before) if (t.Cars.All(c => c.Id != id)) completed.Add(id);

            var ring = t.Cars.Where(c => c.Segment == RoundaboutSegment.Ring).ToList();
            foreach (var a in ring)
                foreach (var b in ring)
                    if (a != b)
                    {
                        double gap = g.RingForward(a.S, b.S) - b.LengthM; // front-to-front minus the leader's length
                        Assert.That(gap, Is.GreaterThan(-0.5).Or.GreaterThan(g.Ring.Length - 2 * p.CarLengthM), $"overlap on the ring at tick {i}");
                    }
        }

        Assert.That(completed.Count, Is.GreaterThan(150), $"throughput too low (deadlock?) – completed {completed.Count} of {seen.Count}");
    }

    [Test]
    public void CirculatingCarStopsBehindAStoppedPlayerOnTheRing()
    {
        var g = Geo();
        var t = new RoundaboutTraffic(g, new RoundaboutTrafficParams(), seed: 1);
        var car = t.AddCar(N, W, RoundaboutSegment.Ring, g.Ring.Wrap(g.EntryMergeS[N] + 2), 7); // going the long way round
        // Player stopped on the ring 25 m ahead of the car
        var (pos, heading) = g.Ring.Sample(car.S + 25);
        var player = new CarState { X = pos.X, Z = pos.Z, Heading = heading, SpeedMps = 0 };

        for (int i = 0; i < 50 * 10; i++) t.Step(Dt, player, spawn: false);

        double gap = g.RingForward(car.S, g.Ring.Project(pos).s);
        Assert.That(car.SpeedMps, Is.LessThan(0.5));
        Assert.That(gap, Is.GreaterThan(2.0).And.LessThan(15));
    }

    [Test]
    public void SameSeedSameTraffic()
    {
        var g = Geo();
        var a = new RoundaboutTraffic(g, new RoundaboutTrafficParams(), 9);
        var b = new RoundaboutTraffic(g, new RoundaboutTrafficParams(), 9);
        for (int i = 0; i < 50 * 60; i++) { a.Step(Dt, FarPlayer); b.Step(Dt, FarPlayer); }
        Assert.That(a.Cars.Select(c => (c.Position.X, c.Position.Z)), Is.EqualTo(b.Cars.Select(c => (c.Position.X, c.Position.Z))));
    }
}
