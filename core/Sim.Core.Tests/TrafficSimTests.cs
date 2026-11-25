using LToLocal.Sim;

namespace Sim.Core.Tests;

public class TrafficSimTests
{
    private const double Dt = FixedStepClock.DefaultStepSeconds;

    // Dual carriageway: lanes 0/1 northbound (0 = nearside/left), lanes 2/3 southbound (3 = nearside for them).
    private static StraightLane[] DualCarriageway() => new[]
    {
        new StraightLane(-1.825, +1, isNearside: true),
        new StraightLane(+1.825, +1, isNearside: false),
        new StraightLane(8.475, -1, isNearside: false),
        new StraightLane(12.125, -1, isNearside: true),
    };

    private static TrafficSim NewSim(ulong seed = 1, int[]? perLane = null) =>
        new TrafficSim(DualCarriageway(), new TrafficParams { CarsPerLane = perLane ?? new int[4] }, seed);

    // A parked "player" far away so it doesn't interfere.
    private static CarState FarPlayer => new CarState { X = 100, Z = 0 };

    private static void Run(TrafficSim sim, CarState player, double seconds, bool respawn = false)
    {
        int ticks = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < ticks; i++) sim.Step(Dt, player, respawn);
    }

    [Test]
    public void FollowerNeverHitsASlowerLeader()
    {
        var sim = NewSim();
        var leader = sim.AddCar(0, 60, speed: 8, desired: 8);
        var follower = sim.AddCar(0, 0, speed: 18, desired: 18);
        // block the overtaking lane so the follower must stay behind
        sim.AddCar(1, 30, speed: 8, desired: 8);

        for (int i = 0; i < 3000; i++)
        {
            sim.Step(Dt, FarPlayer, respawn: false);
            if (follower.Lane == 0 && !follower.ChangingLane)
                Assert.That(leader.Z - follower.Z, Is.GreaterThan(4.3), $"overlap at tick {i}");
        }
    }

    [Test]
    public void NeverDrivesThroughAStationaryPlayer()
    {
        var sim = NewSim();
        var car = sim.AddCar(0, -80, speed: 18, desired: 18);
        sim.AddCar(1, -60, speed: 18, desired: 18); // lane 2 busy at first
        var player = new CarState { X = -1.825, Z = 0, SpeedMps = 0 };

        for (int i = 0; i < 50 * 20; i++)
        {
            sim.Step(Dt, player, respawn: false);
            bool sameLane = Math.Abs(car.X - player.X) < 1.0;
            if (sameLane && car.Z < player.Z)
                Assert.That(player.Z - car.Z, Is.GreaterThan(4.3), $"drove into the player at tick {i}");
        }

        bool queued = car.Lane == 0 && car.Z < player.Z && car.SpeedMps < 0.5;
        bool overtook = car.Z > player.Z;
        Assert.That(queued || overtook, "should either wait behind the player or overtake in lane 2");
    }

    [Test]
    public void OvertakesSlowCarThenReturnsToTheNearsideLane()
    {
        var sim = NewSim();
        var slow = sim.AddCar(0, 40, speed: 9, desired: 9);
        var fast = sim.AddCar(0, 0, speed: 18, desired: 18);

        bool usedOffside = false, signalledRight = false, signalledLeft = false;
        for (int i = 0; i < 50 * 40; i++)
        {
            sim.Step(Dt, FarPlayer, respawn: false);
            if (fast.Indicator == 1) signalledRight = true;
            if (fast.Lane == 1) usedOffside = true;
            if (usedOffside && fast.Indicator == -1) signalledLeft = true;
        }

        Assert.That(signalledRight, "should indicate right before pulling out");
        Assert.That(usedOffside, "should overtake in lane 2");
        Assert.That(signalledLeft, "should indicate left before moving back");
        Assert.That(fast.Lane, Is.EqualTo(0), "should return to the left lane (keep left)");
        Assert.That(fast.Z, Is.GreaterThan(slow.Z), "should have passed the slow car");
    }

    [Test]
    public void DoesNotPullOutIntoTheSideOfThePlayer()
    {
        var sim = NewSim();
        sim.AddCar(0, 40, speed: 5, desired: 5);
        var fast = sim.AddCar(0, 10, speed: 15, desired: 18);
        var player = new CarState { X = +1.825, Z = 8, SpeedMps = 15 }; // alongside in lane 2

        for (int i = 0; i < 25; i++) // 0.5 s
        {
            player.Z += player.SpeedMps * Dt;
            sim.Step(Dt, player, respawn: false);
        }

        Assert.That(fast.TargetLane, Is.EqualTo(0), "must not start a lane change into an occupied lane");
    }

    [Test]
    public void SameSeedGivesSameTraffic()
    {
        var a = NewSim(7, new[] { 5, 3, 4, 4 });
        var b = NewSim(7, new[] { 5, 3, 4, 4 });
        var player = new CarState { X = -1.825, Z = 0, SpeedMps = 13 };
        a.Reset(0); b.Reset(0);

        for (int i = 0; i < 50 * 30; i++)
        {
            player.Z += player.SpeedMps * Dt;
            a.Step(Dt, player); b.Step(Dt, player);
        }

        Assert.That(a.Cars.Count, Is.EqualTo(b.Cars.Count));
        for (int i = 0; i < a.Cars.Count; i++)
        {
            Assert.That(a.Cars[i].Z, Is.EqualTo(b.Cars[i].Z));
            Assert.That(a.Cars[i].X, Is.EqualTo(b.Cars[i].X));
        }
    }

    [Test]
    public void KeepsTrafficAroundAMovingPlayer()
    {
        var sim = NewSim(3, new[] { 5, 3, 4, 4 });
        var player = new CarState { X = -1.825, Z = 0, SpeedMps = 17 };
        sim.Reset(0);

        for (int i = 0; i < 50 * 120; i++)
        {
            player.Z += player.SpeedMps * Dt;
            sim.Step(Dt, player);
        }

        Assert.That(sim.Cars.Count, Is.GreaterThanOrEqualTo(8));
        foreach (var c in sim.Cars)
            Assert.That(Math.Abs(c.Z - player.Z), Is.LessThan(600));
    }
}
