using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    public enum ActorKind : byte { Car = 0, Cyclist = 1, Pedestrian = 2 }

    /// <summary>Where one other road user was at a recorded moment.</summary>
    public struct ActorPose
    {
        public int Id;
        public ActorKind Kind;
        public float X, Z, Heading;
        public sbyte Indicator;
        public byte Colour;
    }

    /// <summary>One recorded moment of a drive: the player, everyone else and the traffic lights.</summary>
    public sealed class RecordedFrame
    {
        public double Time;
        public CarState Player;
        public int PlayerIndicator;
        public ActorPose[] Actors = Array.Empty<ActorPose>();
        public LightState[] Lights = Array.Empty<LightState>();
    }

    /// <summary>
    /// Records a drive as poses at a fixed rate (10 Hz by default) so any moment can be replayed exactly,
    /// independent of how the traffic was simulated. Keeps the most recent <see cref="MaxSeconds"/>.
    /// </summary>
    public sealed class DriveRecorder
    {
        private readonly List<RecordedFrame> _frames = new List<RecordedFrame>();
        private double _lastTime = double.NegativeInfinity;

        public double IntervalS { get; set; } = 0.1;
        public double MaxSeconds { get; set; } = 900;
        public IReadOnlyList<RecordedFrame> Frames => _frames;
        public double StartTime => _frames.Count > 0 ? _frames[0].Time : 0;
        public double EndTime => _frames.Count > 0 ? _frames[_frames.Count - 1].Time : 0;

        public void Clear()
        {
            _frames.Clear();
            _lastTime = double.NegativeInfinity;
        }

        /// <summary>Call every simulation step; only keeps a frame every <see cref="IntervalS"/>.</summary>
        public bool Record(double time, in CarState player, int indicator, IReadOnlyList<ActorPose> actors, IReadOnlyList<LightState>? lights = null)
        {
            if (time - _lastTime < IntervalS - 1e-9) return false;
            _lastTime = time;
            var f = new RecordedFrame { Time = time, Player = player, PlayerIndicator = indicator, Actors = new ActorPose[actors.Count] };
            for (int i = 0; i < actors.Count; i++) f.Actors[i] = actors[i];
            if (lights != null)
            {
                f.Lights = new LightState[lights.Count];
                for (int i = 0; i < lights.Count; i++) f.Lights[i] = lights[i];
            }
            _frames.Add(f);
            int drop = 0;
            while (drop < _frames.Count && _frames[_frames.Count - 1].Time - _frames[drop].Time > MaxSeconds) drop++;
            if (drop > 0) _frames.RemoveRange(0, drop);
            return true;
        }

        /// <summary>The moment at a time, interpolated between recorded frames (actors matched by id).</summary>
        public RecordedFrame? SampleAt(double time)
        {
            if (_frames.Count == 0) return null;
            if (time <= _frames[0].Time) return _frames[0];
            if (time >= _frames[_frames.Count - 1].Time) return _frames[_frames.Count - 1];
            int lo = 0, hi = _frames.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_frames[mid].Time <= time) lo = mid; else hi = mid;
            }
            RecordedFrame a = _frames[lo], b = _frames[hi];
            double t = (time - a.Time) / Math.Max(1e-9, b.Time - a.Time);

            var player = a.Player;
            player.X = Lerp(a.Player.X, b.Player.X, t);
            player.Z = Lerp(a.Player.Z, b.Player.Z, t);
            player.Heading = LerpAngle(a.Player.Heading, b.Player.Heading, t);
            player.SpeedMps = Lerp(a.Player.SpeedMps, b.Player.SpeedMps, t);

            var byId = new Dictionary<int, ActorPose>(b.Actors.Length);
            foreach (var p in b.Actors) byId[p.Id] = p;
            var actors = new List<ActorPose>(a.Actors.Length);
            foreach (var p in a.Actors)
            {
                if (!byId.TryGetValue(p.Id, out var q)) { actors.Add(p); continue; }
                var r = p;
                r.X = (float)Lerp(p.X, q.X, t);
                r.Z = (float)Lerp(p.Z, q.Z, t);
                r.Heading = (float)LerpAngle(p.Heading, q.Heading, t);
                actors.Add(r);
            }
            return new RecordedFrame { Time = time, Player = player, PlayerIndicator = a.PlayerIndicator, Actors = actors.ToArray(), Lights = a.Lights };
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static double LerpAngle(double a, double b, double t) => a + LaneGraph.Angle(b - a) * t;
    }
}
