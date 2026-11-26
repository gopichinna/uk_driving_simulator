using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Sim.Tests
{
    public class RoadPackTests
    {
        private static string Content(string rel)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return File.ReadAllText(Path.Combine(dir!.FullName, "content", rel));
        }

        private static readonly RoadPack Sample = RoadPack.Parse(Content("samples/roadpack.sample.json"));

        [Test]
        public void ParsesTheSamplePack()
        {
            Assert.That(Sample.Links.Count, Is.EqualTo(3));
            Assert.That(Sample.Nodes["n_junction"].TrafficSignals, Is.True);
            Assert.That(Sample.Restrictions.Single().Type, Is.EqualTo("no_right_turn"));
            Assert.That(Sample.SignalPlans["n_junction"].Stages.Count, Is.EqualTo(2));
            Assert.That(Sample.Attribution, Is.Not.Empty);
            var west = Sample.Links.Single(l => l.Id == "l_west");
            Assert.That((west.LanesForward, west.LanesBackward), Is.EqualTo((2, 1)));
        }

        [Test]
        public void BuildsLeftHandLanesWithNearsideFirst()
        {
            var b = new RoadPackGraph(Sample);
            var (fwd, back) = b.LinkLanes["l_west"]; // eastbound along the link, westbound back
            Assert.That(fwd.Count, Is.EqualTo(2));
            var near = b.Graph.Segments[fwd[0]].Path.Sample(50).pos;
            var off = b.Graph.Segments[fwd[1]].Path.Sample(50).pos;
            var opposite = b.Graph.Segments[back[0]].Path.Sample(50).pos;
            Assert.That(near.Z, Is.GreaterThan(off.Z), "eastbound: the nearside (left) is north");
            Assert.That(off.Z, Is.GreaterThan(0), "eastbound lanes are left of the centreline");
            Assert.That(opposite.Z, Is.LessThan(0), "westbound lane is on the other side");
        }

        [Test]
        public void RespectsTurnRestrictionsSignalsAndCrossings()
        {
            var b = new RoadPackGraph(Sample);
            var g = b.Graph;
            int southIn = b.LinkLanes["l_south"].forward[0];
            var eastLanes = b.LinkLanes["l_east"];
            var targets = g.Segments[southIn].Next.Select(c => g.Segments[c].ToLane).ToList();
            Assert.That(targets.Intersect(eastLanes.forward.Concat(eastLanes.backward)), Is.Empty, "no right turn from Sample Road");
            Assert.That(targets, Is.Not.Empty, "but left is allowed");

            var lights = g.Segments.Where(s => s.EndControl == LineControl.Signals).ToList();
            Assert.That(lights.Count, Is.EqualTo(4), "2 + 1 lanes from the west and east, 1 from the south");
            int westGroup = g.Segments[b.LinkLanes["l_west"].forward[0]].SignalGroup;
            Assert.That(g.Segments[b.LinkLanes["l_south"].forward[0]].SignalGroup, Is.Not.EqualTo(westGroup));
            Assert.That(b.Signals.State(westGroup, 1), Is.EqualTo(LightState.Green));

            Assert.That(g.CrossingAreas.Single().Kind, Is.EqualTo(CrossingKind.Zebra));
            Assert.That(g.Segments.Count(s => s.Crossings.Count > 0), Is.EqualTo(2), "both lanes of Sample Road pass the zebra");
            Assert.That(b.Spawns.Count, Is.EqualTo(1));
        }

        [Test]
        public void MotorwayHasJoinLeaveAndStartPointsAndSafeTraffic()
        {
            var b = new RoadPackGraph(RoadPack.Parse(Content("cities/m54-j2/roadpack.json")));
            RoadPack.Link LinkOf(int lane) => b.Pack.Links.Find(l => l.Id == b.Graph.Segments[lane].SourceId)!;
            var start = b.Anchor("start")!.Value;
            var join = b.Anchor("join")!.Value;
            var leave = b.Anchor("leave")!.Value;
            Assert.That(LinkOf(start.lane).RoadClass, Is.EqualTo("motorway"));
            Assert.That(LinkOf(join.lane).JunctionNumber, Is.Not.Empty, "join starts on a slip road");
            Assert.That(b.Graph.Segments[leave.lane].Next.Any(c => LinkOf(b.Graph.Segments[c].ToLane).JunctionNumber.Length > 0), "leave is just before an exit slip");
            Assert.That(b.Graph.Segments[start.lane].SpeedLimitMph, Is.EqualTo(70));

            var (p, h) = b.Graph.Segments[start.lane].Path.Sample(start.s);
            var player = new CarState { X = p.X, Z = p.Z, Heading = h };
            var traffic = new TownTraffic(b.Graph, b.Signals, new TownTrafficParams { TargetCars = 16, SpawnAroundPlayer = true }, 9);
            int overlaps = 0;
            for (int i = 0; i < 50 * 120; i++)
            {
                traffic.Step(0.02, player, i * 0.02);
                var cars = traffic.Cars;
                for (int x = 0; x < cars.Count; x++)
                    for (int y = x + 1; y < cars.Count; y++)
                        if ((cars[x].Position - cars[y].Position).Length < 1.6)
                        {
                            if (overlaps++ % 200 == 0) TestContext.Progress.WriteLine($"overlap t={i * 0.02:0.0} {b.Graph.Segments[cars[x].Segment].Name} s={cars[x].S:0.0} v={cars[x].SpeedMps:0.0} / {b.Graph.Segments[cars[y].Segment].Name} s={cars[y].S:0.0} v={cars[y].SpeedMps:0.0}");
                        }
            }
            Assert.That(overlaps, Is.LessThan(50));
        }

                [Test]
        public void WolverhamptonPedestriansCrossSafely()
        {
            var b = new RoadPackGraph(RoadPack.Parse(Content("cities/wolverhampton/roadpack.json")));
            var routes = b.LoadPedestrianRoutes(Content("cities/wolverhampton/pedestrians.json"));
            Assert.That(routes.Count, Is.GreaterThan(10));
            var peds = new TownPedestrians(b.Graph, routes, 20, 4);
            var (start, heading) = b.Graph.Segments[b.Spawns[0].lane].Path.Sample(b.Spawns[0].s);
            var player = new CarState { X = start.X, Z = start.Z, Heading = heading };
            var traffic = new TownTraffic(b.Graph, b.Signals, new TownTrafficParams { TargetCars = 18, SpawnAroundPlayer = true }, 6) { Pedestrians = peds };
            int crossings = 0, hits = 0;
            var seen = new HashSet<int>();
            for (int i = 0; i < 50 * 240; i++)
            {
                peds.Step(0.02, traffic, player);
                traffic.Step(0.02, player, i * 0.02);
                foreach (var p in peds.People)
                {
                    if (p.State != PedState.Crossing) continue;
                    if (seen.Add(p.Id)) crossings++;
                    foreach (var c in traffic.Cars) if ((c.Position - p.Position).Length < 1.8) hits++;
                }
            }
            TestContext.Progress.WriteLine($"Wolverhampton: {routes.Count} routes, {crossings} people crossed, {b.Graph.CrossingAreas.Count} crossing areas");
            Assert.That(crossings, Is.GreaterThan(5));
            Assert.That(hits, Is.EqualTo(0), "no one is hit while crossing");
        }

                [Test]
        public void LoadsWolverhamptonAndAiDrivesItSafely()
        {
            var sw = Stopwatch.StartNew();
            var pack = RoadPack.Parse(Content("cities/wolverhampton/roadpack.json"));
            var b = new RoadPackGraph(pack);
            sw.Stop();
            TestContext.Progress.WriteLine($"Wolverhampton: {b.Graph.Segments.Count} segments, {b.Graph.Nodes.Count} nodes, {b.Signals.GroupCount} signal groups, built in {sw.ElapsedMilliseconds} ms");
            Assert.That(pack.Attribution, Does.Contain("OpenStreetMap"));
            var lanes = b.Graph.Segments.Where(s => s.Kind == SegmentKind.Lane).ToList();
            Assert.That(lanes.Count(l => l.Next.Count == 0), Is.LessThan(lanes.Count / 50), "the main network has (almost) no dead ends");

            var (start, heading) = b.Graph.Segments[b.Spawns[0].lane].Path.Sample(b.Spawns[0].s);
            var player = new CarState { X = start.X, Z = start.Z, Heading = heading };
            var traffic = new TownTraffic(b.Graph, b.Signals, new TownTrafficParams { TargetCars = 18, SpawnAroundPlayer = true }, 5);
            var moved = new Dictionary<int, double>();
            int overlaps = 0;
            for (int i = 0; i < 50 * 180; i++)
            {
                traffic.Step(0.02, player, i * 0.02);
                foreach (var c in traffic.Cars) moved[c.Id] = (moved.TryGetValue(c.Id, out var m) ? m : 0) + c.SpeedMps * 0.02;
                var cars = traffic.Cars;
                for (int x = 0; x < cars.Count; x++)
                    for (int y = x + 1; y < cars.Count; y++)
                        if ((cars[x].Position - cars[y].Position).Length < 1.6) overlaps++;
            }
            TestContext.Progress.WriteLine($"vehicles seen {moved.Count}, overlap-steps {overlaps}, median distance {moved.Values.OrderBy(v => v).ElementAt(moved.Count / 2):0} m");
            Assert.That(moved.Count, Is.GreaterThan(18));
            Assert.That(moved.Values.OrderBy(v => v).ElementAt(moved.Count / 2), Is.GreaterThan(100), "traffic keeps moving");
            Assert.That(overlaps, Is.LessThan(50), "vehicles (almost) never overlap (50 steps = 1 s in 3 minutes)");
        }
    }
}
