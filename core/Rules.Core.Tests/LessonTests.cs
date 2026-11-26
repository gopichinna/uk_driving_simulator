using System.Collections.Generic;
using System.IO;
using System.Linq;
using LToLocal.Rules;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    public class LessonTests
    {
        private static string Root()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "content");
        }

        private static List<LessonDef> Lessons() =>
            Directory.GetFiles(Path.Combine(Root(), "lessons/gb"), "*.json").Where(f => Path.GetFileName(f)[0] != '_' && !f.EndsWith("rule_index.json") && !f.EndsWith("progression.json"))
                .Select(f => LessonDef.FromJson(File.ReadAllText(f))).OrderBy(l => l.Order).ToList();

        [Test]
        public void EveryLessonScenarioAndQuizLoads()
        {
            var lessons = Lessons();
            Assert.That(lessons.Count, Is.GreaterThanOrEqualTo(18));
            Assert.That(lessons.Count(l => l.FreeTier), Is.EqualTo(3));
            foreach (var l in lessons)
            {
                Assert.That(l.LearnBlocks, Is.Not.Empty, l.Id);
                Assert.That(l.Scenarios, Is.Not.Empty, l.Id);
                foreach (var s in l.Scenarios)
                {
                    var def = ScenarioDef.FromJson(File.ReadAllText(Path.Combine(Root(), "scenarios/gb", s.Id + ".json")));
                    Assert.That(def.Course, Is.EqualTo(s.Course), s.Id);
                    Assert.That(def.Events.Select(e => e.Type), Is.SubsetOf(new[] { "pedestrian_waits", "pedestrian_steps_out", "car_emerges", "parked_car", "cyclist_ahead", "lights_turn_amber", "traffic_density", "finish_at", "take_exit", "finish_after_s", "independent_driving", "conditions", "red_x", "variable_limit", "slow_vehicle_ahead", "breakdown" }));
                }
                var quiz = QuizDef.FromJson(File.ReadAllText(Path.Combine(Root(), "lessons/gb/quiz", l.Id + ".json")));
                Assert.That(quiz.Questions.All(q => q.AnswerIndex >= 0 && q.AnswerIndex < q.Options.Count), l.Id);
            }
        }

        [Test]
        public void StarsAndUnlocking()
        {
            var lessons = Lessons();
            var first = lessons[0];
            ScenarioResult Clean() => new ScenarioResult { Completed = true, Summary = new DriveSummary() };
            ScenarioResult WithMinor() { var r = Clean(); r.Summary.Driving = 1; return r; }
            ScenarioResult WithSerious() { var r = Clean(); r.Summary.Serious = 1; return r; }
            var cleanAll = first.Scenarios.Select(_ => Clean()).ToList();
            Assert.That(Stars.For(first, cleanAll, quizAllCorrect: true), Is.EqualTo(3));
            Assert.That(Stars.For(first, cleanAll, quizAllCorrect: false), Is.EqualTo(2));
            Assert.That(Stars.For(first, first.Scenarios.Select(_ => WithMinor()).ToList(), true), Is.EqualTo(2));
            Assert.That(Stars.For(first, first.Scenarios.Select(_ => WithSerious()).ToList(), true), Is.EqualTo(1));

            var progress = new LessonProgress();
            var stage1 = lessons.First(l => l.Stage == 1);
            Assert.That(progress.Unlocked(first, lessons), Is.True);
            Assert.That(progress.Unlocked(stage1, lessons), Is.False);
            foreach (var l in lessons.Where(l => l.Stage == 0)) progress.Record(l.Id, 1);
            Assert.That(progress.Unlocked(stage1, lessons), Is.True);
            var reloaded = LessonProgress.Parse(progress.Serialize());
            Assert.That(reloaded.StarsFor(first.Id), Is.EqualTo(1));
            Assert.That(reloaded.Record(first.Id, 1), Is.False, "keeps the best");
        }

        [Test]
        public void ProgressionDataControlsUnlocks()
        {
            var prog = Progression.FromJson(File.ReadAllText(Path.Combine(Root(), "lessons/gb/progression.json")));
            var lessons = Lessons();
            Assert.That(prog.FreeTier, Is.EquivalentTo(lessons.Where(l => l.FreeTier).Select(l => l.Id)));
            var stage1 = lessons.First(l => l.Stage == 1);
            var rule = prog.StageOf(1)!;
            var progress = new LessonProgress();
            Assert.That(progress.Unlocked(stage1, lessons, prog), Is.False);
            int given = 0;
            foreach (var id in prog.StageOf(rule.UnlockAfterStage)!.LessonIds)
                if (given < rule.MinStars) { int s = System.Math.Min(3, rule.MinStars - given); progress.Record(id, s); given += s; }
            Assert.That(progress.Unlocked(stage1, lessons, prog), Is.True, $"{rule.MinStars} stars in stage {rule.UnlockAfterStage} unlock stage 1");
        }

        [Test]
        public void UnplayableStagesAreSkippedWhenUnlocking()
        {
            var prog = Progression.FromJson("{\"stages\":[{\"stage\":5,\"lessonIds\":[\"a\"],\"unlockAfter\":null},{\"stage\":6,\"lessonIds\":[\"m\"],\"unlockAfter\":{\"stage\":5,\"minStars\":2}},{\"stage\":7,\"lessonIds\":[\"c\"],\"unlockAfter\":{\"stage\":6,\"minStars\":3}}]}");
            var lessons = new List<LessonDef> { new LessonDef { Id = "a", Stage = 5 }, new LessonDef { Id = "m", Stage = 6 }, new LessonDef { Id = "c", Stage = 7 } };
            var p = new LessonProgress();
            p.Record("a", 2);
            Assert.That(p.Unlocked(lessons[2], lessons, prog), Is.False, "normally needs motorway stars");
            Assert.That(p.Unlocked(lessons[2], lessons, prog, stage => stage != 6), Is.True, "motorways not built: stage 5 stars (2) unlock stage 7");
        }

                [Test]
        public void BreakdownPassesOnlyWhenStoppedSafely()
        {
            var world = new FakeWorld();
            var d = new ScenarioDirector(Def("{\"scenarioId\":\"b\",\"course\":\"motorway\",\"anchor\":\"start\",\"durationS\":90,\"events\":[{\"type\":\"breakdown\",\"atM\":100},{\"type\":\"finish_after_s\",\"seconds\":20}]}"));
            var car = new CarState { SpeedMps = 25 };
            d.Start(world, car);
            for (int i = 0; i < 50 * 6; i++) { car.Z += car.SpeedMps * 0.02; d.Step(0.02, world, car); }
            Assert.That(d.BrokenDown, Is.True);
            car.SpeedMps = 10;
            for (int i = 0; i < 50 * 30; i++) d.Step(0.02, world, car);
            Assert.That(d.Finished, Is.False, "still rolling: the clock doesn't end it");
            car.SpeedMps = 0;
            d.Step(0.02, world, car);
            Assert.That(d.Finished && !d.Completed, "stopped in a live lane");
            Assert.That(d.Note, Does.Contain("hard shoulder"));
        }

        [Test]
        public void IndependentDrivingRunsForItsMinutes()
        {
            var world = new FakeWorld();
            var d = new ScenarioDirector(Def("{\"scenarioId\":\"m\",\"course\":\"wolverhampton\",\"anchor\":\"start\",\"durationS\":180,\"events\":[{\"type\":\"independent_driving\",\"minutes\":35,\"mode\":\"mixed\"},{\"type\":\"finish_after_s\",\"seconds\":170}]}"));
            d.Start(world, new CarState());
            Assert.That(d.IsIndependentDriving && d.IndependentMode == "mixed");
            var fog = new ScenarioDirector(Def("{\"scenarioId\":\"f\",\"course\":\"town\",\"anchor\":\"start\",\"durationS\":90,\"events\":[{\"type\":\"conditions\",\"timeOfDay\":\"night\",\"weather\":\"fog\",\"visibilityM\":80}]}"));
            fog.Start(world, new CarState());
            Assert.That((fog.TimeOfDay, fog.Weather, fog.VisibilityM), Is.EqualTo(("night", "fog", 80.0)));
            for (int i = 0; i < 50 * 200; i++) d.Step(0.02, world, new CarState());
            Assert.That(d.Finished, Is.False, "the 35 minutes win over the scenario's own short timers");
            for (int i = 0; i < 50 * 60 * 35; i++) d.Step(0.02, world, new CarState());
            Assert.That(d.Finished && d.Completed);
        }

        private sealed class FakeWorld : IScenarioWorld
        {
            public readonly List<string> Calls = new List<string>();
            public int Exit;
            public bool TryLocate(string name, out Vec2 position, out double heading)
            {
                heading = 0;
                position = name switch { "zebra" => new Vec2(0, 100), "start" => new Vec2(0, 0), "crossroads_signals_line" => new Vec2(0, 200), _ => new Vec2(0, 0) };
                return true;
            }
            public void SetTrafficDensity(double v) => Calls.Add($"density {v}");
            public void PedestrianWaits(string c) => Calls.Add($"waits {c}");
            public void PedestrianStepsOut(string c) => Calls.Add($"steps {c}");
            public void CarEmerges(string j, double gap, in CarState p) => Calls.Add($"emerges {j}");
            public void ParkedCar(string a, double o) => Calls.Add($"parked {a}");
            public void CyclistAhead(string a, double o, double mph) => Calls.Add($"cyclist {a}");
            public bool LightsTurnAmber(in CarState p) { Calls.Add("amber"); return true; }
            public int ExitTaken(in CarState p) => Exit;
        }

        private static ScenarioDef Def(string json) => ScenarioDef.FromJson(json);

        [Test]
        public void DirectorFiresEventsWhenThePlayerIsCloseAndFinishes()
        {
            var def = Def("{\"scenarioId\":\"t\",\"course\":\"town\",\"anchor\":\"start\",\"durationS\":120,\"events\":[" +
                          "{\"type\":\"traffic_density\",\"value\":0.4},{\"type\":\"pedestrian_waits\",\"crossing\":\"zebra\",\"whenPlayerWithinM\":40}," +
                          "{\"type\":\"lights_turn_amber\",\"whenPlayerWithinM\":45},{\"type\":\"finish_at\",\"anchor\":\"start\",\"offsetM\":220}]}");
            var world = new FakeWorld();
            var d = new ScenarioDirector(def);
            var car = new CarState { Z = 0 };
            d.Start(world, car);
            Assert.That(world.Calls, Is.EqualTo(new[] { "density 0.4" }));
            for (double t = 0; t < 30 && !d.Finished; t += 0.02)
            {
                car.Z += 10 * 0.02;
                d.Step(0.02, world, car);
                if (car.Z < 55) Assert.That(world.Calls, Does.Not.Contain("waits zebra"));
            }
            Assert.That(world.Calls, Does.Contain("waits zebra"));
            Assert.That(world.Calls, Does.Contain("amber"));
            Assert.That(d.Finished && d.Completed, "reached the finish point");
            Assert.That(car.Z, Is.EqualTo(220).Within(1));
        }

        [Test]
        public void DirectorChecksTheRoundaboutExitAndTimesOut()
        {
            var world = new FakeWorld();
            var d = new ScenarioDirector(Def("{\"scenarioId\":\"r\",\"course\":\"roundabout\",\"anchor\":\"start\",\"durationS\":60,\"events\":[{\"type\":\"take_exit\",\"exitNumber\":3}]}"));
            d.Start(world, new CarState());
            Assert.That(d.RequiredExit, Is.EqualTo(3));
            d.Step(0.02, world, new CarState());
            Assert.That(d.Finished, Is.False);
            world.Exit = 2;
            d.Step(0.02, world, new CarState());
            Assert.That(d.Finished && d.Completed);
            Assert.That(d.Note, Does.Contain("exit 2"));

            var t = new ScenarioDirector(Def("{\"scenarioId\":\"x\",\"course\":\"dual_carriageway\",\"anchor\":\"start\",\"durationS\":10,\"events\":[]}"));
            t.Start(world, new CarState());
            for (int i = 0; i < 600; i++) t.Step(0.02, world, new CarState());
            Assert.That(t.Finished && t.Note.Length > 0, "time up");
        }
    }
}
