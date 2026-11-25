using LToLocal.Sim;

namespace Sim.Core.Tests;

public class DeterministicRandomTests
{
    [Test]
    public void SameSeedGivesSameSequence()
    {
        var a = new DeterministicRandom(42);
        var b = new DeterministicRandom(42);
        for (int i = 0; i < 1000; i++)
            Assert.That(a.NextUInt64(), Is.EqualTo(b.NextUInt64()));
    }

    [Test]
    public void KnownValuesNeverChange()
    {
        // Golden values: if this test fails, saved replays from older builds would play back differently.
        var rng = new DeterministicRandom(42);
        var first = new[] { rng.NextUInt64(), rng.NextUInt64(), rng.NextUInt64() };
        var again = new DeterministicRandom(42);
        Assert.That(new[] { again.NextUInt64(), again.NextUInt64(), again.NextUInt64() }, Is.EqualTo(first));
        Assert.That(first, Is.EqualTo(GoldenSeed42));
    }

    // Literal xorshift64* outputs for seed 42 (computed independently in Python).
    private static readonly ulong[] GoldenSeed42 =
        { 6255019084209693600UL, 14430073426741505498UL, 14575455857230217846UL };

    [Test]
    public void RestoringStateReplaysTheSameNumbers()
    {
        var rng = new DeterministicRandom(7);
        for (int i = 0; i < 10; i++) rng.NextUInt64();

        ulong saved = rng.State;
        var expected = new[] { rng.NextDouble(), rng.NextDouble(), rng.NextDouble() };

        rng.State = saved;
        Assert.That(new[] { rng.NextDouble(), rng.NextDouble(), rng.NextDouble() }, Is.EqualTo(expected));
    }

    [Test]
    public void RangesStayInBounds()
    {
        var rng = new DeterministicRandom(123);
        for (int i = 0; i < 10_000; i++)
        {
            double d = rng.Range(2.0, 5.0);
            int n = rng.Range(-3, 4);
            Assert.That(d, Is.GreaterThanOrEqualTo(2.0).And.LessThan(5.0));
            Assert.That(n, Is.InRange(-3, 3));
        }
    }

    [Test]
    public void ZeroSeedStillProducesNumbers()
    {
        var rng = new DeterministicRandom(0);
        Assert.That(rng.NextUInt64(), Is.Not.EqualTo(0UL));
    }
}
