using System;
using System.IO;
using System.Reflection;
using InsaneSystems.InputManager;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Vehicles;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// The ignition hotkey is a row in the game's own Controls screen ("Ignition", after Handbrake), not a config entry (2.3.2).
    /// The game's rebinding asset (InsaneSystems InputManager) drops actions that are not in its built-in list whenever it loads its
    /// saved bindings, so the row is added again on every start and its keys are kept in config/WildWasteland/Vehicles/ignition-key.txt
    /// ("Key,AlternativeKey"; D12/D13: no copy from older folders). A rebind in the Controls screen is written there at once. Car reads
    /// the action through GameBindings (IgnitionAction = "Ignition"); <see cref="KeyCode"/> mirrors the key for the moment before the row exists.
    /// </summary>
    internal static class IgnitionControl
    {
        internal const string Name = "Ignition";
        private static KeyCode _key = KeyCode.I, _alt = KeyCode.None, _lastKey, _lastAlt;
        private static bool _loaded, _lastEnabled;
        private static string _path = "ignition-key.txt";

        /// <summary>The primary ignition key (mirror of the Controls row; I until the row exists).</summary>
        internal static KeyCode KeyCode { get { return _key; } }

        /// <summary>Reads the key file at <paramref name="path"/> (Vehicles Init), writing <paramref name="fallback"/> when it does not exist yet.</summary>
        internal static void Load(string path, KeyCode fallback)
        {
            _loaded = true; _path = path;
            try
            {
                if (File.Exists(_path))
                {
                    var parts = File.ReadAllText(_path).Trim().Split(',');
                    _key = Parse(parts.Length > 0 ? parts[0] : "", KeyCode.I);
                    _alt = parts.Length > 1 ? Parse(parts[1], KeyCode.None) : KeyCode.None;
                }
                else
                {
                    if (fallback != KeyCode.None) _key = fallback;
                    Save();
                    Log.Main(VehiclesModule.Instance, "Ignition key " + _key + " is on the game's Controls screen (" + _path + ")");
                }
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Ignition key file: " + e.Message); }
            _lastKey = _key; _lastAlt = _alt;
        }

        /// <summary>Parses a KeyCode name (case-insensitive); <paramref name="fallback"/> for anything unknown.</summary>
        private static KeyCode Parse(string s, KeyCode fallback)
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), s.Trim(), true); } catch { return fallback; }
        }

        /// <summary>Writes "Key,AlternativeKey" through a same-folder temp file and AtomicFile.Replace (D17: no Win32 1175 on replace).</summary>
        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temp = _path + ".tmp";
                File.WriteAllText(temp, _key + "," + _alt);
                if (File.Exists(_path)) AtomicFile.Replace(temp, _path);
                else File.Move(temp, _path);
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Ignition key file: " + e.Message); }
        }

        /// <summary>
        /// Interval job "Vehicles.Ignition control" (0.5 s): keeps the "Ignition" row in the game's Controls list while the module runs and
        /// [Vehicles / Controls] IgnitionKey is on, removes it otherwise, and records rebinds made in the Controls screen.
        /// </summary>
        internal static void Tick()
        {
            if (!_loaded) return;
            InputStorage st;
            try { st = InputStorage.Singleton; } catch { return; }
            if (st == null || st.Keys == null) return;
            var keys = st.Keys;
            bool enabled = VehiclesModule.Running && VehiclesSettings.IgnitionKey.Value, changed = false;
            int idx = IndexOf(keys, Name);
            if (enabled)
            {
                if (idx < 0)
                {
                    var ka = Make();
                    if (ka == null) return;
                    int after = IndexOf(keys, "Handbrake");
                    keys.Insert(after >= 0 ? Mathf.Min(after + 1, keys.Count) : keys.Count, ka);
                    _lastKey = _key; _lastAlt = _alt;
                    changed = true;
                    if (Log.L2) Log.Deep(VehiclesModule.Instance, "Controls: added \"" + Name + "\" (" + _key + ")");
                }
                else
                {
                    var ka = keys[idx];
                    if (ka.Key != _lastKey || ka.AlternativeKey != _lastAlt)
                    {   // rebound in the Controls screen
                        _key = _lastKey = ka.Key; _alt = _lastAlt = ka.AlternativeKey;
                        Save();
                        Log.Main(VehiclesModule.Instance, "Controls: Ignition rebound to " + _key + (_alt != KeyCode.None ? " / " + _alt : ""));
                    }
                }
            }
            else if (idx >= 0) { keys.RemoveAt(idx); changed = true; }
            if (changed || enabled != _lastEnabled) { _lastEnabled = enabled; RefreshMenu(); }
        }

        /// <summary>Index of the named key action in the game's list (-1 when absent); a loop, no predicate closure.</summary>
        private static int IndexOf(System.Collections.Generic.List<KeyAction> keys, string name)
        {
            for (int i = 0; i < keys.Count; i++) if (keys[i] != null && keys[i].Name == name) return i;
            return -1;
        }

        /// <summary>Module shutdown: the row leaves the game's Controls list.</summary>
        internal static void Remove()
        {
            if (!_loaded) return;
            try
            {
                var st = InputStorage.Singleton;
                if (st != null && st.Keys != null)
                {
                    int idx = IndexOf(st.Keys, Name);
                    if (idx >= 0) { st.Keys.RemoveAt(idx); RefreshMenu(); }
                }
            }
            catch (Exception e) { Log.Deep(VehiclesModule.Instance, "Controls: " + e.Message); }
            _lastEnabled = false;
        }

        /// <summary>Builds the "Ignition" KeyAction (private fields set by reflection, once per insertion); null when the asset changed.</summary>
        private static KeyAction Make()
        {
            try
            {
                var ka = new KeyAction();
                SetField(typeof(InputAction), ka, "name", Name);
                SetField(typeof(KeyAction), ka, "key", _key);
                SetField(typeof(KeyAction), ka, "alternativeKey", _alt);
                return ka;
            }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Could not add the Ignition control: " + e.Message); _loaded = false; return null; }
        }

        /// <summary>Sets a private instance field of the InputManager types (throws when it is missing).</summary>
        private static void SetField(Type t, object o, string name, object v)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) throw new MissingFieldException(t.Name, name);
            f.SetValue(o, v);
        }

        /// <summary>Rebuilds the Controls screen if it is open (its rows are generated from InputStorage.keys).</summary>
        private static void RefreshMenu()
        {
            try
            {
                var s = InsaneSystems.InputManager.UI.Settings.singleton;
                if (s == null) return;
                var m = typeof(InsaneSystems.InputManager.UI.Settings).GetMethod("RefreshUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null) m.Invoke(s, null);
            }
            catch (Exception e) { Log.Deep(VehiclesModule.Instance, "Controls refresh: " + e.Message); }
        }
    }
}
