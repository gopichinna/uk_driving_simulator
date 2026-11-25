using LToLocal.Sim;
using G = LToLocal.Sim.TwoLaneRoundaboutGeometry;

namespace Sim.Core.Tests;

public class TwoLaneRoundaboutTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private const int N = 0, E = 1, S = 2, W = 3;
    private static readonly CarState FarPlayer = new CarState { X = 900, Z = 900 };

    private static G Geo() => new G(new Vec2(0, 0));

    [Test]
    public void EntriesJoinAndExitsLeaveFromTheirOwnRingLane()
    {
        var g = Geo();
        for (int k = 0; k < g.ArmCount; k++)
        {
            var (end0, _) = g.Entry[k, 0].Sample(g.Entry[k, 0].Length);
            var (end1, _) = g.Entry[k, 1].Sample(g.Entry[k, 1].Length);
            Assert.That(g.RadiusOf(end0), Is.EqualTo(G.OuterLaneR).Within(0.05), "left lane joins the outer lane");
            Assert.That(g.RadiusOf(end1), Is.EqualTo(G.InnerLaneR).Within(0.05), "right lane joins the inner lane");
            var (exit0, _) = g.Exit[k, 0].Sample(0);
            var (exit1, _) = g.Exit[k, 1].Sample(0);
            Assert.That(g.RadiusOf(exit0), Is.EqualTo(G.OuterLaneR).Within(0.05), "left exit lane leaves from the outer lane");
            Assert.That(g.RadiusOf(exit1), Is.EqualTo(G.InnerLaneR).Within(0.05), "right exit lane leaves from the inner lane");
        }
    }

    [Test]
    public void LeftApproachLaneIsNearside()
    {
        var g = Geo();
        // Coming north up the south arm: lane 0 (left) is further west than lane 1.
        var (l0, _) = g.Entry[S, 0].Sample(0);
        var (l1, _) = g.Entry[S, 1].Sample(0);
        Assert.That(l0.X, Is.LessThan(l1.X));
        Assert.That(l1.X, Is.LessThan(0), "both approach lanes are on the left of the road");
    }

    [Test]
    public void LaneArrowsMatchExits()
    {
        Assert.That(G.ApproachLaneFor(1), Is.EqualTo(0), "left: left lane");
        Assert.That(G.ApproachLaneFor(2), Is.EqualTo(0), "straight on: left lane");
        Assert.That(G.ApproachLaneFor(3), Is.EqualTo(1), "right: right lane");
    }

    [Test]
    public void RightTurnerStaysInTheInnerLaneAndLeavesByTheRightExitLane()
    {
        var g = Geo();
        var t = new TwoLaneRoundaboutTraffic(g, new TwoLaneTrafficParams(), 1);
        var car = t.AddOnEntry(S, 1, E, g.GiveWayS[S, 1] - 30, 10);

        bool rightOnApproach = false, leftOnRing = false, finished = false, leftInnerLane = false;
        for (int i = 0; i < 50 * 60 && !finished; i++)
        {
            t.Step(Dt, FarPlayer, spawn: false);
            if (car.Segment == TwoLaneSegment.Ring && car.RingLane != 1) leftInnerLane = true;
            if (car.Segment == TwoLaneSegment.Entry && car.Indicator == 1) rightOnApproach = true;
            if (car.Segment == TwoLaneSegment.Ring && car.Indicator == -1) leftOnRing = true;
            if (car.Segment == TwoLaneSegment.Exit) Assert.That(car.ExitLane, Is.EqualTo(1), "right exit lane");
            finished = !t.Cars.Contains(car);
        }

        Assert.That(finished, "should leave by the east arm");
        Assert.That(leftInnerLane, Is.False, "stays in the inner lane all the way round");
        Assert.That(rightOnApproach && leftOnRing, "right on approach, left before the exit");
    }

    [Test]
    public void InnerLaneCarWaitsForOuterLaneTrafficBeforeCrossingToItsExit()
    {
        var g = Geo();
        var t = new TwoLaneRoundaboutTraffic(g, new TwoLaneTrafficParams(), 1);
        // Inner-lane car about to leave by the west exit...
        var inner = t.AddOnRing(N, W, 1, g.DivergeBearing[W, 1] - 0.2, 7);
        // ...while an outer-lane car is right next to the crossing point, going past it (to the north exit).
        var outer = t.AddOnRing(E, N, 0, g.ExitCrossBearing[W] - 6.0 / G.OuterLaneR, 7);

        for (int i = 0; i < 25; i++) t.Step(Dt, FarPlayer, spawn: false);
        Assert.That(inner.Segment, Is.EqualTo(TwoLaneSegment.Ring), "must not cut across the outer-lane car");
    }

    [Test]
    public void EnteringCarGivesWayToCarComingFromTheRight()
    {
        var g = Geo();
        var t = new TwoLaneRoundaboutTraffic(g, new TwoLaneTrafficParams(), 1);
        var entering = t.AddOnEntry(S, 0, N, g.GiveWayS[S, 0] - 6, 3);
        // Outer-lane car ~2.5 s upstream of the south merge point, going to the west exit (passes our merge).
        double upstream = g.MergeBearing[S, 0] - 20.0 / G.OuterLaneR;
        var circulating = t.AddOnRing(E, W, 0, upstream, 8);

        bool waited = false;
        for (int i = 0; i < 50 * 10; i++)
        {
            t.Step(Dt, FarPlayer, spawn: false);
            if (entering.Segment == TwoLaneSegment.Entry && entering.S > g.GiveWayS[S, 0] - 4 && entering.SpeedMps < 1.0) waited = true;
        }
        Assert.That(waited, "should stop at the give-way line");
        Assert.That(entering.Segment, Is.Not.EqualTo(TwoLaneSegment.Entry), "should go once the gap is clear");
    }

    [TestCase(1UL)]
    [TestCase(42UL)]
    [TestCase(2026UL)]
    public void TenMinutesWithoutOverlapOrDeadlock(ulong seed)
    {
        var g = Geo();
        var p = new TwoLaneTrafficParams { TargetCars = 16 };
        var t = new TwoLaneRoundaboutTraffic(g, p, seed);
        int completed = 0;

        for (int i = 0; i < 50 * 600; i++)
        {
            int before = t.Cars.Count;
            var ids = t.Cars.Select(c => c.Id).ToList();
            t.Step(Dt, FarPlayer);
            completed += ids.Count(id => t.Cars.All(c => c.Id != id));

            var ring = t.Cars.Where(c => c.Segment == TwoLaneSegment.Ring).ToList();
            foreach (var a in ring)
                foreach (var b in ring)
                {
                    if (a == b || Math.Abs(a.Radius - b.Radius) > 2.0) continue;
                    double ahead = G.Cw(a.Bearing, b.Bearing) * a.Radius; // front of a to front of b
                    if (ahead > 2 * Math.PI * a.Radius - 10) continue;     // b is actually behind a
                    Assert.That(ahead - b.LengthM, Is.GreaterThan(-0.5), $"overlap on the ring at tick {i} (seed {seed})");
                }
        }

        Assert.That(completed, Is.GreaterThan(200), $"throughput too low (deadlock?): {completed}");
    }

    [Test]
    public void CyclistsStayInTheOuterLane()
    {
        var g = Geo();
        var t = new TwoLaneRoundaboutTraffic(g, new TwoLaneTrafficParams(), 1);
        var bike = t.AddOnEntry(S, 0, E, g.GiveWayS[S, 0] - 10, 5, cyclist: true); // going right, but stays left (rule 187)
        for (int i = 0; i < 50 * 40 && t.Cars.Contains(bike); i++)
        {
            t.Step(Dt, FarPlayer, spawn: false);
            if (bike.Segment == TwoLaneSegment.Ring) Assert.That(bike.RingLane, Is.EqualTo(0));
        }
        Assert.That(t.Cars.Contains(bike), Is.False, "cyclist should complete the route");
    }
}
