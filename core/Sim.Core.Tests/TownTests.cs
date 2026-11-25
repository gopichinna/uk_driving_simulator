using System;
using System.Collections.Generic;
using System.Linq;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Sim.Tests
{
    public class TrafficSignalsTests
    {
        [Test]
        public void FollowsTheGbSequenceAndNeverShowsGreenToBothRoads()
        {
            var sig = TownGeometry.CreateSignals();
            var last = new[] { sig.State(0, 0), sig.State(1, 0) };
            var allowed = new Dictionary<LightState, LightState>
            {
                [LightState.Red] = LightState.RedAmber,
                [LightState.RedAmber] = LightState.Green,
                [LightState.Green] = LightState.Amber,
                [LightState.Amber] = LightState.Red,
            };
            var seen = new HashSet<LightState>();
            for (double t = 0; t < sig.CycleS * 3; t += 0.05)
            {
                var a = sig.State(0, t);
                var b = sig.State(1, t);
                Assert.That(a == LightState.Green && b == LightState.Green, Is.False, $"both green at {t}");
                Assert.That(a != LightState.Red && a != LightState.RedAmber && b != LightState.Red && b != LightState.RedAmber, Is.False,
                    $"no all-red protection at {t}");
                for (int g = 0; g < 2; g++)
                {
                    var now = g == 0 ? a : b;
                    if (now != last[g]) Assert.That(now, Is.EqualTo(allowed[last[g]]), $"group {g} went {last[g]} → {now} at {t}");
                    last[g] = now;
                    seen.Add(now);
                }
            }
            Assert.That(seen.Count, Is.EqualTo(4));
        }

        [Test]
        public void TimingsMatchTheParameters()
        {
            var sig = new TrafficSignals(new[] { new[] { 0 }, new[] { 1 } }, new[] { 10.0, 8.0 }, amberS: 3, allRedS: 2, redAmberS: 2);
            Assert.That(sig.CycleS, Is.EqualTo(10 + 3 + 2 + 8 + 3 + 2));
            Assert.That(sig.State(0, 9.9), Is.EqualTo(LightState.Green));
            Assert.That(sig.State(0, 10.1), Is.EqualTo(LightState.Amber));
            Assert.That(sig.State(0, 13.1), Is.EqualTo(LightState.Red));
            Assert.That(sig.State(1, 13.1), Is.EqualTo(LightState.RedAmber));
            Assert.That(sig.State(1, 15.1), Is.EqualTo(LightState.Green));
            Assert.That(sig.TimeToGreen(1, 0), Is.EqualTo(15.0).Within(0.11));
        }
    }

    public class TownGeometryTests
    {
        private static readonly TownGeometry G = new TownGeometry();

        [Test]
        public void HasTheExpectedLanesConnectorsAndControls()
        {
            var lanes = G.Graph.Segments.Where(s => s.Kind == SegmentKind.Lane).ToList();
            var connectors = G.Graph.Segments.Where(s => s.Kind == SegmentKind.Connector).ToList();
            Assert.That(lanes.Count, Is.EqualTo(16));
            Assert.That(connectors.Count(c => c.Node == G.NodeA), Is.EqualTo(12), "crossroads: 4 in × 3 out");
            Assert.That(connectors.Count(c => c.Node == G.NodeB), Is.EqualTo(6));
            Assert.That(connectors.Count(c => c.Node == G.NodeD), Is.EqualTo(6));
            Assert.That(connectors.Count(c => c.Node == G.NodeC), Is.EqualTo(2), "bend");
            Assert.That(lanes.Count(l => l.EndControl == LineControl.GiveWay), Is.EqualTo(2));
            Assert.That(lanes.Count(l => l.EndControl == LineControl.Signals), Is.EqualTo(4));
            Assert.That(lanes.Where(l => l.EndControl == LineControl.Signals).All(l => l.HasAdvancedStopLine), "every signal approach has a cycle box");
            Assert.That(lanes.Single(l => l.Name.StartsWith("Church Street (east")).SpeedLimitMph, Is.EqualTo(20));
        }

        [Test]
        public void LanesKeepLeft()
        {
            foreach (var l in G.Graph.Segments.Where(s => s.Kind == SegmentKind.Lane))
            {
                var road = G.Roads.Single(r => r.Forward == l.Id || r.Backward == l.Id);
                var centre = new PathPolyline(road.Centreline);
                var (mid, heading) = l.Path.Sample(l.Length / 2);
                var (cs, _, _) = centre.Project(mid);
                var (cp, _) = centre.Sample(cs);
                double side = (mid - cp).Dot(Vec2.FromHeading(heading).LeftNormal);
                Assert.That(side, Is.EqualTo(TownGeometry.LaneWidthM / 2).Within(0.2), l.Name);
            }
        }

        [Test]
        public void TurnsAreClassifiedForLeftHandTraffic()
        {
            var eastbound = G.Graph.Segments[G.Roads[1].Forward]; // High St, B → A
            var turns = eastbound.Next.Select(i => G.Graph.Segments[i]).ToList();
            var north = turns.Single(t => t.Path.Sample(t.Length).pos.Z > 5);
            var south = turns.Single(t => t.Path.Sample(t.Length).pos.Z < -5);
            Assert.That(north.Turn, Is.EqualTo(TurnKind.Near), "turning left (north) from eastbound is the near turn");
            Assert.That(south.Turn, Is.EqualTo(TurnKind.Far));
        }

        [Test]
        public void SideRoadGivesWayAndRightTurnersGiveWayToOncoming()
        {
            var g = G.Graph;
            var atB = g.Segments.Where(s => s.Kind == SegmentKind.Connector && s.Node == G.NodeB).ToList();
            foreach (var minor in atB.Where(c => g.Segments[c.FromLane].EndControl == LineControl.GiveWay))
                foreach (var cp in minor.Conflicts)
                    Assert.That(g.MustYield(minor, g.Segments[cp.Other]), Is.True, $"{minor.Name} must give way to {g.Segments[cp.Other].Name}");

            var farFromMajor = atB.Single(c => c.Turn == TurnKind.Far && g.Segments[c.FromLane].EndControl == LineControl.None);
            Assert.That(farFromMajor.Conflicts.Any(cp => g.MustYield(farFromMajor, g.Segments[cp.Other])), Is.True);

            // At the lights, east-west and north-south never give way to each other (never green together).
            var atA = g.Segments.Where(s => s.Kind == SegmentKind.Connector && s.Node == G.NodeA).ToList();
            var ewAhead = atA.First(c => c.Turn == TurnKind.Ahead && g.Segments[c.FromLane].SignalGroup == TownGeometry.GroupEastWest);
            var nsAhead = atA.First(c => c.Turn == TurnKind.Ahead && g.Segments[c.FromLane].SignalGroup == TownGeometry.GroupNorthSouth);
            Assert.That(g.MustYield(ewAhead, nsAhead), Is.False);
            Assert.That(ewAhead.Conflicts.Any(cp => cp.Other == nsAhead.Id), Is.True, "paths do cross");
        }

        [Test]
        public void CrossingsLieOnTheRightLanes()
        {
            var g = G.Graph;
            var zebraLanes = g.Segments.Where(s => s.Crossings.Any(c => c.crossing == G.Zebra)).ToList();
            Assert.That(zebraLanes.Count, Is.EqualTo(2));
            Assert.That(zebraLanes.All(s => s.Name.StartsWith("High Street")));
            var mouth = g.Segments.Where(s => s.Crossings.Any(c => c.crossing == G.MillLaneMouth)).ToList();
            Assert.That(mouth.All(s => s.Name.StartsWith("Mill Lane")), string.Join(", ", mouth.Select(m => m.Name)));
            Assert.That(G.Graph.ZigZags.Count, Is.EqualTo(2));
        }

        [Test]
        public void AnchorsPromisedToTheLessonsExist()
        {
            foreach (var name in new[] { "start", "crossroads_signals", "tjunction_north", "tjunction_south", "zebra", "box_junction", "school_street" })
            {
                Assert.That(G.Anchors.ContainsKey(name), name);
                var (pos, _) = G.AnchorPose(name);
                Assert.That(double.IsNaN(pos.X), Is.False);
            }
            Assert.That(G.SpeedLimitAt(G.AnchorPose("school_street").pos), Is.EqualTo(20));
            Assert.That(G.SpeedLimitAt(G.AnchorPose("start").pos), Is.EqualTo(30));
        }
    }

    public class TownTrafficTests
    {
        private static readonly CarState Away = new CarState { X = 5000, Z = 5000 };

        [TestCase(7UL)]
        [TestCase(21UL)]
        [TestCase(99UL)]
        public void SoakTestKeepsTrafficFlowingSafelyAndLegally(ulong seed)
        {
            var town = new TownGeometry();
            var signals = TownGeometry.CreateSignals();
            var traffic = new TownTraffic(town.Graph, signals, new TownTrafficParams { TargetCars = 18 }, seed);
            var peds = new TownPedestrians(town.Graph, town.PedestrianRoutes, 10, seed + 1);
            traffic.Pedestrians = peds;
            var g = town.Graph;
            const double dt = 0.02;
            var lastS = new Dictionary<int, (int seg, double s)>();
            var stoppedSince = new Dictionary<int, double>();
            var seenIds = new HashSet<int>();
            int pedCrossings = 0;
            var wasCrossing = new HashSet<int>();

            for (int step = 0; step < 50 * 600; step++)
            {
                double t = step * dt;
                peds.Step(dt, traffic, Away);
                traffic.Step(dt, Away, t);

                foreach (var c in traffic.Cars)
                {
                    seenIds.Add(c.Id);
                    var seg = g.Segments[c.Segment];
                    // Never cross a stop line on red.
                    if (lastS.TryGetValue(c.Id, out var prev) && prev.seg == c.Segment && seg.EndControl == LineControl.Signals && !c.IsCyclist)
                    {
                        var light = signals.State(seg.SignalGroup, t);
                        if (prev.s < seg.StopLineS && c.S >= seg.StopLineS)
                            Assert.That(light, Is.Not.EqualTo(LightState.Red).And.Not.EqualTo(LightState.RedAmber), $"car {c.Id} ran a red at {t:0.0}s");
                    }
                    lastS[c.Id] = (c.Segment, c.S);

                    // Never parked in the yellow box.
                    if (Geo2.PointInPolygon(c.Position, g.Nodes[town.NodeA].Box!) && c.SpeedMps < 0.3)
                    {
                        if (!stoppedSince.ContainsKey(c.Id)) stoppedSince[c.Id] = t;
                        Assert.That(t - stoppedSince[c.Id], Is.LessThan(4.0), $"car {c.Id} stuck in the box at {t:0.0}s");
                    }
                    else stoppedSince.Remove(c.Id);
                }

                // No two vehicles on top of each other.
                var cars = traffic.Cars;
                for (int i = 0; i < cars.Count; i++)
                    for (int j = i + 1; j < cars.Count; j++)
                    {
                        double d = (cars[i].Position - cars[j].Position).Length;
                        Assert.That(d, Is.GreaterThan(1.6), $"cars {cars[i].Id} and {cars[j].Id} overlap at {t:0.0}s on {g.Segments[cars[i].Segment].Name} / {g.Segments[cars[j].Segment].Name}");
                    }

                // Nobody drives through a person on a crossing.
                foreach (var p in peds.People)
                {
                    if (p.State == PedState.Crossing && wasCrossing.Add(p.Id)) pedCrossings++;
                    if (p.State != PedState.Crossing) continue;
                    foreach (var c in cars)
                        Assert.That((c.Position - p.Position).Length, Is.GreaterThan(1.8), $"car {c.Id} hit pedestrian {p.Id} at {t:0.0}s");
                }
            }

            TestContext.Progress.WriteLine($"seed {seed}: {seenIds.Count} vehicles, {pedCrossings} crossings");
            Assert.That(seenIds.Count, Is.GreaterThan(60), "traffic keeps flowing (cars leave and new ones arrive)");
            Assert.That(pedCrossings, Is.GreaterThan(20), "people get to cross");
            Assert.That(traffic.Cars.Count(c => c.IsCyclist) + seenIds.Count, Is.GreaterThan(0));
        }

        [Test]
        public void StopsForRedAndGoesOnGreen()
        {
            var town = new TownGeometry();
            var signals = TownGeometry.CreateSignals();
            var traffic = new TownTraffic(town.Graph, signals, new TownTrafficParams(), 1);
            var lane = town.Graph.Segments[town.Roads[3].Backward]; // Station Road southbound into the crossroads (north-south group)
            var ahead = lane.Next.Select(i => town.Graph.Segments[i]).Single(c => c.Turn == TurnKind.Ahead);
            var car = traffic.Add(lane.Id, lane.StopLineS - 60, 12, false, ahead.Id);
            double t = 0;
            // North-south is red for the first ~21 s.
            for (; t < 18; t += 0.02) traffic.Step(0.02, Away, t, spawn: false);
            Assert.That(car.Segment, Is.EqualTo(lane.Id));
            Assert.That(car.SpeedMps, Is.LessThan(0.2));
            Assert.That(lane.StopLineS - car.S, Is.InRange(0.0, 2.0), "waits at the first stop line, behind the cycle box");
            for (; t < 40; t += 0.02) traffic.Step(0.02, Away, t, spawn: false);
            Assert.That(car.Segment == lane.Id, Is.False, "went on green");
        }

        [Test]
        public void SideRoadTrafficWaitsForThePlayerOnTheMainRoad()
        {
            var town = new TownGeometry();
            var traffic = new TownTraffic(town.Graph, null, new TownTrafficParams(), 1);
            var mill = town.Graph.Segments[town.Roads[6].Backward]; // Mill Lane northbound, give way at High Street
            var car = traffic.Add(mill.Id, mill.Length - 20, 6, false);
            // Player on High Street eastbound, approaching the junction.
            var high = town.Graph.Segments[town.Roads[0].Forward];
            var (pp, ph) = high.Path.Sample(high.Length - 35);
            var player = new CarState { X = pp.X, Z = pp.Z, Heading = ph, SpeedMps = 12 };
            for (double t = 0; t < 2.0; t += 0.02)
            {
                traffic.Step(0.02, player, t, spawn: false);
                player = CarModel.Step(player, new CarControls(0, 0.3, 0), new CarParams(), 0.02);
            }
            Assert.That(car.Segment, Is.EqualTo(mill.Id), "still behind the give-way line");
            Assert.That(car.BrakingForPlayer, Is.True);
        }

        [Test]
        public void PedestrianWaitsForAFastCarThenCrossesWhenItStops()
        {
            var town = new TownGeometry();
            var traffic = new TownTraffic(town.Graph, null, new TownTrafficParams(), 1);
            var peds = new TownPedestrians(town.Graph, town.PedestrianRoutes, 0, 1);
            traffic.Pedestrians = peds;
            // Route 4 = north footway → zebra → south footway.
            var p = peds.Add(4, 31.5);
            var east = town.Graph.Segments[town.Roads[2].Forward];
            var car = traffic.Add(east.Id, 15, 12, false); // about 45 m away at 27 mph: room to stop
            bool waited = false;
            for (double t = 0; t < 20; t += 0.02)
            {
                peds.Step(0.02, traffic, Away, spawn: false);
                traffic.Step(0.02, Away, t, spawn: false);
                if (p.State == PedState.Waiting) waited = true;
                if (p.State == PedState.Crossing) break;
            }
            Assert.That(waited, Is.True);
            Assert.That(p.State, Is.EqualTo(PedState.Crossing));
            Assert.That(car.SpeedMps, Is.LessThan(3.0), "the car slowed right down for them");
            Assert.That(town.Graph.Segments[car.Segment].Crossings[0].sFrom - car.S, Is.GreaterThan(1.0), "and stopped short of the crossing");
        }
    }
}
