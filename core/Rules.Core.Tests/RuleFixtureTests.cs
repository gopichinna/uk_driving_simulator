using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LToLocal.Rules;
using LToLocal.Sim;
using NUnit.Framework;

namespace LToLocal.Rules.Tests
{
    /// <summary>
    /// Replays the scripted drives in content/fixtures/rules (rulefixture.v0) against the rules, so every rule is checked
    /// against pass / minor / serious / dangerous drives written independently from its spec. A rule with no runner yet
    /// is reported as ignored, so each rule that gets implemented only needs a line in <see cref="Runners"/>.
    /// </summary>
    public class RuleFixtureTests
    {
        private delegate void Runner(in DriverFrame frame, IReadOnlyList<RoadUser> users, List<RuleEvent> output);

        /// <summary>Rules that can be replayed so far: ruleId → a factory for a fresh runner per drive.</summary>
        private static readonly Dictionary<string, Func<Runner>> Runners = new Dictionary<string, Func<Runner>>
        {
            [SignalCancelRule.RuleId] = () =>
            {
                var rule = new SignalCancelRule();
                return (in DriverFrame f, IReadOnlyList<RoadUser> u, List<RuleEvent> o) => rule.OnFrame(f, o);
            },
        };

        /// <summary>Fixture drives whose scripted data does not match the rule's wording yet; each names what to fix.</summary>
        private static readonly Dictionary<string, string> KnownMismatches = new Dictionary<string, string>
        {
            ["GB.SIG.MISLEADING:serious_mislead_waiting_car"] =
                "needs the side road's position: the fixture's environment has no junction positions. Ask Hydrogen to add environment.junctionCentres.",
        };

        private static string FixturesDir()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "content", "fixtures", "rules");
        }

        public static IEnumerable<TestCaseData> Drives()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "content"))) dir = dir.Parent;
            foreach (var file in Directory.GetFiles(Path.Combine(dir!.FullName, "content", "fixtures", "rules"), "*.json").OrderBy(f => f))
            {
                var root = MiniJson.Obj(MiniJson.Parse(File.ReadAllText(file)));
                string rule = (string)root["ruleId"]!;
                var drives = MiniJson.Arr(root["drives"]);
                for (int i = 0; i < drives.Count; i++)
                    yield return new TestCaseData(Path.GetFileName(file), i).SetName($"{rule}:{(string)MiniJson.Obj(drives[i])["id"]!}");
            }
        }

        private sealed class Key
        {
            public double T, X, Z, Heading, Speed;
            public int Indicator;
            public bool Horn;
        }

        private static List<Key> Keys(object? arr) =>
            MiniJson.Arr(arr).Select(o =>
            {
                var k = MiniJson.Obj(o);
                return new Key
                {
                    T = MiniJson.Num(k["t"]), X = MiniJson.Num(k["x"]), Z = MiniJson.Num(k["z"]),
                    Heading = MiniJson.Num(k["headingDeg"]) * Math.PI / 180, Speed = MiniJson.Num(k["speedMps"]),
                    Indicator = (string?)k.Get("indicator") switch { "left" => -1, "right" => 1, _ => 0 },
                    Horn = k.Get("hornActive") is true,
                };
            }).OrderBy(k => k.T).ToList();

        private static Key At(List<Key> ks, double t)
        {
            if (t <= ks[0].T) return ks[0];
            if (t >= ks[^1].T) return ks[^1];
            int i = 1;
            while (ks[i].T < t) i++;
            Key a = ks[i - 1], b = ks[i];
            double f = (t - a.T) / (b.T - a.T);
            return new Key
            {
                T = t, X = a.X + (b.X - a.X) * f, Z = a.Z + (b.Z - a.Z) * f, Speed = a.Speed + (b.Speed - a.Speed) * f,
                Heading = a.Heading + LaneGraph.Angle(b.Heading - a.Heading) * f, Indicator = a.Indicator, Horn = a.Horn,
            };
        }

        [TestCaseSource(nameof(Drives))]
        public void ScriptedDriveMatchesItsExpectedResult(string file, int index)
        {
            var root = MiniJson.Obj(MiniJson.Parse(File.ReadAllText(Path.Combine(FixturesDir(), file))));
            string rule = (string)root["ruleId"]!;
            var drive = MiniJson.Obj(MiniJson.Arr(root["drives"])[index]);
            string id = (string)drive["id"]!;
            if (!Runners.TryGetValue(rule, out var make)) Assert.Ignore($"{rule} is not implemented yet (no runner registered).");
            if (KnownMismatches.TryGetValue($"{rule}:{id}", out var why)) Assert.Ignore($"Known mismatch: {why}");

            var player = Keys(drive["playerKeyframes"]);
            var others = MiniJson.Arr(drive["otherUsers"]).Select(o =>
            {
                var u = MiniJson.Obj(o);
                return (id: (string)u["id"]!, kind: (string)u["kind"]!, keys: Keys(u["keyframes"]));
            }).ToList();

            var runner = make();
            var events = new List<RuleEvent>();
            double end = player[^1].T;
            for (double t = player[0].T; t <= end + 1e-9; t += Drive.Dt)
            {
                var p = At(player, t);
                var ego = new CarState { X = p.X, Z = p.Z, Heading = p.Heading, SpeedMps = p.Speed };
                var users = new List<RoadUser>();
                int n = 0;
                foreach (var (uid, kind, keys) in others)
                {
                    var k = At(keys, t);
                    users.Add(new RoadUser(++n, new Vec2(k.X, k.Z), k.Heading, k.Speed, kind == "cyclist"));
                }
                runner(new DriverFrame(t, ego, p.Indicator), users, events);
            }

            var mine = events.Where(e => e.RuleId == rule && e.Outcome == RuleOutcome.Violation).ToList();
            var expected = MiniJson.Arr(drive["expectedEvents"]).Select(MiniJson.Obj).Single(e => (string)e["ruleId"]! == rule);
            string outcome = (string)expected["outcome"]!;
            string severity = (string)expected["severity"]!;
            string kind0 = (string)drive["kind"]!;
            string got = mine.Count == 0 ? "no fault" : string.Join(", ", mine.Select(e => $"{e.Severity} at {e.Time:0.0}s"));
            string context = $"{kind0} drive '{id}': {drive["description"]}  Expected: {outcome}/{severity} (about {expected["timeS"]} s). Got: {got}.";

            if (outcome == "pass") Assert.That(mine, Is.Empty, context);
            else
            {
                Assert.That(mine, Is.Not.Empty, context);
                var want = Enum.Parse<Severity>(severity);
                Assert.That(mine.Max(e => e.Severity), Is.EqualTo(want), context);
            }
        }
    }
}
