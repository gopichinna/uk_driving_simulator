using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>Fault categories modelled on the practical driving test (docs/02 §1.2).</summary>
    public enum Severity
    {
        None = 0,      // good practice / information
        Driving = 1,   // minor
        Serious = 2,   // potentially dangerous
        Dangerous = 3, // actual danger
    }

    public enum RuleOutcome
    {
        Violation,
        GoodPractice,
    }

    /// <summary>Which side of the car, independent of country: offside = driver's side (right in the UK).</summary>
    public enum CarSide
    {
        Nearside,
        Offside,
    }

    public enum DrivingSide
    {
        Left,  // UK, India, Japan, ...: offside = right
        Right, // US, most of Europe: offside = left
    }

    public enum ObservationKind
    {
        MirrorInterior,
        MirrorOffside,
        MirrorNearside,
        ShoulderOffside,
        ShoulderNearside,
    }

    public readonly struct ObservationEvent
    {
        public ObservationEvent(ObservationKind kind, double time)
        {
            Kind = kind;
            Time = time;
        }

        public ObservationKind Kind { get; }
        public double Time { get; }
    }

    public sealed class RuleEvent
    {
        public RuleEvent(string ruleId, RuleOutcome outcome, Severity severity, double time, string message, string source)
        {
            RuleId = ruleId;
            Outcome = outcome;
            Severity = severity;
            Time = time;
            Message = message;
            Source = source;
        }

        public string RuleId { get; }
        public RuleOutcome Outcome { get; }
        public Severity Severity { get; }
        public double Time { get; }
        /// <summary>Plain-English explanation (CEFR B1), shown to the player.</summary>
        public string Message { get; }
        /// <summary>Where the rule comes from, e.g. "Highway Code rule 133".</summary>
        public string Source { get; }

        public override string ToString() => $"{Time:0.00}s {RuleId} {Outcome} {Severity}: {Message}";
    }

    /// <summary>Everything the rules need to know about one simulation tick.</summary>
    public readonly struct DriverFrame
    {
        public DriverFrame(double time, CarState ego, int indicator)
        {
            Time = time;
            Ego = ego;
            Indicator = indicator;
        }

        public double Time { get; }
        public CarState Ego { get; }
        /// <summary>-1 left, 0 off, +1 right (screen/world directions, not nearside/offside).</summary>
        public int Indicator { get; }
    }

    public static class Sides
    {
        /// <summary>Maps a world direction (-1 left, +1 right, relative to travel) to nearside/offside.</summary>
        public static CarSide FromDirection(int direction, DrivingSide drivingSide) =>
            drivingSide == DrivingSide.Left
                ? (direction > 0 ? CarSide.Offside : CarSide.Nearside)
                : (direction > 0 ? CarSide.Nearside : CarSide.Offside);
    }
}
