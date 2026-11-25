using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// Player-facing UI text from <c>content/countries/gb/strings/&lt;lang&gt;/ui.json</c> (strings.v0, kind "ui"),
    /// with a small ICU MessageFormat subset: <c>{name}</c> and <c>{name, plural, one {…} other {…}}</c> with <c>#</c>.
    /// Missing keys fall back to the built-in English text passed by the caller.
    /// </summary>
    public sealed class UiStrings
    {
        private readonly Dictionary<string, string> _entries = new Dictionary<string, string>();

        public static UiStrings Empty => new UiStrings();

        public static UiStrings FromJson(string json)
        {
            var s = new UiStrings();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            if (root.Get("entries") is Dictionary<string, object?> e)
                foreach (var kv in e) if (kv.Value is string v) s._entries[kv.Key] = v;
            return s;
        }

        public bool Has(string key) => _entries.ContainsKey(key);

        public string Get(string key, string fallback, params (string name, object value)[] args) =>
            Icu.Format(_entries.TryGetValue(key, out var v) ? v : fallback, args);
    }

    /// <summary>Per-rule text from <c>strings/&lt;lang&gt;/rules.json</c>: title, fault, why, tip, good.</summary>
    public sealed class RuleStrings
    {
        public sealed class Entry
        {
            public string Title = "", Fault = "", Why = "", Tip = "", Good = "";
        }

        private readonly Dictionary<string, Entry> _rules = new Dictionary<string, Entry>();

        public static RuleStrings Empty => new RuleStrings();

        public static RuleStrings FromJson(string json)
        {
            var s = new RuleStrings();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            if (root.Get("rules") is Dictionary<string, object?> rules)
                foreach (var kv in rules)
                    if (kv.Value is Dictionary<string, object?> o)
                        s._rules[kv.Key] = new Entry
                        {
                            Title = o.Get("title") as string ?? "", Fault = o.Get("fault") as string ?? "", Why = o.Get("why") as string ?? "",
                            Tip = o.Get("tip") as string ?? "", Good = o.Get("good") as string ?? "",
                        };
            return s;
        }

        public Entry? For(string ruleId) => _rules.TryGetValue(ruleId, out var e) ? e : null;
    }

    /// <summary>The subset of ICU MessageFormat the content uses.</summary>
    public static class Icu
    {
        public static string Format(string pattern, params (string name, object value)[] args)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < pattern.Length)
            {
                char c = pattern[i];
                if (c != '{') { sb.Append(c); i++; continue; }
                int end = Matching(pattern, i);
                sb.Append(Placeholder(pattern.Substring(i + 1, end - i - 1), args));
                i = end + 1;
            }
            return sb.ToString();
        }

        private static string Placeholder(string body, (string name, object value)[] args)
        {
            int comma = body.IndexOf(',');
            string name = (comma < 0 ? body : body.Substring(0, comma)).Trim();
            object? value = null;
            foreach (var a in args) if (a.name == name) value = a.value;
            if (comma < 0) return value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "{" + body + "}";

            string rest = body.Substring(comma + 1);
            int comma2 = rest.IndexOf(',');
            if (comma2 < 0 || rest.Substring(0, comma2).Trim() != "plural") return "{" + body + "}";
            double n = value == null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var options = Options(rest.Substring(comma2 + 1));
            string key = n == 0 && options.ContainsKey("=0") ? "=0" : n == 1 && options.ContainsKey("=1") ? "=1" : n == 1 ? "one" : "other";
            if (!options.TryGetValue(key, out var text) && !options.TryGetValue("other", out text)) return "";
            return Format(text.Replace("#", n.ToString(CultureInfo.InvariantCulture)), args);
        }

        private static Dictionary<string, string> Options(string s)
        {
            var d = new Dictionary<string, string>();
            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                int start = i;
                while (i < s.Length && s[i] != '{' && !char.IsWhiteSpace(s[i])) i++;
                string key = s.Substring(start, i - start);
                while (i < s.Length && s[i] != '{') i++;
                if (i >= s.Length) break;
                int end = Matching(s, i);
                d[key] = s.Substring(i + 1, end - i - 1);
                i = end + 1;
            }
            return d;
        }

        private static int Matching(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}' && --depth == 0) return i;
            }
            throw new FormatException("Unbalanced braces in message.");
        }
    }

    /// <summary>
    /// How a practical test is marked (from <c>content/countries/gb/test_marking.yaml</c>, verified on GOV.UK):
    /// pass with no more than <see cref="MaxDrivingFaults"/> driving faults and no serious or dangerous fault.
    /// </summary>
    public sealed class TestMarking
    {
        public int MaxDrivingFaults = 15;
        public bool FailOnSerious = true;
        public bool FailOnDangerous = true;
        public string PassSummary = "Practice pass: 15 or fewer driving faults and no serious or dangerous faults.";
        public string FailSummary = "Practice fail: over 15 driving faults, or at least one serious or dangerous fault.";

        /// <summary>Reads the flat keys of test_marking.yaml (no YAML library needed).</summary>
        public static TestMarking FromYaml(string yaml)
        {
            var m = new TestMarking();
            string section = "";
            foreach (var raw in yaml.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#")) continue;
                bool indented = line.StartsWith(" ");
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim().Trim('"');
                if (!indented) section = value.Length == 0 ? key : "";
                if (!indented && key == "maxDrivingFaults" && int.TryParse(value, out int max)) m.MaxDrivingFaults = max;
                if (!indented && key == "failOnSerious") m.FailOnSerious = value == "true";
                if (!indented && key == "failOnDangerous") m.FailOnDangerous = value == "true";
                if (indented && section == "summary" && key == "pass") m.PassSummary = value;
                if (indented && section == "summary" && key == "fail") m.FailSummary = value;
            }
            return m;
        }

        public bool Passed(int driving, int serious, int dangerous) =>
            driving <= MaxDrivingFaults && !(FailOnSerious && serious > 0) && !(FailOnDangerous && dangerous > 0);
    }

    /// <summary>Everything the end-of-drive debrief shows, built from the rule events of one drive.</summary>
    public sealed class DriveSummary
    {
        public sealed class Group
        {
            public string RuleId = "";
            public Severity Worst;
            public readonly List<RuleEvent> Faults = new List<RuleEvent>();
        }

        public int Driving, Serious, Dangerous, Good;
        public bool Passed;
        /// <summary>Faults grouped by rule, worst first, then by first occurrence.</summary>
        public readonly List<Group> Groups = new List<Group>();

        public static DriveSummary From(IReadOnlyList<RuleEvent> events, TestMarking marking)
        {
            var s = new DriveSummary();
            var byRule = new Dictionary<string, Group>();
            foreach (var e in events)
            {
                if (e.Outcome == RuleOutcome.GoodPractice) { s.Good++; continue; }
                switch (e.Severity)
                {
                    case Severity.Driving: s.Driving++; break;
                    case Severity.Serious: s.Serious++; break;
                    case Severity.Dangerous: s.Dangerous++; break;
                    default: continue;
                }
                if (!byRule.TryGetValue(e.RuleId, out var g))
                {
                    g = new Group { RuleId = e.RuleId };
                    byRule[e.RuleId] = g;
                    s.Groups.Add(g);
                }
                g.Faults.Add(e);
                if (e.Severity > g.Worst) g.Worst = e.Severity;
            }
            s.Groups.Sort((a, b) => a.Worst != b.Worst ? b.Worst.CompareTo(a.Worst) : a.Faults[0].Time.CompareTo(b.Faults[0].Time));
            s.Passed = marking.Passed(s.Driving, s.Serious, s.Dangerous);
            return s;
        }
    }
}
