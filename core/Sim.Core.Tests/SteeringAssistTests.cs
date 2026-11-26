using LToLocal.Sim;

namespace Sim.Core.Tests;

/// <summary>Aim assist: the player steers, the game only nudges a turn that is roughly right.</summary>
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

    [Test]
    public void HandsOffMeansNoHelpAtAll()
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var (pos, h) = geo.Entry[0, 0].Sample(25);
        var car = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };
        var c = SteeringAssist.Apply(car, new CarControls(0, 1, 0), new TwoLaneRoundaboutGuide(geo), P, AssistLevel.Full);
        Assert.That(c.Steer, Is.EqualTo(0));
    }

    [Test]
    public void OffMeansExactlyTheDriversOwnSteering()
    {
        var c = SteeringAssist.Apply(new CarState { SpeedMps = 8 }, new CarControls(0.3, 1, 0), new LaneGraphGuide(TJunction().g), P, AssistLevel.Off);
        Assert.That(c.Steer, Is.EqualTo(0.3));
    }

    [Test]
    public void NudgesAnApproximatelyRightLeftTurnTowardTheLine()
    {
        var (g, _) = TJunction();
        var guide = new LaneGraphGuide(g);
        var car = new CarState { X = 0, Z = 99, Heading = 0, SpeedMps = 4 };      // turning in at the junction
        var targets = new List<Vec2>();
        guide.GetTargets(car, 6, targets);
        double leftCmd = targets.Select(t => SteeringAssist.CommandToward(car, t, P)).Min();   // the left-turn line
        Assert.That(leftCmd, Is.LessThan(-0.3));

        double playerSteer = leftCmd * 0.7;                                          // turns about 70% of the way
        var c = SteeringAssist.Apply(car, new CarControls(playerSteer, 1, 0), guide, P, AssistLevel.Full);
        Assert.That(Math.Abs(c.Steer - leftCmd), Is.LessThan(Math.Abs(playerSteer - leftCmd)), "pulled toward the line");
        Assert.That(c.Steer, Is.LessThan(playerSteer), "still turning left, not past the line");
        Assert.That(c.Steer, Is.GreaterThan(leftCmd - 1e-9));
    }

    [Test]
    public void LeavesAWildTurnAlone()
    {
        var (g, _) = TJunction();
        var car = new CarState { X = 0, Z = 50, Heading = 0, SpeedMps = 6 };       // mid-lane, the only line is straight ahead
        var c = SteeringAssist.Apply(car, new CarControls(0.9, 1, 0), new LaneGraphGuide(g), P, AssistLevel.Full);
        Assert.That(c.Steer, Is.EqualTo(0.9), "hard right matches no line, so the driver steers alone");
    }

    // A driver who turns only 80% as much as needed (Full help keeps them in lane; no help lets them drift).
    private static double WorstErrorFollowingTheSecondExit(AssistLevel level, double turnFraction, out double finalZ)
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var guide = new TwoLaneRoundaboutGuide(geo);
        // The ideal line: in from the north arm, round the outer lane, out by the south arm (second exit).
        var pts = new List<Vec2>(geo.Entry[0, 0].Points);
        double a0 = geo.MergeBearing[0, 0], a1 = geo.DivergeBearing[2, 0];
        double sweep = TwoLaneRoundaboutGeometry.Cw(a0, a1);
        for (double t = 0.05; t < sweep; t += 0.05) pts.Add(geo.RingPoint(a0 + t, TwoLaneRoundaboutGeometry.OuterLaneR));
        pts.AddRange(geo.Exit[2, 0].Points);
        var ideal = new PathPolyline(pts);

        var (pos, h) = geo.Entry[0, 0].Sample(25);
        var s = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };
        double worst = 0;
        for (int i = 0; i < (int)(70 / Dt); i++)
        {
            var (ps, _, _) = ideal.Project(new Vec2(s.X, s.Z));
            var aim = ideal.Sample(Math.Min(ideal.Length, ps + Math.Max(5, s.SpeedMps))).pos;
            double want = SteeringAssist.CommandToward(s, aim, P);
            var player = new CarControls(want * turnFraction, s.SpeedMps < 8 ? 0.5 : 0, 0);
            var c = SteeringAssist.Apply(s, player, guide, P, level);
            s = CarModel.Step(s, c, P, Dt);
            double d = ideal.Project(new Vec2(s.X, s.Z)).distance;
            if (d > worst && ps < ideal.Length - 20) worst = d;
        }
        finalZ = s.Z;
        return worst;
    }

    [Test]
    public void AssistKeepsAnEightyPercentTurnerInLaneRoundTheRoundabout()
    {
        double helped = WorstErrorFollowingTheSecondExit(AssistLevel.Full, 0.8, out double zHelped);
        double alone = WorstErrorFollowingTheSecondExit(AssistLevel.Off, 0.8, out _);
        Assert.That(helped, Is.LessThan(1.75), $"with help, stays inside the 3.5 m lane (was {helped:0.00} m; {alone:0.00} m without)");
        Assert.That(helped, Is.LessThan(alone), "and does better than no help");
        Assert.That(zHelped, Is.LessThan(-100), "and leaves by the south arm");
    }
}
