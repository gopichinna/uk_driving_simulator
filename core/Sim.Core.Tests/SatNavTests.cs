using System.IO;
using System.Linq;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Sim.Tests
{
    public class SatNavTests
    {
        private static RoadPackGraph Wolverhampton()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return new RoadPackGraph(RoadPack.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "content/cities/wolverhampton/roadpack.json"))));
        }

        [Test]
        public void GuidesAlongALegalPathAndArrives()
        {
            var b = Wolverhampton();
            var g = b.Graph;
            var plan = new MockTestPlan(g, 7);
            var startLane = g.Segments[b.Spawns[0].lane];
            var (p0, h0) = startLane.Path.Sample(5);
            var car = new CarState { X = p0.X, Z = p0.Z, Heading = h0, SpeedMps = 10 };
            var nav = new SatNav(g);
            nav.SetDestination(plan.NextDestination(p0), car);
            Assert.That(nav.Path.Count, Is.GreaterThan(3), "found a path");
            // Path is legal: each step follows a Next link of the graph.
            for (int i = 0; i + 1 < nav.Path.Count; i++)
            {
                Segment a = g.Segments[nav.Path[i]], c = g.Segments[nav.Path[i + 1]];
                Assert.That(a.Next.Contains(c.Id) || (a.Group >= 0 && a.Group == c.Group), "follows a link or changes lane on the same road");
            }

            // Drive it (teleporting along the path) and check instructions count down and we arrive.
            var (what, dist) = nav.Next(car);
            Assert.That(what, Is.Not.EqualTo(Manoeuvre.None));
            double t = 0;
            foreach (int id in nav.Path.ToList())
            {
                var seg = g.Segments[id];
                for (double s = 0; s < seg.Length; s += 3)
                {
                    var (p, h) = seg.Path.Sample(s);
                    car.X = p.X; car.Z = p.Z; car.Heading = h;
                    nav.Update(car, t += 0.3);
                }
            }
            Assert.That(nav.Arrived, Is.True);
            Assert.That(nav.Replans, Is.EqualTo(0), "never thought we left the route");
        }

        [Test]
        public void ReplansWhenTheDriverGoesAnotherWay()
        {
            var b = Wolverhampton();
            var g = b.Graph;
            var startLane = g.Segments[b.Spawns[0].lane];
            var (p0, h0) = startLane.Path.Sample(5);
            var car = new CarState { X = p0.X, Z = p0.Z, Heading = h0 };
            var nav = new SatNav(g);
            nav.SetDestination(new MockTestPlan(g, 3).NextDestination(p0), car);
            Assert.That(nav.Path.Count, Is.GreaterThan(0));
            // Jump onto some other lane (one that can still reach the destination) that isn't on the planned path.
            var candidates = g.Segments.Where(s => s.Kind == SegmentKind.Lane && s.Length > 40 && !nav.Path.Contains(s.Id) && s.Next.Count > 0 && (s.Path.Sample(0).pos - p0).Length > 150).ToList();
            int replansBefore = nav.Replans;
            Segment? used = null;
            foreach (var other in candidates.Take(20))
            {
                var (p, h) = other.Path.Sample(10);
                car.X = p.X; car.Z = p.Z; car.Heading = h;
                nav.Update(car, 10 * candidates.IndexOf(other));
                nav.Update(car, 10 * candidates.IndexOf(other) + 2);
                if (nav.Path.Count > 0 && nav.Path[0] == other.Id) { used = other; break; }
            }
            Assert.That(nav.Replans, Is.GreaterThan(replansBefore));
            Assert.That(used, Is.Not.Null, "new path starts where the driver is");
            Assert.That(SatNav.Describe(Manoeuvre.TurnLeft, 120), Is.EqualTo("In 120 m, turn left"));
        }
    }
}
