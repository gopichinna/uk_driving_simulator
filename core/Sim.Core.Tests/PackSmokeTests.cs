using System.IO;
using System.Linq;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Sim.Tests
{
    /// <summary>Every city pack must load into the game's road engine and carry traffic for a few minutes without trouble.</summary>
    public class PackSmokeTests
    {
        private static string PackFile(string city)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "content", "cities", city, "roadpack.json");
        }

        [TestCase("wolverhampton")]
        [TestCase("wolverhampton-test-area")]
        [TestCase("suburb_practice")]
        [TestCase("rural_practice")]
        [TestCase("ring_road_practice")]
        [TestCase("coventry")]
        [TestCase("queslett_roundabout")]
        [TestCase("m54-j2")]
        public void PackLoadsAndCarriesTraffic(string city)
        {
            var built = new RoadPackGraph(RoadPack.Parse(File.ReadAllText(PackFile(city))));
            Assert.That(built.LinkLanes, Is.Not.Empty, "no lanes were built");
            Assert.That(built.Spawns, Is.Not.Empty, "no spawn points");

            var traffic = new TownTraffic(built.Graph, built.Signals, new TownTrafficParams { TargetCars = 12 }, 5);
            var away = new CarState { X = 1e6, Z = 1e6 };
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int step = 0; step < 50 * 180; step++)
            {
                traffic.Step(0.02, away, step * 0.02);
                foreach (var c in traffic.Cars)
                {
                    seen.Add(c.Id);
                    Assert.That(double.IsNaN(c.S) || double.IsInfinity(c.S), Is.False, $"car {c.Id} has an invalid position");
                }
            }
            Assert.That(seen.Count, Is.GreaterThan(3), "traffic never spawned");
        }
    }
}
