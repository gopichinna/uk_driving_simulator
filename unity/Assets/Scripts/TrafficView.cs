using System.Collections.Generic;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>One AI vehicle as the view needs it, independent of which traffic model produced it.</summary>
    public struct VehicleSnapshot
    {
        public int Id;
        public double X, Z;
        /// <summary>Radians, 0 = north (+Z), positive = clockwise.</summary>
        public double Heading;
        /// <summary>-1 left, 0 off, +1 right.</summary>
        public int Indicator;
        public int Colour;
        public bool IsCyclist;
    }

    /// <summary>
    /// Shows AI vehicles using a pool of pre-built low-poly cars (children of this object).
    /// Pool cars are grouped by colour via their name suffix "_c{n}". Indicator lamps are children
    /// named "IndL*" / "IndR*" and blink while a car signals. Interpolates between simulation steps.
    /// </summary>
    public sealed class TrafficView : MonoBehaviour
    {
        private readonly Dictionary<int, Transform> _assigned = new Dictionary<int, Transform>();
        private readonly Dictionary<int, VehicleSnapshot> _prev = new Dictionary<int, VehicleSnapshot>();
        private readonly List<Transform>[] _freeByColour = new List<Transform>[8];
        private readonly List<Transform> _freeCyclists = new List<Transform>();
        private readonly Dictionary<Transform, (GameObject[] left, GameObject[] right)> _lamps = new Dictionary<Transform, (GameObject[], GameObject[])>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _toRelease = new List<int>();

        private void Awake()
        {
            for (int i = 0; i < _freeByColour.Length; i++) _freeByColour[i] = new List<Transform>();
            foreach (Transform t in transform)
            {
                t.gameObject.SetActive(false);
                if (t.name.StartsWith("Cyclist")) { _freeCyclists.Add(t); _lamps[t] = (new GameObject[0], new GameObject[0]); continue; }
                _freeByColour[ColourOf(t)].Add(t);

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
        public void CapturePrevious(List<VehicleSnapshot> vehicles)
        {
            _prev.Clear();
            foreach (var v in vehicles) _prev[v.Id] = v;
        }

        public void Clear()
        {
            foreach (var kv in _assigned) Release(kv.Value);
            _assigned.Clear();
            _prev.Clear();
        }

        public void Render(List<VehicleSnapshot> vehicles, float alpha, bool blinkOn)
        {
            _seen.Clear();
            foreach (var v in vehicles)
            {
                _seen.Add(v.Id);
                if (!_assigned.TryGetValue(v.Id, out var t))
                {
                    t = v.IsCyclist ? AcquireCyclist() : Acquire(v.Colour);
                    if (t == null) continue;
                    _assigned[v.Id] = t;
                }

                var p = _prev.TryGetValue(v.Id, out var prev) ? prev : v;
                float x = Mathf.Lerp((float)p.X, (float)v.X, alpha);
                float z = Mathf.Lerp((float)p.Z, (float)v.Z, alpha);
                float yaw = Mathf.LerpAngle((float)(p.Heading * Mathf.Rad2Deg), (float)(v.Heading * Mathf.Rad2Deg), alpha);
                t.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));

                var lamps = _lamps[t];
                SetLamps(lamps.left, v.Indicator < 0 && blinkOn);
                SetLamps(lamps.right, v.Indicator > 0 && blinkOn);
            }

            _toRelease.Clear();
            foreach (var kv in _assigned) if (!_seen.Contains(kv.Key)) _toRelease.Add(kv.Key);
            foreach (int id in _toRelease) { Release(_assigned[id]); _assigned.Remove(id); }
        }

        private static void SetLamps(GameObject[] lamps, bool on)
        {
            foreach (var l in lamps) if (l.activeSelf != on) l.SetActive(on);
        }

        private int ColourOf(Transform t)
        {
            int colour = 0;
            int idx = t.name.LastIndexOf("_c");
            if (idx >= 0) int.TryParse(t.name.Substring(idx + 2), out colour);
            return Mathf.Clamp(colour, 0, _freeByColour.Length - 1);
        }

        private Transform Acquire(int colour)
        {
            for (int k = 0; k < _freeByColour.Length; k++)
            {
                var list = _freeByColour[((colour % _freeByColour.Length) + k) % _freeByColour.Length];
                if (list.Count == 0) continue;
                var t = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                t.gameObject.SetActive(true);
                return t;
            }
            return null;
        }

        private Transform AcquireCyclist()
        {
            if (_freeCyclists.Count == 0) return null;
            var t = _freeCyclists[_freeCyclists.Count - 1];
            _freeCyclists.RemoveAt(_freeCyclists.Count - 1);
            t.gameObject.SetActive(true);
            return t;
        }

        private void Release(Transform t)
        {
            t.gameObject.SetActive(false);
            if (t.name.StartsWith("Cyclist")) _freeCyclists.Add(t);
            else _freeByColour[ColourOf(t)].Add(t);
        }
    }
}
