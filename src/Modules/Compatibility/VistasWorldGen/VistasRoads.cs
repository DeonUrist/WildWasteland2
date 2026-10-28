using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MapMagic.Core;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    internal sealed class VistasRoads : IWorldGenRoads
    {
        private readonly Func<object> _network;
        private readonly Func<bool> _enabled;
        private readonly Func<bool> _spawnTrackEnabled;
        private readonly Func<object, int> _spawnTrackState;
        private readonly Func<object, object> _gate;
        private readonly Func<object, HashSet<string>> _requested, _completed;
        private readonly Func<object, bool> _busy, _painted, _graded;
        private readonly Func<object, int> _epoch, _generation, _revision;
        private readonly Func<object, IDictionary> _states;
        private readonly Func<object, double, double, double, double, int, bool> _changed;
        private readonly Func<object, double[], double[], int, bool, string, int> _add;
        private readonly Func<object, double[], double[], int, bool, string, long, int> _add120;
        private readonly Action<object, int> _invalidate;
        private readonly HashSet<string> _connections = new HashSet<string>();
        private object _connectionNetwork;

        internal VistasRoads(Assembly assembly)
        {
            if (VistasWorldGen.Is120)
            {
                var network = assembly.GetType(VistasWorldGen.Network120, true);
                var ground = assembly.GetType(VistasWorldGen.Ground120, true);
                var tileState = assembly.GetType(VistasWorldGen.Ground120 + "+_gvcDF5oLi7OELGjDZOqlBQayqdZ", true);
                _network = Access.Call<Func<object>>(assembly.GetType("_VMZDKcjNhDXyjFn6g3O5VsT2DWb", true), "_CqJPWQ3lkAljQiYYu3S7b1QFB9p", Type.EmptyTypes);
                var enabled = Access.Get<bool>(assembly.GetType("_VytWLDV3k8KEHZg3nuNSznWZpmC", true), "_w8Rbzu8jEMpBlbHK3b3CPekHXFg");
                _enabled = () => enabled(null);
                var spawnTrack = assembly.GetType(VistasWorldGen.Track120, true);
                var trackEnabled = Access.Get<bool>(spawnTrack, "_MMQ8zKGRslvk6pWHCIpYmgR3MlA");
                _spawnTrackEnabled = () => trackEnabled(null);
                _spawnTrackState = Read<int>(spawnTrack, "_SavBMyPfR9wfS1h8ZSnv3upYJgp", typeof(int));
                _gate = Read<object>(network, "_0DEn5J6Rdd0F8seUsufEoU6F5NX", typeof(object));
                _requested = Read<HashSet<string>>(network, "_V9bPTVzicQMJW4k6YAqArOikWAaA", typeof(HashSet<string>));
                _completed = Read<HashSet<string>>(network, "_rYS4ZttQfy2DQV0CqG2ezGWoqb", typeof(HashSet<string>));
                _busy = Access.Get<bool>(network, "_IbhyuLadRlh6WyHTBp1xUH0fm2C");
                _states = Read<IDictionary>(ground, "_XtZPmC1S95ntdW75LoybFfzENfq", typeof(Dictionary<,>).MakeGenericType(typeof(int), tileState));
                _epoch = Read<int>(ground, "_N4nfzc4N1XSJhrywpI73cRXK55h", typeof(int));
                _generation = Read<int>(tileState, "_e8X2e2quFY4yLxdyZuCxMSLKswc", typeof(int));
                _revision = Read<int>(tileState, "_qPGykP2VipXFCs5cFRzSxxuOQoP", typeof(int));
                _painted = Read<bool>(tileState, "_6I2DdOibS5aeeoNMPbNiFe7N4ml", typeof(bool));
                _graded = Read<bool>(tileState, "_YbiAcDXFwTA5d5ZBkZqKdrMhw8A", typeof(bool));
                _changed = Access.Call<Func<object, double, double, double, double, int, bool>>(network, "_LJrlIAf2BbOD8BOeKyGEsXWzexr", typeof(double), typeof(double), typeof(double), typeof(double), typeof(int));
                _add120 = Access.Call<Func<object, double[], double[], int, bool, string, long, int>>(network, "_Ub8zoH3AAR3DGM1SNvZpR6o32Mu", typeof(double[]), typeof(double[]), typeof(int), typeof(bool), typeof(string), typeof(long));
                _invalidate = Access.Call<Action<object, int>>(network, "_bw69Xys9RjjLiGSCdJlkSiaHWbt", typeof(int));
                return;
            }
            var q = assembly.GetType("A.q", true); var s = assembly.GetType("A.S", true); var state = assembly.GetType("A.S+A", true);
            _network = Access.Call<Func<object>>(assembly.GetType("A.r", true), "A", Type.EmptyTypes);
            _enabled = Access.Call<Func<bool>>(assembly.GetType("A.R", true), "B", Type.EmptyTypes);
            var track = assembly.GetType("A.U", true);
            _spawnTrackEnabled = Access.Call<Func<bool>>(track, "C", Type.EmptyTypes);
            foreach (var field in track.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (field.Name == "A" && field.FieldType == typeof(int) && field.GetRequiredCustomModifiers().Length == 0 && !field.IsLiteral)
                    _spawnTrackState = Access.Read<int>(field);
            if (_spawnTrackState == null) throw new MissingFieldException(track.FullName, "spawn track state");
            _gate = Read<object>(q, "A", typeof(object));
            _requested = Read<HashSet<string>>(q, "a", typeof(HashSet<string>)); _completed = Read<HashSet<string>>(q, "A", typeof(HashSet<string>));
            _busy = Access.Call<Func<object, bool>>(q, "e", Type.EmptyTypes);
            _states = Read<IDictionary>(s, "A", typeof(Dictionary<,>).MakeGenericType(typeof(int), state));
            _epoch = Read<int>(s, "D", typeof(int)); _generation = Read<int>(state, "A", typeof(int)); _revision = Read<int>(state, "a", typeof(int));
            _painted = Read<bool>(state, "A", typeof(bool)); _graded = Read<bool>(state, "a", typeof(bool));
            _changed = Access.Call<Func<object, double, double, double, double, int, bool>>(q, "A", typeof(double), typeof(double), typeof(double), typeof(double), typeof(int));
            _add = Access.Call<Func<object, double[], double[], int, bool, string, int>>(q, "A", typeof(double[]), typeof(double[]), typeof(int), typeof(bool), typeof(string));
            _invalidate = Access.Call<Action<object, int>>(q, "B", typeof(int));
        }

        private static Func<object, T> Read<T>(Type owner, string name, Type value)
        {
            foreach (var field in owner.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.Name == name && field.FieldType == value) return Access.Read<T>(field);
            throw new MissingFieldException(owner.FullName, name + " : " + value.Name);
        }

        public bool Ready(GameObject root)
        {
            // Compatibility limit: this covers currently loaded terrain. Future streaming, road-setting changes and Vista's
            // separate Add/Off cleanup writer can still affect saved town surroundings; saved town sites are never relocated.
            if (VistasWorldGen.Is120) return VistasWorldGen.NativeLoadingFinished120;
            var map = root == null ? null : root.GetComponent<MapMagicObject>();
            if (!VistasWorldGen.Present || map == null || map.IsGenerating()) return false;
            if (!_enabled()) return true;
            if (_spawnTrackEnabled() && _spawnTrackState(null) < 3) return false;
            var network = _network(); var states = _states(null); bool found = false;
            lock (_gate(network))
            {
                // Pending links into unloaded terrain are retained by Vista indefinitely. Wait only for requested generation batches.
                if (_busy(network) || !_requested(network).SetEquals(_completed(network))) return false;
                foreach (var tile in map.tiles.All())
                {
                    if (tile == null || tile.main == null || tile.ActiveTerrain != tile.main.terrain || tile.main.terrain == null) continue;
                    var data = tile.main.terrain.terrainData; if (data == null) return false; found = true;
                    var state = states[data.GetInstanceID()];
                    if (state == null || _generation(state) != _epoch(null) || !_painted(state) || !_graded(state)) return false;
                    double x = tile.coord.x * map.tileSize.x, z = tile.coord.z * map.tileSize.z;
                    if (_changed(network, x - 20, z - 20, x + data.size.x + 20, z + data.size.z + 20, _revision(state))) return false;
                }
            }
            return found;
        }

        public bool Connect(string key, Vector3[] worldPath)
        {
            if (!VistasWorldGen.Present || !_enabled() || worldPath == null || worldPath.Length < 2 || worldPath.Length > 20000) return false;
            var network = _network();
            if (!ReferenceEquals(_connectionNetwork, network)) { _connections.Clear(); _connectionNetwork = network; }
            if (_connections.Contains(key)) return true;
            var x = new double[worldPath.Length]; var z = new double[worldPath.Length];
            for (int i = 0; i < x.Length; i++)
            {
                if (float.IsNaN(worldPath[i].x) || float.IsInfinity(worldPath[i].x) || float.IsNaN(worldPath[i].z) || float.IsInfinity(worldPath[i].z)) return false;
                x[i] = worldPath[i].x; z[i] = worldPath[i].z;
            }
            lock (_gate(network))
            {
                int id = _add120 != null ? _add120(network, x, z, 2, false, "WW town " + key, 0L) : _add(network, x, z, 2, false, "WW town " + key);
                if (id < 0) return false;
                _invalidate(network, id); _connections.Add(key);
            }
            Log.Main(null, "[POI] Vista road connector registered: " + key + "; " + x.Length + " points; native painting and grading queued.");
            return true;
        }
    }
}
