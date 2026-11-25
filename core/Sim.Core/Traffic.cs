using System;
using System.Collections.Generic;

namespace LToLocal.Sim
{
    /// <summary>A straight lane along the Z axis. Direction +1 = northbound (+Z), -1 = southbound.</summary>
    public readonly struct StraightLane
    {
        public StraightLane(double centerX, int direction, bool isNearside)
        {
            CenterX = centerX;
            Direction = direction >= 0 ? 1 : -1;
            IsNearside = isNearside;
        }

        public double CenterX { get; }
        public int Direction { get; }
        /// <summary>True for the left-hand (lane 1) lane of its carriageway: the "keep left" lane in the UK.</summary>
        public bool IsNearside { get; }
    }

    public sealed class TrafficCar
    {
        public int Id;
        public int Lane;
        public int TargetLane;
        public double X;
        public double Z;
        public double SpeedMps;
        public double DesiredSpeedMps;
        /// <summary>-1 left, 0 off, +1 right.</summary>
        public int Indicator;
        public double SignalTimer;
        public int ColourIndex;
        public bool Active;
        public bool IsCyclist;
        public double LengthM = 4.3;
        /// <summary>Acceleration applied on the last step (negative = braking). Used by rules to spot forced braking.</summary>
        public double LastAccelMps2;

        public bool ChangingLane => TargetLane != Lane;
    }

    public sealed class TrafficParams
    {
        // Intelligent Driver Model (IDM)
        public double MinGapM = 2.0;
        /// <summary>Following gap in seconds. The Highway Code asks for at least 2 s (rule 126); AI keeps 3 s.</summary>
        public double TimeHeadwayS = 3.0;
        public double MaxAccelMps2 = 1.5;
        public double ComfortDecelMps2 = 2.0;
        public double CarLengthM = 4.3;

        // Lane changes
        public double SignalBeforeMoveS = 2.0;
        public double LateralSpeedMps = 1.1;
        public double LaneHalfWidthM = 1.6;

        // Population
        public double DesiredSpeedMinMps = 16.5; // ~37 mph
        public double DesiredSpeedMaxMps = 19.5; // ~44 mph
        public double DespawnBehindM = 320;
        public double DespawnAheadM = 520;
        public int[] CarsPerLane = Array.Empty<int>();
        public int ColourCount = 6;

        // Cyclists (they ride in the nearside lane and never change lanes)
        public double CyclistChance = 0.10;
        public double CyclistLengthM = 1.8;
        public double CyclistSpeedMinMps = 5.0;  // ~11 mph
        public double CyclistSpeedMaxMps = 7.0;  // ~16 mph
        public double PlayerLengthM = 4.3;
    }

    /// <summary>
    /// Prototype traffic on straight lanes: IDM car-following, simple MOBIL-style overtaking that
    /// signals first and returns to the nearside lane (keep left), and spawning around the player.
    /// Deterministic for a given seed. Replaced by lane-graph AI in milestone P3 (docs/04 §8).
    /// </summary>
    public sealed class TrafficSim
    {
        private readonly StraightLane[] _lanes;
        private readonly TrafficParams _p;
        private readonly DeterministicRandom _rng;
        private readonly List<TrafficCar> _cars = new List<TrafficCar>();
        private int _nextId;

        public TrafficSim(IReadOnlyList<StraightLane> lanes, TrafficParams p, ulong seed)
        {
            _lanes = new StraightLane[lanes.Count];
            for (int i = 0; i < lanes.Count; i++) _lanes[i] = lanes[i];
            _p = p;
            _rng = new DeterministicRandom(seed);
        }

        public IReadOnlyList<TrafficCar> Cars => _cars;
        public IReadOnlyList<StraightLane> Lanes => _lanes;

        /// <summary>Removes all cars and fills each lane around the player.</summary>
        public void Reset(double playerZ)
        {
            _cars.Clear();
            for (int lane = 0; lane < _lanes.Length; lane++)
            {
                int count = lane < _p.CarsPerLane.Length ? _p.CarsPerLane[lane] : 0;
                for (int i = 0; i < count; i++)
                {
                    double z = playerZ + _rng.Range(-_p.DespawnBehindM * 0.7, _p.DespawnAheadM * 0.8);
                    if (Math.Abs(z - playerZ) < 25) z -= 60; // never on top of the player
                    if (IsSpaceFree(lane, z, 30)) AddCar(lane, z, isCyclist: RollCyclist(lane));
                }
            }
        }

        /// <summary>Spawns one car at an exact place. Mainly for tests and scripted scenarios.</summary>
        public TrafficCar AddCar(int lane, double z, double? speed = null, double? desired = null, bool isCyclist = false)
        {
            double d = desired ?? (isCyclist
                ? _rng.Range(_p.CyclistSpeedMinMps, _p.CyclistSpeedMaxMps)
                : _rng.Range(_p.DesiredSpeedMinMps, _p.DesiredSpeedMaxMps));
            var car = new TrafficCar
            {
                Id = _nextId++,
                Lane = lane,
                TargetLane = lane,
                X = _lanes[lane].CenterX,
                Z = z,
                DesiredSpeedMps = d,
                SpeedMps = speed ?? d,
                ColourIndex = _rng.Range(0, Math.Max(1, _p.ColourCount)),
                Active = true,
                IsCyclist = isCyclist,
                LengthM = isCyclist ? _p.CyclistLengthM : _p.CarLengthM,
            };
            _cars.Add(car);
            return car;
        }

        public void Step(double dt, in CarState player, bool respawn = true)
        {
            double playerForward = player.SpeedMps * Math.Cos(player.Heading);

            // 1. Longitudinal (IDM), computed from the current state for every car, then applied.
            var accel = new double[_cars.Count];
            for (int i = 0; i < _cars.Count; i++)
                accel[i] = IdmAccel(_cars[i], player, playerForward);

            for (int i = 0; i < _cars.Count; i++)
            {
                var c = _cars[i];
                double newSpeed = Math.Max(0, c.SpeedMps + accel[i] * dt);
                c.LastAccelMps2 = (newSpeed - c.SpeedMps) / dt;
                c.SpeedMps = newSpeed;
                c.Z += _lanes[c.Lane].Direction * c.SpeedMps * dt;
            }

            // 2. Lane-change decisions and lateral movement
            foreach (var c in _cars) UpdateLaneChange(c, dt, player, playerForward);

            // 3. Keep the population around the player
            if (respawn) Respawn(player.Z);
        }

        // ---------- longitudinal ----------

        private double IdmAccel(TrafficCar c, in CarState player, double playerForward)
        {
            double gap = double.PositiveInfinity;
            double leaderSpeed = c.SpeedMps;

            foreach (int lane in OccupiedLanes(c))
            {
                FindLeader(lane, c, player, playerForward, out double g, out double v);
                if (g < gap) { gap = g; leaderSpeed = v; }
            }

            double v0 = c.DesiredSpeedMps;
            double free = 1 - Math.Pow(c.SpeedMps / v0, 4);
            if (double.IsPositiveInfinity(gap)) return _p.MaxAccelMps2 * free;

            double dv = c.SpeedMps - leaderSpeed;
            double sStar = _p.MinGapM + Math.Max(0, c.SpeedMps * _p.TimeHeadwayS
                + c.SpeedMps * dv / (2 * Math.Sqrt(_p.MaxAccelMps2 * _p.ComfortDecelMps2)));
            double s = Math.Max(0.1, gap);
            double a = _p.MaxAccelMps2 * (free - (sStar / s) * (sStar / s));
            return Math.Max(-9.0, a); // emergency braking limit
        }

        private IEnumerable<int> OccupiedLanes(TrafficCar c)
        {
            yield return c.Lane;
            if (c.ChangingLane && c.SignalTimer <= 0) yield return c.TargetLane;
        }

        /// <summary>Gap (bumper to bumper) and speed of the nearest vehicle ahead in a lane, including the player.</summary>
        private void FindLeader(int lane, TrafficCar self, in CarState player, double playerForward, out double gap, out double speed)
        {
            int dir = _lanes[lane].Direction;
            gap = double.PositiveInfinity;
            speed = self.SpeedMps;
            double selfPos = dir * self.Z;

            foreach (var o in _cars)
            {
                if (o == self || !InLane(o, lane)) continue;
                double half = (self.LengthM + o.LengthM) / 2;
                double d = dir * o.Z - selfPos - half;
                if (d > -half && d < gap) { gap = Math.Max(0, d); speed = o.SpeedMps; }
            }

            if (PlayerInLane(player, lane))
            {
                double half = (self.LengthM + _p.PlayerLengthM) / 2;
                double d = dir * player.Z - selfPos - half;
                if (d > -half && d < gap) { gap = Math.Max(0, d); speed = Math.Max(0, dir * playerForward); }
            }
        }

        private bool InLane(TrafficCar o, int lane) =>
            Math.Abs(o.X - _lanes[lane].CenterX) < _p.LaneHalfWidthM + 0.4 || o.Lane == lane ||
            (o.ChangingLane && o.TargetLane == lane && o.SignalTimer <= 0);

        public bool PlayerInLane(in CarState player, int lane) =>
            Math.Abs(player.X - _lanes[lane].CenterX) < _p.LaneHalfWidthM + 0.6;

        // ---------- lane changes ----------

        private void UpdateLaneChange(TrafficCar c, double dt, in CarState player, double playerForward)
        {
            if (c.ChangingLane)
            {
                if (c.SignalTimer > 0)
                {
                    c.SignalTimer -= dt;
                    if (c.SignalTimer <= 0 && !IsLaneChangeSafe(c, c.TargetLane, player, playerForward))
                    {
                        c.TargetLane = c.Lane; // abort: someone is there now
                        c.Indicator = 0;
                    }
                    return;
                }

                double targetX = _lanes[c.TargetLane].CenterX;
                double step = _p.LateralSpeedMps * dt;
                c.X = Math.Abs(targetX - c.X) <= step ? targetX : c.X + Math.Sign(targetX - c.X) * step;
                if (c.X == targetX)
                {
                    c.Lane = c.TargetLane;
                    c.Indicator = 0;
                }
                return;
            }

            if (c.IsCyclist) return;
            int other = AdjacentLaneSameDirection(c.Lane);
            if (other < 0) return;

            FindLeader(c.Lane, c, player, playerForward, out double gapHere, out double speedHere);
            bool inNearside = _lanes[c.Lane].IsNearside;

            if (inNearside)
            {
                // Overtake a slower vehicle ahead
                bool heldUp = gapHere < 45 && speedHere < c.DesiredSpeedMps - 2.5;
                if (heldUp && IsLaneChangeSafe(c, other, player, playerForward)) BeginLaneChange(c, other);
            }
            else
            {
                // Keep left: go back to the nearside lane when there's room (Highway Code rules 137/264)
                FindLeader(other, c, player, playerForward, out double gapLeft, out double speedLeft);
                bool roomAhead = gapLeft > 40 && (speedLeft >= c.SpeedMps - 1.0 || gapLeft > 90);
                if (roomAhead && IsLaneChangeSafe(c, other, player, playerForward)) BeginLaneChange(c, other);
            }
        }

        private void BeginLaneChange(TrafficCar c, int target)
        {
            c.TargetLane = target;
            c.SignalTimer = _p.SignalBeforeMoveS;
            // Indicator side depends on which way the car moves *relative to its own direction of travel*.
            double lateral = (_lanes[target].CenterX - _lanes[c.Lane].CenterX) * _lanes[c.Lane].Direction;
            c.Indicator = lateral > 0 ? 1 : -1;
        }

        private int AdjacentLaneSameDirection(int lane)
        {
            int best = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < _lanes.Length; i++)
            {
                if (i == lane || _lanes[i].Direction != _lanes[lane].Direction) continue;
                double d = Math.Abs(_lanes[i].CenterX - _lanes[lane].CenterX);
                if (d < bestDist && d < 5) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>Enough space in the target lane ahead and behind (allowing for closing speed)?</summary>
        private bool IsLaneChangeSafe(TrafficCar c, int target, in CarState player, double playerForward)
        {
            int dir = _lanes[target].Direction;
            double selfPos = dir * c.Z;

            bool Check(double otherPos, double otherSpeed, double otherLength)
            {
                double d = otherPos - selfPos;
                double half = (c.LengthM + otherLength) / 2;
                if (d >= 0) return d - half > 12 + Math.Max(0, c.SpeedMps - otherSpeed) * 2; // ahead
                double behind = -d - half;
                return behind > 10 + Math.Max(0, otherSpeed - c.SpeedMps) * 3;                          // behind
            }

            foreach (var o in _cars)
            {
                if (o == c || !InLane(o, target)) continue;
                if (!Check(dir * o.Z, o.SpeedMps, o.LengthM)) return false;
            }

            if (PlayerInLane(player, target) && !Check(dir * player.Z, Math.Max(0, dir * playerForward), _p.PlayerLengthM)) return false;
            return true;
        }

        // ---------- population ----------

        private bool RollCyclist(int lane) => _lanes[lane].IsNearside && _rng.NextDouble() < _p.CyclistChance;

        private bool IsSpaceFree(int lane, double z, double clearance)
        {
            foreach (var o in _cars)
                if (InLane(o, lane) && Math.Abs(o.Z - z) < clearance) return false;
            return true;
        }

        private void Respawn(double playerZ)
        {
            _cars.RemoveAll(c =>
            {
                double rel = (c.Z - playerZ) * _lanes[c.Lane].Direction;
                return rel < -_p.DespawnBehindM || rel > _p.DespawnAheadM;
            });

            for (int lane = 0; lane < _lanes.Length; lane++)
            {
                int want = lane < _p.CarsPerLane.Length ? _p.CarsPerLane[lane] : 0;
                int have = 0;
                foreach (var c in _cars) if (c.Lane == lane) have++;
                if (have >= want) continue;

                int dir = _lanes[lane].Direction;
                // Same-direction traffic mostly arrives from behind (so it shows up in the mirrors);
                // oncoming traffic arrives from ahead.
                bool behind = dir > 0 && _rng.NextDouble() < 0.7;
                double offset = behind
                    ? -_rng.Range(_p.DespawnBehindM * 0.6, _p.DespawnBehindM * 0.9)
                    : _rng.Range(_p.DespawnAheadM * 0.65, _p.DespawnAheadM * 0.95);
                double z = playerZ + dir * offset;
                if (IsSpaceFree(lane, z, 40)) AddCar(lane, z, isCyclist: RollCyclist(lane));
            }
        }
    }
}
