using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>Another road user, as the rules see them.</summary>
    public readonly struct RoadUser
    {
        public RoadUser(int id, Vec2 position, double heading, double speedMps, bool isCyclist, double lengthM = 0)
        {
            LengthM = lengthM > 0 ? lengthM : (isCyclist ? 1.8 : 4.3);
            Id = id;
            Position = position;
            Heading = heading;
            SpeedMps = speedMps;
            IsCyclist = isCyclist;
        }

        public int Id { get; }
        public Vec2 Position { get; }
        public double Heading { get; }
        public double SpeedMps { get; }
        public bool IsCyclist { get; }
        public double LengthM { get; }
    }

    /// <summary>
    /// GB.VRU.PASS_CYCLIST: leave at least 1.5 m when overtaking cyclists at speeds up to 30 mph,
    /// and more at higher speeds (Highway Code rule 163). Judged when the overtake is complete.
    /// </summary>
    public sealed class CyclistPassingRule
    {
        public const string RuleId = "GB.VRU.PASS_CYCLIST";
        private const string Source = "Highway Code rule 163";
        private const double CarHalfWidth = 0.9, BikeHalfWidth = 0.3;
        private const double AlongsideHalfLength = (4.3 + 1.8) / 2 + 0.5;

        private sealed class PassState
        {
            public double MinClearance = double.MaxValue;
            public double MaxSpeedMph;
            public bool EnteredFromBehind;
        }

        private readonly Dictionary<int, PassState> _passing = new Dictionary<int, PassState>();
        private readonly List<int> _done = new List<int>();
        private double _lastGoodTime = double.NegativeInfinity;

        public void Reset()
        {
            _passing.Clear();
            _lastGoodTime = double.NegativeInfinity;
        }

        public void OnFrame(in DriverFrame f, IReadOnlyList<RoadUser> users, List<RuleEvent> output)
        {
            var me = new Vec2(f.Ego.X, f.Ego.Z);
            _done.Clear();
            foreach (var id in _passing.Keys) _done.Add(id);

            foreach (var u in users)
            {
                if (!u.IsCyclist) continue;
                if (Math.Cos(f.Ego.Heading - u.Heading) < 0.5) continue; // not travelling the same way

                Vec2 fwd = Vec2.FromHeading(u.Heading);
                Vec2 d = me - u.Position;
                double along = d.Dot(fwd);
                double lateral = Math.Abs(d.Dot(fwd.LeftNormal));
                bool alongside = Math.Abs(along) < AlongsideHalfLength && lateral < 4.5;

                if (_passing.TryGetValue(u.Id, out var st))
                {
                    if (alongside)
                    {
                        _done.Remove(u.Id);
                        st.MinClearance = Math.Min(st.MinClearance, lateral - CarHalfWidth - BikeHalfWidth);
                        st.MaxSpeedMph = Math.Max(st.MaxSpeedMph, f.Ego.SpeedMph);
                        continue;
                    }

                    _done.Remove(u.Id);
                    _passing.Remove(u.Id);
                    if (along > 0 && st.EnteredFromBehind) Judge(f, st, output); // we came past them
                }
                else if (alongside)
                {
                    _done.Remove(u.Id);
                    _passing[u.Id] = new PassState
                    {
                        MinClearance = lateral - CarHalfWidth - BikeHalfWidth,
                        MaxSpeedMph = f.Ego.SpeedMph,
                        EnteredFromBehind = along < 0,
                    };
                }
            }

            foreach (int id in _done) _passing.Remove(id); // cyclist gone (left the road)
        }

        private void Judge(in DriverFrame f, PassState st, List<RuleEvent> output)
        {
            double required = st.MaxSpeedMph > 30 ? 2.0 : 1.5;
            double c = Math.Max(0, st.MinClearance);
            if (st.MinClearance < 1.0)
                output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Dangerous, f.Time,
                    $"Far too close: you passed the cyclist with only {c:0.0} m to spare. Wait until you can leave at least 1.5 m.", Source));
            else if (st.MinClearance < required)
                output.Add(new RuleEvent(RuleId, RuleOutcome.Violation, Severity.Serious, f.Time,
                    $"You passed the cyclist with {c:0.0} m. Leave at least 1.5 m{(required > 1.5 ? ", and more above 30 mph" : "")}.", Source));
            else if (f.Time - _lastGoodTime > 20)
            {
                _lastGoodTime = f.Time;
                output.Add(new RuleEvent(RuleId, RuleOutcome.GoodPractice, Severity.None, f.Time,
                    "Good: you gave the cyclist plenty of room.", Source));
            }
        }
    }
}
