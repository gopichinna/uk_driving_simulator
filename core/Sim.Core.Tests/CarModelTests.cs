using LToLocal.Sim;

namespace Sim.Core.Tests;

public class CarModelTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;
    private static readonly CarParams P = new();

    private static CarState Run(CarState s, CarControls c, double seconds)
    {
        int ticks = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < ticks; i++) s = CarModel.Step(s, c, P, Dt);
        return s;
    }

    [Test]
    public void FullThrottleReaches30MphInAFewSeconds()
    {
        var s = Run(new CarState(), new CarControls(0, 1, 0), 6);
        Assert.That(s.SpeedMph, Is.InRange(30, 50));
    }

    [Test]
    public void NeverExceedsTopSpeed()
    {
        var s = Run(new CarState(), new CarControls(0, 1, 0), 120);
        Assert.That(s.SpeedMps, Is.LessThanOrEqualTo(P.TopSpeedMps));
    }

    [Test]
    public void StraightLineHeadsNorth()
    {
        var s = Run(new CarState(), new CarControls(0, 1, 0), 5);
        Assert.That(s.X, Is.EqualTo(0).Within(1e-9));
        Assert.That(s.Z, Is.GreaterThan(10));
    }

    [Test]
    public void PositiveSteerTurnsRight()
    {
        var start = new CarState { SpeedMps = 8 };
        var s = Run(start, new CarControls(0.5, 0.2, 0), 2);
        Assert.That(s.Heading, Is.GreaterThan(0));
        Assert.That(s.X, Is.GreaterThan(0), "turning right from north should move east");
    }

    [Test]
    public void HardBrakingFrom30MphStopsInAboutFourteenMetres()
    {
        // 30 mph ≈ 13.4 m/s; at 8 m/s² braking distance ≈ v²/2a ≈ 11 m (plus coasting effect).
        // The Highway Code's typical braking distance at 30 mph is 14 m (UNVERIFIED until SD-PDF is read).
        var s = Run(new CarState { SpeedMps = 13.41 }, new CarControls(0, 0, 1), 5);
        Assert.That(s.SpeedMps, Is.EqualTo(0));
        Assert.That(s.Z, Is.InRange(9, 15));
    }

    [Test]
    public void DoesNotRollBackwards()
    {
        var s = Run(new CarState(), new CarControls(0, 0, 1), 2);
        Assert.That(s.SpeedMps, Is.EqualTo(0));
        Assert.That(s.Z, Is.EqualTo(0));
    }

    [Test]
    public void ControlsAreClamped()
    {
        var c = new CarControls(5, -1, double.NaN);
        Assert.That(c.Steer, Is.EqualTo(1));
        Assert.That(c.Throttle, Is.EqualTo(0));
        Assert.That(c.Brake, Is.EqualTo(0));
    }
}
