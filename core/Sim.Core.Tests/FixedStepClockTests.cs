using LToLocal.Sim;

namespace Sim.Core.Tests;

public class FixedStepClockTests
{
    [Test]
    public void RunsFiftyTicksPerSimulatedSecond_RegardlessOfFrameRate()
    {
        var at30Fps = new FixedStepClock();
        var at60Fps = new FixedStepClock();

        int ticks30 = 0, ticks60 = 0;
        for (int i = 0; i < 30; i++) ticks30 += at30Fps.Advance(1.0 / 30);
        for (int i = 0; i < 60; i++) ticks60 += at60Fps.Advance(1.0 / 60);

        Assert.That(ticks30, Is.EqualTo(50));
        Assert.That(ticks60, Is.EqualTo(50));
        Assert.That(at30Fps.SimTime, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void CarriesLeftoverTimeIntoAlpha()
    {
        var clock = new FixedStepClock();
        int steps = clock.Advance(0.03); // 1.5 steps

        Assert.That(steps, Is.EqualTo(1));
        Assert.That(clock.Alpha, Is.EqualTo(0.5).Within(1e-6));
    }

    [Test]
    public void CapsStepsAfterALongHitch()
    {
        var clock = new FixedStepClock();
        int steps = clock.Advance(2.0); // a 2-second freeze

        Assert.That(steps, Is.EqualTo(FixedStepClock.DefaultMaxStepsPerFrame));
        Assert.That(clock.Alpha, Is.EqualTo(0));
    }

    [Test]
    public void RejectsNegativeFrameTime()
    {
        var clock = new FixedStepClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-0.01));
    }
}
