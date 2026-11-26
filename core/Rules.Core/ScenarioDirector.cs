using System;
using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Rules
{
    /// <summary>
    /// What a course can do for a scenario. Names are the scenario.v0 vocabulary: anchors (e.g. "zebra"),
    /// crossings ("zebra", "mill_lane_mouth", "church_st_mouth") and junctions ("tjunction_north", "tjunction_south").
    /// Courses do what they support and ignore the rest.
    /// </summary>
    public interface IScenarioWorld
    {
        /// <summary>Where a named anchor, crossing or junction is.</summary>
        bool TryLocate(string name, out Vec2 position, out double heading);
        void SetTrafficDensity(double value01);
        void PedestrianWaits(string crossing);
        void PedestrianStepsOut(string crossing);
        void CarEmerges(string junction, double gapS, in CarState player);
        void ParkedCar(string anchor, double offsetM);
        void CyclistAhead(string anchor, double offsetM, double speedMph);
        /// <summary>Turns the lights facing the player amber now; false if there are none ahead.</summary>
        bool LightsTurnAmber(in CarState player);
        /// <summary>Roundabouts: 0 while still on it or approaching, else the exit number the player left by.</summary>
        int ExitTaken(in CarState player);

        // Motorways (scenario.v0 R16 events). Courses without motorways keep the defaults.
        /// <summary>The next gantry ahead shows a red X over a lane (0 = left). False if there's no gantry ahead.</summary>
        bool RedX(int lane, in CarState player) => false;
        /// <summary>The next gantry ahead shows a lower limit in a red ring.</summary>
        bool VariableLimit(int mph, in CarState player) => false;
        /// <summary>A slower vehicle appears ahead in a lane (0 = left).</summary>
        void SlowVehicleAhead(int lane, double speedMph, double offsetM, in CarState player) { }
        /// <summary>Is the car stopped somewhere safe after a breakdown (hard shoulder or emergency area)?</summary>
        bool SafeStopPlace(in CarState player) => false;
    }

    /// <summary>
    /// Plays one scripted scenario: sets it up, fires each event when the player comes within its distance, and
    /// decides when it is over (finish point reached, the right exit taken, time driven, or time up).
    /// Engine-free and deterministic; the course does the actual spawning.
    /// </summary>
    public sealed class ScenarioDirector
    {
        private readonly ScenarioDef _def;
        private readonly bool[] _fired;
        private double _elapsed, _travelled;
        private Vec2 _startPos, _lastPos;

        public ScenarioDirector(ScenarioDef def)
        {
            _def = def;
            _fired = new bool[def.Events.Count];
        }

        public ScenarioDef Definition => _def;
        public bool Finished { get; private set; }
        public bool Completed { get; private set; }
        public string Note { get; private set; } = "";
        public double Elapsed => _elapsed;
        public double TimeLeft => Math.Max(0, (IsIndependentDriving ? IndependentMinutes * 60 : _def.DurationS) - _elapsed);
        /// <summary>The exit the player is asked to take (0 if none).</summary>
        public int RequiredExit { get; private set; }
        /// <summary>Independent driving (the mock test): minutes to drive, "satnav" | "signs" | "mixed", and the instruction shown.</summary>
        public double IndependentMinutes { get; private set; }
        public string IndependentMode { get; private set; } = "";
        public string IndependentInstruction { get; private set; } = "";
        /// <summary>"Follow the signs" destination (a POI id the course can locate), or empty.</summary>
        public string IndependentDestination { get; private set; } = "";
        public bool IsIndependentDriving => IndependentMinutes > 0;
        /// <summary>Driving conditions for the scenario (scenario.v0 "conditions"): "day" | "dusk" | "night", "clear" | "rain" | "fog".</summary>
        public string TimeOfDay { get; private set; } = "day";
        public string Weather { get; private set; } = "clear";
        public double VisibilityM { get; private set; } = 1000;
        /// <summary>The car has lost power (breakdown event): the player should coast to a safe place and stop.</summary>
        public bool BrokenDown { get; private set; }
        private double _breakdownAtM = double.NaN, _brokeDownAt;

        public void Start(IScenarioWorld world, in CarState player)
        {
            _startPos = _lastPos = new Vec2(player.X, player.Z);
            for (int i = 0; i < _def.Events.Count; i++)
            {
                var e = _def.Events[i];
                switch (e.Type)
                {
                    case "traffic_density": world.SetTrafficDensity(e.Num("value", 0.5)); _fired[i] = true; break;
                    case "parked_car": world.ParkedCar(e.Str("anchor"), e.Num("offsetM")); _fired[i] = true; break;
                    case "cyclist_ahead": world.CyclistAhead(e.Str("anchor"), e.Num("offsetM", 40), e.Num("speedMph", 12)); _fired[i] = true; break;
                    case "take_exit": RequiredExit = (int)e.Num("exitNumber", 1); _fired[i] = true; break;
                    case "breakdown": _breakdownAtM = e.Num("atM", 200); _fired[i] = true; break;
                    case "slow_vehicle_ahead": world.SlowVehicleAhead((int)e.Num("lane"), e.Num("speedMph", 50), e.Num("offsetM", 120), player); _fired[i] = true; break;
                    case "conditions":
                        TimeOfDay = e.Str("timeOfDay") is { Length: > 0 } tod ? tod : "day";
                        Weather = e.Str("weather") is { Length: > 0 } w ? w : "clear";
                        VisibilityM = e.Num("visibilityM", 1000);
                        _fired[i] = true;
                        break;
                    case "independent_driving":
                        IndependentMinutes = e.Num("minutes", 35);
                        IndependentMode = e.Str("mode");
                        IndependentInstruction = e.Str("instruction");
                        IndependentDestination = e.Str("poiId");
                        _fired[i] = true;
                        break;
                }
            }
        }

        public void Step(double dt, IScenarioWorld world, in CarState player)
        {
            if (Finished) return;
            _elapsed += dt;
            var p = new Vec2(player.X, player.Z);
            _travelled += (p - _lastPos).Length;
            _lastPos = p;

            for (int i = 0; i < _def.Events.Count; i++)
            {
                var e = _def.Events[i];
                switch (e.Type)
                {
                    case "pedestrian_waits":
                        if (!_fired[i] && Within(world, e.Str("crossing"), p, e.Num("whenPlayerWithinM", 50))) { world.PedestrianWaits(e.Str("crossing")); _fired[i] = true; }
                        break;
                    case "pedestrian_steps_out":
                        if (!_fired[i] && Within(world, e.Str("crossing"), p, e.Num("whenPlayerWithinM", 30))) { world.PedestrianStepsOut(e.Str("crossing")); _fired[i] = true; }
                        break;
                    case "car_emerges":
                        if (!_fired[i] && Within(world, e.Str("junction"), p, e.Num("whenPlayerWithinM", 40))) { world.CarEmerges(e.Str("junction"), e.Num("gapS", 4), player); _fired[i] = true; }
                        break;
                    case "red_x":
                        if (!_fired[i] && _elapsed > 1 && world.RedX((int)e.Num("lane"), player)) _fired[i] = true;
                        break;
                    case "variable_limit":
                        if (!_fired[i] && _elapsed > 1 && world.VariableLimit((int)e.Num("mph", 50), player)) _fired[i] = true;
                        break;
                    case "lights_turn_amber":
                        if (!_fired[i] && Within(world, "crossroads_signals_line", p, e.Num("whenPlayerWithinM", 45)) && world.LightsTurnAmber(player)) _fired[i] = true;
                        break;
                    case "finish_at":
                        if (world.TryLocate(e.Str("anchor"), out var a, out _) && _elapsed > 5)
                        {
                            double need = Math.Abs(e.Num("offsetM", 50));
                            if ((p - a).Length >= need && _travelled >= need * 0.6) Finish(true, "");
                        }
                        break;
                    case "finish_after_s":
                        if (!IsIndependentDriving && !WaitingForBreakdownStop && _elapsed >= e.Num("seconds", 90)) Finish(true, "");
                        break;
                    case "take_exit":
                        int exit = world.ExitTaken(player);
                        if (exit > 0) Finish(true, exit == RequiredExit ? "" : $"You left by exit {exit}. The plan was exit {RequiredExit}.");
                        break;
                }
                if (Finished) return;
            }
            if (!double.IsNaN(_breakdownAtM) && !BrokenDown && _travelled >= _breakdownAtM) { BrokenDown = true; _brokeDownAt = _elapsed; }
            if (BrokenDown && _elapsed - _brokeDownAt > 2 && player.SpeedMps < 0.3)
            {
                bool safe = world.SafeStopPlace(player);
                Finish(safe, safe ? "You stopped safely off the live lanes."
                                  : "You stopped in a live lane. After a breakdown, get onto the hard shoulder or into an emergency area.");
                return;
            }
            if (IndependentDestination.Length > 0 && world.TryLocate(IndependentDestination, out var dest, out _) && (dest - p).Length < 35)
                Finish(true, "You reached the destination by following the signs.");
            if (Finished) return;
            double limit = IsIndependentDriving ? IndependentMinutes * 60 : _def.DurationS;
            if (_elapsed >= limit) Finish(true, IsIndependentDriving ? "" : "Time is up for this drive.");
        }

        /// <summary>Breakdown scenarios don't end on the clock while the car is still rolling to a stop.</summary>
        private bool WaitingForBreakdownStop => BrokenDown;

        private void Finish(bool completed, string note)
        {
            Finished = true;
            Completed = completed;
            Note = note;
        }

        private static bool Within(IScenarioWorld world, string name, Vec2 p, double m) =>
            world.TryLocate(name, out var pos, out _) && (pos - p).Length <= m;
    }
}
