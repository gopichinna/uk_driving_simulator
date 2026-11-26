using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>Anything that can say what a signal group shows at a time.</summary>
    public interface ISignalPlan
    {
        LightState State(int group, double time);
    }

    /// <summary>
    /// Many independent junction controllers behind one set of global group numbers (road packs: one plan per
    /// signalised node). <see cref="Add"/> returns the first global group of the new plan.
    /// </summary>
    public sealed class SignalNetwork : ISignalPlan
    {
        private readonly List<(int first, int count, TrafficSignals plan)> _plans = new List<(int, int, TrafficSignals)>();

        public int GroupCount { get; private set; }

        public int Add(TrafficSignals plan, int groups)
        {
            int first = GroupCount;
            _plans.Add((first, groups, plan));
            GroupCount += groups;
            return first;
        }

        public LightState State(int group, double time)
        {
            foreach (var (first, count, plan) in _plans)
                if (group >= first && group < first + count) return plan.State(group - first, time);
            return LightState.Green;
        }
    }

    /// <summary>What a signal head shows. GB sequence: Red → RedAmber → Green → Amber → Red.</summary>
    public enum LightState { Red, RedAmber, Green, Amber }

    /// <summary>
    /// Fixed-time signal controller for one junction. Each stage gives green to a set of signal groups;
    /// stages run in order and the cycle repeats. Between stages: amber for the old groups, then an all-red
    /// period during whose end the next groups show red and amber together. Deterministic: the state is a pure
    /// function of time. GB: Highway Code "Light signals controlling traffic".
    /// </summary>
    public sealed class TrafficSignals : ISignalPlan
    {
        private readonly int[][] _stages;
        private readonly double[] _green;
        private readonly double _cycle;

        /// <param name="stages">Signal groups that are green in each stage.</param>
        /// <param name="greenS">Green time per stage.</param>
        public TrafficSignals(int[][] stages, double[] greenS, double amberS = 3.0, double allRedS = 2.0, double redAmberS = 2.0, double offsetS = 0)
        {
            if (stages.Length == 0 || stages.Length != greenS.Length) throw new ArgumentException("One green time per stage.");
            if (redAmberS > allRedS) throw new ArgumentException("Red and amber must fit inside the all-red time.");
            _stages = stages;
            _green = greenS;
            AmberS = amberS;
            AllRedS = allRedS;
            RedAmberS = redAmberS;
            OffsetS = offsetS;
            double c = 0;
            foreach (var g in greenS) c += g + amberS + allRedS;
            _cycle = c;
        }

        public double AmberS { get; }
        public double AllRedS { get; }
        public double RedAmberS { get; }
        public double OffsetS { get; }
        public double CycleS => _cycle;
        public int StageCount => _stages.Length;

        public LightState State(int group, double time)
        {
            double t = (time + OffsetS) % _cycle;
            if (t < 0) t += _cycle;
            double start = 0;
            for (int k = 0; k < _stages.Length; k++)
            {
                double greenEnd = start + _green[k], amberEnd = greenEnd + AmberS, stageEnd = amberEnd + AllRedS;
                if (t < stageEnd)
                {
                    if (Contains(_stages[k], group))
                    {
                        if (t < greenEnd) return LightState.Green;
                        if (t < amberEnd) return LightState.Amber;
                    }
                    int next = (k + 1) % _stages.Length;
                    // Red and amber for the groups whose green is about to start (unless they're green already).
                    if (t >= stageEnd - RedAmberS && Contains(_stages[next], group) && !Contains(_stages[k], group))
                        return LightState.RedAmber;
                    return LightState.Red;
                }
                start = stageEnd;
            }
            return LightState.Red;
        }

        /// <summary>Seconds until the given group next turns green (0 if green now).</summary>
        public double TimeToGreen(int group, double time)
        {
            if (State(group, time) == LightState.Green) return 0;
            for (double dt = 0.1; dt <= _cycle + 0.1; dt += 0.1)
                if (State(group, time + dt) == LightState.Green) return dt;
            return double.PositiveInfinity;
        }

        private static bool Contains(int[] set, int g)
        {
            foreach (var x in set) if (x == g) return true;
            return false;
        }

        /// <summary>Copies every group's state into a list indexed by group (for views and rules).</summary>
        public void States(double time, int groupCount, List<LightState> into)
        {
            into.Clear();
            for (int g = 0; g < groupCount; g++) into.Add(State(g, time));
        }
    }
}
