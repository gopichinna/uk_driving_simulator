using System.Collections.Generic;
using System.Linq;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Sim.Tests
{
    public class DriveRecorderTests
    {
        private static readonly CarState Away = new CarState { X = 5000, Z = 5000 };

        private static List<ActorPose> Poses(TownTraffic t) =>
            t.Cars.Select(c => new ActorPose { Id = c.Id, Kind = c.IsCyclist ? ActorKind.Cyclist : ActorKind.Car, X = (float)c.Position.X, Z = (float)c.Position.Z, Heading = (float)c.Heading }).ToList();

        [Test]
        public void RecordsAtTenHertzAndReplaysWhatHappened()
        {
            var town = new TownGeometry();
            var traffic = new TownTraffic(town.Graph, TownGeometry.CreateSignals(), new TownTrafficParams(), 3);
            var rec = new DriveRecorder();
            var truth = new Dictionary<double, List<ActorPose>>();
            for (int i = 0; i <= 50 * 30; i++)
            {
                double t = i * 0.02;
                traffic.Step(0.02, Away, t);
                var poses = Poses(traffic);
                if (rec.Record(t, Away, 0, poses)) truth[t] = poses;
            }
            Assert.That(rec.Frames.Count, Is.EqualTo(301).Within(1), "10 frames a second");

            // Replaying any recorded moment gives back exactly what was there.
            foreach (var (t, poses) in truth.Where((_, k) => k % 37 == 0))
            {
                var f = rec.SampleAt(t)!;
                Assert.That(f.Actors.Length, Is.EqualTo(poses.Count));
                foreach (var p in poses)
                {
                    var q = f.Actors.Single(a => a.Id == p.Id);
                    Assert.That(q.X, Is.EqualTo(p.X).Within(1e-4));
                    Assert.That(q.Z, Is.EqualTo(p.Z).Within(1e-4));
                }
            }
        }

        [Test]
        public void InterpolatesBetweenFramesAndKeepsOnlyTheLastStretch()
        {
            var rec = new DriveRecorder { MaxSeconds = 10 };
            for (int i = 0; i <= 200; i++)
            {
                double t = i * 0.1;
                rec.Record(t, new CarState { X = t * 10, Heading = 3.1 }, 0, new[] { new ActorPose { Id = 1, X = (float)t } });
            }
            Assert.That(rec.StartTime, Is.EqualTo(10.0).Within(0.11));
            var mid = rec.SampleAt(15.05)!;
            Assert.That(mid.Player.X, Is.EqualTo(150.5).Within(1e-6));
            Assert.That(mid.Actors[0].X, Is.EqualTo(15.05f).Within(1e-3));
        }

        [Test]
        public void HeadingsInterpolateTheShortWayRound()
        {
            var rec = new DriveRecorder();
            rec.Record(0, new CarState { Heading = 3.0 }, 0, new ActorPose[0]);
            rec.Record(0.1, new CarState { Heading = -3.0 }, 0, new ActorPose[0]);
            double h = rec.SampleAt(0.05)!.Player.Heading;
            Assert.That(System.Math.Abs(LaneGraph.Angle(h - System.Math.PI)), Is.LessThan(0.01));
        }

        [Test]
        public void MiniJsonReadsContentFiles()
        {
            var o = MiniJson.Obj(MiniJson.Parse("{\"a\": [1, 2.5, -3e1], \"b\": {\"c\": \"x\\\"y\\u00e9\"}, \"d\": true, \"e\": null}"));
            Assert.That(MiniJson.Arr(o["a"]).Select(MiniJson.Num), Is.EqualTo(new[] { 1, 2.5, -30 }));
            Assert.That(MiniJson.Obj(o["b"])["c"], Is.EqualTo("x\"yé"));
            Assert.That(o["d"], Is.EqualTo(true));
            Assert.That(o["e"], Is.Null);
        }
    }
}
