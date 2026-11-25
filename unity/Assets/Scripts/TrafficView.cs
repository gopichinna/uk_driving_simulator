using System.Collections.Generic;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Shows <see cref="TrafficSim"/> cars using a pool of pre-built low-poly cars (children of this object).
    /// Pool cars are grouped by colour index via their name suffix "_c{n}". Indicator lamps are children
    /// named "IndL*" / "IndR*" and blink while a car signals.
    /// </summary>
    public sealed class TrafficView : MonoBehaviour
    {
        private readonly Dictionary<int, Transform> _assigned = new Dictionary<int, Transform>();
        private readonly Dictionary<int, (double x, double z)> _prev = new Dictionary<int, (double, double)>();
        private readonly Dictionary<int, (double x, double z)> _curr = new Dictionary<int, (double, double)>();
        private readonly List<Transform>[] _freeByColour = new List<Transform>[8];
        private readonly Dictionary<Transform, (GameObject[] left, GameObject[] right)> _lamps = new Dictionary<Transform, (GameObject[], GameObject[])>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _toRelease = new List<int>();

        private void Awake()
        {
            for (int i = 0; i < _freeByColour.Length; i++) _freeByColour[i] = new List<Transform>();
            foreach (Transform t in transform)
            {
                int colour = 0;
                int idx = t.name.LastIndexOf("_c");
                if (idx >= 0) int.TryParse(t.name.Substring(idx + 2), out colour);
                _freeByColour[Mathf.Clamp(colour, 0, _freeByColour.Length - 1)].Add(t);
                t.gameObject.SetActive(false);

                var left = new List<GameObject>();
                var right = new List<GameObject>();
                foreach (Transform c in t)
                {
                    if (c.name.StartsWith("IndL")) left.Add(c.gameObject);
                    if (c.name.StartsWith("IndR")) right.Add(c.gameObject);
                }
                _lamps[t] = (left.ToArray(), right.ToArray());
            }
        }

        /// <summary>Call before each simulation step so the view can interpolate between steps.</summary>
        public void CapturePrevious(TrafficSim sim)
        {
            _prev.Clear();
            foreach (var c in sim.Cars) _prev[c.Id] = (c.X, c.Z);
        }

        public void Clear()
        {
            foreach (var kv in _assigned) Release(kv.Value);
            _assigned.Clear();
            _prev.Clear();
        }

        public void Render(TrafficSim sim, float alpha, bool blinkOn)
        {
            _seen.Clear();
            _curr.Clear();
            foreach (var c in sim.Cars)
            {
                _seen.Add(c.Id);
                _curr[c.Id] = (c.X, c.Z);
                if (!_assigned.TryGetValue(c.Id, out var t))
                {
                    t = Acquire(c.ColourIndex);
                    if (t == null) continue;
                    _assigned[c.Id] = t;
                }

                var (px, pz) = _prev.TryGetValue(c.Id, out var p) ? p : (c.X, c.Z);
                float x = Mathf.Lerp((float)px, (float)c.X, alpha);
                float z = Mathf.Lerp((float)pz, (float)c.Z, alpha);
                int dir = sim.Lanes[c.Lane].Direction;

                // Face the direction of travel, turning slightly while changing lane.
                float yaw = dir > 0 ? 0f : 180f;
                double dx = c.X - px, dz = c.Z - pz;
                if (dx * dx + dz * dz > 1e-6) yaw = Mathf.Atan2((float)dx, (float)dz) * Mathf.Rad2Deg;
                t.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));

                var lamps = _lamps[t];
                SetLamps(lamps.left, c.Indicator < 0 && blinkOn);
                SetLamps(lamps.right, c.Indicator > 0 && blinkOn);
            }

            _toRelease.Clear();
            foreach (var kv in _assigned) if (!_seen.Contains(kv.Key)) _toRelease.Add(kv.Key);
            foreach (int id in _toRelease) { Release(_assigned[id]); _assigned.Remove(id); }
        }

        private static void SetLamps(GameObject[] lamps, bool on)
        {
            foreach (var l in lamps) if (l.activeSelf != on) l.SetActive(on);
        }

        private Transform Acquire(int colour)
        {
            for (int k = 0; k < _freeByColour.Length; k++)
            {
                var list = _freeByColour[(colour + k) % _freeByColour.Length];
                if (list.Count == 0) continue;
                var t = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                t.gameObject.SetActive(true);
                return t;
            }
            return null;
        }

        private void Release(Transform t)
        {
            t.gameObject.SetActive(false);
            int colour = 0;
            int idx = t.name.LastIndexOf("_c");
            if (idx >= 0) int.TryParse(t.name.Substring(idx + 2), out colour);
            _freeByColour[Mathf.Clamp(colour, 0, _freeByColour.Length - 1)].Add(t);
        }
    }
}
