using LToLocal.Sim;

namespace Sim.Core.Tests;

public class SteeringAssistTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private static readonly CarParams P = new();

    // Drives with the wheel left at zero: the assist alone must do the steering. Holds about 8 m/s.
    private static CarState Drive(CarState s, ISteeringGuide guide, Func<CarState, int> indicator, double seconds,
                                  Action<CarState>? each = null, Func<CarState, double>? steer = null)
    {
        for (int i = 0; i < (int)(seconds / Dt); i++)
        {
            var player = new CarControls(steer?.Invoke(s) ?? 0, s.SpeedMps < 8 ? 0.5 : 0, 0);
            var c = SteeringAssist.Apply(s, player, guide, indicator(s), P, AssistLevel.Full, Dt);
            s = CarModel.Step(s, c, P, Dt);
            each?.Invoke(s);
        }
        return s;
    }

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
    public void ZeroPlayerSteerStaysInLaneOnAStraightRoad()
    {
        var (g, _) = TJunction();
        var s = new CarState { X = 1.2, Z = 5, Heading = 0 };       // starts 1.2 m off the lane centre
        s = Drive(s, new LaneGraphGuide(g), _ => 0, 6);
        Assert.That(Math.Abs(s.X), Is.LessThan(0.3));
    }

    [Test]
    public void SignallingLeftTakesTheLeftTurn()
    {
        var (g, _) = TJunction();
        var s = Drive(new CarState { X = 0, Z = 5, Heading = 0, SpeedMps = 6 }, new LaneGraphGuide(g), _ => -1, 20);
        Assert.That(s.X, Is.LessThan(-30), "should have turned left");
    }

    [Test]
    public void NoSignalGoesStraightOn()
    {
        var (g, _) = TJunction();
        var s = Drive(new CarState { X = 0, Z = 5, Heading = 0, SpeedMps = 6 }, new LaneGraphGuide(g), _ => 0, 20);
        Assert.That(s.Z, Is.GreaterThan(150));
        Assert.That(Math.Abs(s.X), Is.LessThan(1.0));
    }

    [Test]
    public void OffMeansExactlyTheDriversOwnSteering()
    {
        var p = new CarControls(0.3, 1, 0);
        var c = SteeringAssist.Apply(new CarState(), p, new LaneGraphGuide(TJunction().g), -1, P, AssistLevel.Off, Dt);
        Assert.That(c.Steer, Is.EqualTo(0.3));
    }

    [Test]
    public void SecondExitOfTheTwoLaneRoundabout()
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var guide = new TwoLaneRoundaboutGuide(geo);
        var (pos, h) = geo.Entry[0, 0].Sample(25);
        var s = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };

        // Signal left only after passing the first exit (the Highway Code way to take the second).
        double firstExit = geo.DivergeBearing[1, 0];
        bool passedFirst = false;
        double worstRadiusOnRing = 0;
        s = Drive(s, guide, st =>
        {
            double theta = geo.BearingOf(new Vec2(st.X, st.Z));
            if (geo.OnRing(new Vec2(st.X, st.Z)) && TwoLaneRoundaboutGeometry.Cw(firstExit, theta) < 1.5 && theta > firstExit) passedFirst = true;
            return passedFirst ? -1 : 0;
        }, 60, st =>
        {
            var p = new Vec2(st.X, st.Z);
            double th = geo.BearingOf(p);   // the steady middle of the ring, between merging in and peeling off
            if (geo.OnRing(p) && th > 1.15 && th < 1.95) worstRadiusOnRing = Math.Max(worstRadiusOnRing, Math.Abs(geo.RadiusOf(p) - TwoLaneRoundaboutGeometry.OuterLaneR));
        });

        Assert.That(passedFirst, Is.True);
        Assert.That(s.Z, Is.LessThan(-100), "should have left by the south arm (second exit)");
        Assert.That(Math.Abs(s.X), Is.LessThan(8));
        Assert.That(worstRadiusOnRing, Is.LessThan(1.0), "stayed in its lane on the ring");
    }

    [Test]
    public void WithoutASignalTheCarStaysOnTheRing()
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var (pos, h) = geo.Entry[0, 0].Sample(25);
        var s = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };
        s = Drive(s, new TwoLaneRoundaboutGuide(geo), _ => 0, 40);
        Assert.That(geo.RadiusOf(new Vec2(s.X, s.Z)), Is.InRange(10.5, 17.5));
    }

    [Test]
    public void SignalLeftOnEntryTakesTheFirstExit()
    {
        var geo = new TwoLaneRoundaboutGeometry(new Vec2(0, 0));
        var (pos, h) = geo.Entry[0, 0].Sample(25);
        var s = new CarState { X = pos.X, Z = pos.Z, Heading = h, SpeedMps = 8 };
        s = Drive(s, new TwoLaneRoundaboutGuide(geo), _ => -1, 40);
        Assert.That(s.X, Is.GreaterThan(60), "first exit from the north arm leads east");
    }
}
