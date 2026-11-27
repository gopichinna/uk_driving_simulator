using System.IO;
using System.Linq;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Sim.Tests
{
    public class StopControlTests
    {
        private static string Content(params string[] rel)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "content" }.Concat(rel).ToArray()));
        }

        [Test]
        public void MinorApproachToAStopJunctionGetsAStopLine()
        {
            // The sample junction with its signals swapped for a STOP sign: the side road must stop, the main road must not.
            string json = Content("samples", "roadpack.sample.json")
                .Replace("\"controlledBy\": \"signals\"", "\"controlledBy\": \"stop\"")
                .Replace("\"signalPlans\"", "\"unusedSignalPlans\"");
            var b = new RoadPackGraph(RoadPack.Parse(json));
            var g = b.Graph;
            foreach (int lane in b.LinkLanes["l_south"].forward)
                Assert.That(g.Segments[lane].EndControl, Is.EqualTo(LineControl.Stop), "side road");
            foreach (int lane in b.LinkLanes["l_west"].forward)
                Assert.That(g.Segments[lane].EndControl, Is.Not.EqualTo(LineControl.Stop), "main road");
        }

        [Test]
        public void PacksWithoutStopSignsGetNoStopLines()
        {
            var graph = new RoadPackGraph(RoadPack.Parse(Content("cities", "wolverhampton", "roadpack.json"))).Graph;
            Assert.That(graph.Segments.Any(s => s.EndControl == LineControl.Stop), Is.False);
        }

        [Test, Ignore("Pending Hydrogen R35: the suburb_practice STOP node has both links marked major, so no approach gets a stop line.")]
        public void StopJunctionInTheSuburbPackBecomesAStopLine()
        {
            var graph = new RoadPackGraph(RoadPack.Parse(Content("cities", "suburb_practice", "roadpack.json"))).Graph;
            Assert.That(graph.Segments.Any(s => s.EndControl == LineControl.Stop), Is.True);
        }
    }
}
