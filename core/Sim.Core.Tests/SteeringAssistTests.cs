using LToLocal.Sim;

namespace Sim.Core.Tests;

/// <summary>Aim assist: the player steers; the game keeps them in lane on turns and roundabouts, smoothly.</summary>
public class SteeringAssistTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private static readonly CarParams P = new();

    private static (LaneGraph g, int a) TJunction()
    {
        var g = new LaneGraph(leftHandTraffic: true);
        int node = g.AddNode("J", new Vec2(0, 104));
        int a = g.AddLane("A", new[] { new Vec2(0, 0), new Vec2(0, 100) }, 30);
        int left = g.AddLane("Left", new[] { new Vec2(-8, 108), new Vec2(-100, 108) }, 30);
        int ahead = g.AddLane("Ahead", new[] { new Vec2(0, 108), new Vec2(0, 200) }, 30);
        g.AddConnector(a, left, node);
        g.AddConnector(a, ahead, node);
        g.Finalise();
        return (g, a);
    }

    // Holds the same car state for a second so the smoothing settles, and returns the final controls.
    private static CarControls Settle(SteeringAssistor a, CarState car, double playerSteer, ISteeringGuide guide, AssistLevel level = AssistLevel.Full)
    {
        CarControls c = default;
        for (int i = 0; i < 100; i++) c = a.Apply(car, new CarControls(playerSteer, 1, 0), guide, P, level, Dt);
        return c;
    }

    [Test]
    public void HandsOffMeansNoHelpAtAll()
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var (pos, h) = geo.Entry[0, 0].Sample(geo.GiveWayS[0, 0]);
        var car = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };
        Assert.That(Settle(new SteeringAssistor(), car, 0, new TwoLaneRoundaboutGuide(geo)).Steer, Is.EqualTo(0));
    }

    [Test]
    public void OffMeansExactlyTheDriversOwnSteering()
    {
        var c = Settle(new SteeringAssistor(), new CarState { SpeedMps = 8 }, 0.3, new LaneGraphGuide(TJunction().g), AssistLevel.Off);
        Assert.That(c.Steer, Is.EqualTo(0.3));
    }

    [Test]
    public void NudgesAnApproximatelyRightLeftTurnTowardTheLine()
    {
        var (g, _) = TJunction();
        var guide = new LaneGraphGuide(g);
        var car = new CarState { X = 0, Z = 99, Heading = 0, SpeedMps = 4 };      // turning in at the junction
        var targets = new List<AimPoint>();
        guide.GetTargets(car, 6, targets);
        double leftCmd = targets.Select(t => SteeringAssist.CommandToward(car, t.Pos, P)).Min();   // the left-turn line
        Assert.That(leftCmd, Is.LessThan(-0.3));

        double playerSteer = leftCmd * 0.5;                                          // turns only half as much as needed
        var c = Settle(new SteeringAssistor(), car, playerSteer, guide);
        Assert.That(Math.Abs(c.Steer - leftCmd), Is.LessThan(Math.Abs(playerSteer - leftCmd)), "pulled toward the line");
        Assert.That(c.Steer, Is.LessThan(0), "still turning left");
    }

    [Test]
    public void NoHelpKeepingAStraightLane()
    {
        var (g, _) = TJunction();
        var car = new CarState { X = 0.8, Z = 30, Heading = 0.05, SpeedMps = 8 };  // mid-lane on a straight, slightly off line
        var c = Settle(new SteeringAssistor(), car, 0.3, new LaneGraphGuide(g));
        Assert.That(c.Steer, Is.EqualTo(0.3).Within(0.02), "lane keeping on a straight is not helped");
    }

    [Test]
    public void LeavesADecisiveSwerveAlone()
    {
        var (g, _) = TJunction();
        var car = new CarState { X = 0, Z = 99, Heading = 0, SpeedMps = 4 };
        var c = Settle(new SteeringAssistor(), car, 0.95, new LaneGraphGuide(g));       // hard right: matches no line
        Assert.That(c.Steer, Is.EqualTo(0.95).Within(0.02));
    }

    [Test]
    public void ChangingRingLaneIsNotPulledBack()
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var guide = new TwoLaneRoundaboutGuide(geo);
        var car = new CarState { X = 0, Z = 0, SpeedMps = 8 };
        var p0 = geo.RingPoint(1.2, TwoLaneRoundaboutGeometry.OuterLaneR);
        car.X = p0.X; car.Z = p0.Z; car.Heading = 1.2 + Math.PI / 2;                      // in the outer ring lane, travelling clockwise
        var targets = new List<AimPoint>();
        guide.GetTargets(car, 8, targets);
        var inner = targets.First(t => t.Neutral);                                       // the other ring lane
        double toInner = SteeringAssist.CommandToward(car, inner.Pos, P);
        var c = Settle(new SteeringAssistor(), car, toInner, guide);
        Assert.That(c.Steer, Is.EqualTo(toInner).Within(0.02), "a lane change on the ring is left to the driver");
    }

    // A driver who turns a fraction of what's needed (0.8 = a bit short, 2.2 = spins the wheel). Returns the worst distance off the
    // lane centre, and the biggest change in the game's correction in a single step (smoothness).
    private static double Run(AssistLevel level, double turnFraction, out double finalZ, out double biggestJump)
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var guide = new TwoLaneRoundaboutGuide(geo);
        var pts = new List<Vec2>(geo.Entry[0, 0].Points);
        double a0 = geo.MergeBearing[0, 0], a1 = geo.DivergeBearing[2, 0];
        for (double t = 0.05; t < TwoLaneRoundaboutGeometry.Cw(a0, a1); t += 0.05) pts.Add(geo.RingPoint(a0 + t, TwoLaneRoundaboutGeometry.OuterLaneR));
        pts.AddRange(geo.Exit[2, 0].Points);
        var ideal = new PathPolyline(pts);

        var assistor = new SteeringAssistor();
        var (pos, h) = geo.Entry[0, 0].Sample(25);
        var s = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };
        double worst = 0, last = 0; biggestJump = 0;
        for (int i = 0; i < (int)(70 / Dt); i++)
        {
            var (ps, _, _) = ideal.Project(new Vec2(s.X, s.Z));
            var aim = ideal.Sample(Math.Min(ideal.Length, ps + Math.Max(5, s.SpeedMps))).pos;
            double want = SteeringAssist.CommandToward(s, aim, P);
            var player = new CarControls(Math.Max(-1, Math.Min(1, want * turnFraction)), s.SpeedMps < 8 ? 0.5 : 0, 0);
            var c = assistor.Apply(s, player, guide, P, level, Dt);
            biggestJump = Math.Max(biggestJump, Math.Abs((c.Steer - player.Steer) - last));
            last = c.Steer - player.Steer;
            s = CarModel.Step(s, c, P, Dt);
            double d = ideal.Project(new Vec2(s.X, s.Z)).distance;
            if (d > worst && ps < ideal.Length - 20) worst = d;
        }
        finalZ = s.Z;
        return worst;
    }

    [Test]
    public void KeepsAnEightyPercentTurnerInLaneAndIsSmooth()
    {
        double helped = Run(AssistLevel.Full, 0.8, out double z, out double jump);
        double alone = Run(AssistLevel.Off, 0.8, out _, out _);
        Assert.That(helped, Is.LessThan(1.75), $"inside the 3.5 m lane (was {helped:0.00} m; {alone:0.00} m without)");
        Assert.That(helped, Is.LessThan(alone));
        Assert.That(z, Is.LessThan(-100), "leaves by the south arm");
        Assert.That(jump, Is.LessThan(0.045), "the help never jerks the wheel");
    }

    [Test]
    public void ReinsInAWheelSpinnerSmoothly()
    {
        double helped = Run(AssistLevel.Full, 2.2, out _, out double jump);
        double alone = Run(AssistLevel.Off, 2.2, out _, out _);
        Assert.That(helped, Is.LessThan(2.2), $"(was {helped:0.00} m; {alone:0.00} m without)");
        Assert.That(helped, Is.LessThan(alone));
        Assert.That(jump, Is.LessThan(0.045));
    }
}
