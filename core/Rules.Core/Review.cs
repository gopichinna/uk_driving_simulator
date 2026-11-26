using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// Spaced repetition of rules the player got wrong (content/lessons/gb/progression.json "review"): a fault puts the
    /// rule back to the first interval; a clean review drive moves it to the next; after the last interval it's done.
    /// Days are whole day numbers supplied by the caller (so tests can control time).
    /// </summary>
    public sealed class ReviewSchedule
    {
        private readonly Dictionary<string, (int stage, int due)> _items = new Dictionary<string, (int, int)>();
        public int[] IntervalsDays { get; set; } = { 1, 3, 7, 14 };
        public int MaxPerDay { get; set; } = 3;

        public int Count => _items.Count;

        public void RecordFaults(IEnumerable<string> ruleIds, int today)
        {
            foreach (var id in ruleIds) _items[id] = (0, today + IntervalsDays[0]);
        }

        public void RecordReview(string ruleId, bool clean, int today)
        {
            if (!_items.TryGetValue(ruleId, out var item)) { if (!clean) RecordFaults(new[] { ruleId }, today); return; }
            if (!clean) { _items[ruleId] = (0, today + IntervalsDays[0]); return; }
            int next = item.stage + 1;
            if (next >= IntervalsDays.Length) _items.Remove(ruleId);
            else _items[ruleId] = (next, today + IntervalsDays[next]);
        }

        /// <summary>Rules due today (earliest first), at most <see cref="MaxPerDay"/> minus reviews already done today.</summary>
        public List<string> Due(int today, int doneToday)
        {
            var due = new List<(string id, int day)>();
            foreach (var kv in _items) if (kv.Value.due <= today) due.Add((kv.Key, kv.Value.due));
            due.Sort((a, b) => a.day != b.day ? a.day.CompareTo(b.day) : string.CompareOrdinal(a.id, b.id));
            var result = new List<string>();
            for (int i = 0; i < due.Count && result.Count < Math.Max(0, MaxPerDay - doneToday); i++) result.Add(due[i].id);
            return result;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in _items) sb.Append(kv.Key).Append(':').Append(kv.Value.stage).Append(':').Append(kv.Value.due).Append(';');
            return sb.ToString();
        }

        public static ReviewSchedule Parse(string s, int[]? intervals = null, int maxPerDay = 3)
        {
            var r = new ReviewSchedule { MaxPerDay = maxPerDay };
            if (intervals != null && intervals.Length > 0) r.IntervalsDays = intervals;
            foreach (var part in (s ?? "").Split(';'))
            {
                var f = part.Split(':');
                if (f.Length == 3 && int.TryParse(f[1], out int stage) && int.TryParse(f[2], out int due)) r._items[f[0]] = (stage, due);
            }
            return r;
        }

        /// <summary>Reads intervals and the daily cap from progression.json's "review" block.</summary>
        public static (int[] intervals, int maxPerDay) SettingsFromProgression(string json)
        {
            var o = MiniJson.Obj(MiniJson.Parse(json));
            if (!(o.Get("review") is Dictionary<string, object?> rv)) return (new[] { 1, 3, 7, 14 }, 3);
            var list = new List<int>();
            if (rv.Get("intervalsDays") is List<object?> iv) foreach (var x in iv) list.Add((int)MiniJson.Num(x));
            return (list.Count > 0 ? list.ToArray() : new[] { 1, 3, 7, 14 }, (int)LessonDef.N(rv, "maxPerDay", 3));
        }
    }

    /// <summary>Which lessons and scenarios practise each rule (content/lessons/gb/rule_index.json).</summary>
    public sealed class RuleIndex
    {
        public sealed class Entry
        {
            public string LessonId = "", ScenarioId = "", Title = "";
        }

        private readonly Dictionary<string, List<Entry>> _rules = new Dictionary<string, List<Entry>>();

        public static RuleIndex FromJson(string json)
        {
            var idx = new RuleIndex();
            var o = MiniJson.Obj(MiniJson.Parse(json));
            if (o.Get("rules") is Dictionary<string, object?> rules)
                foreach (var kv in rules)
                {
                    var list = new List<Entry>();
                    if (kv.Value is List<object?> arr)
                        foreach (var x in arr)
                        {
                            var e = MiniJson.Obj(x);
                            list.Add(new Entry { LessonId = LessonDef.S(e, "lessonId"), ScenarioId = LessonDef.S(e, "scenarioId"), Title = LessonDef.S(e, "title") });
                        }
                    idx._rules[kv.Key] = list;
                }
            return idx;
        }

        public IReadOnlyList<Entry> For(string ruleId) => _rules.TryGetValue(ruleId, out var l) ? l : (IReadOnlyList<Entry>)Array.Empty<Entry>();

        /// <summary>The first short practice drive for a rule that the game can play (never the 35-minute mock test).</summary>
        public Entry? PracticeFor(string ruleId, Func<Entry, bool> playable)
        {
            foreach (var e in For(ruleId))
                if (!e.LessonId.Contains("mock") && playable(e)) return e;
            return null;
        }
    }
}
