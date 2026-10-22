using System;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// The bus passenger pose preset (one for all bus passengers): loaded lazily from <c>config/WildWasteland/Presentation/BusPassengerPose.json</c>
    /// (D13; D12: the old Patrols editor folder is not read), validated (five joints per side, finite values, clamped), written by the
    /// Developer bus passenger pose editor through <see cref="Save"/> with a same-directory atomic replace and a <c>.bak</c> (D17).
    /// </summary>
    internal static class BusPassengerPoseStore
    {
        private static BusPassengerPreset _preset;
        /// <summary>The current preset (loaded on first use; defaults when the file is missing or invalid).</summary>
        internal static BusPassengerPreset Current { get { if (_preset == null) Load(); return _preset; } }
        /// <summary>Folder of the preset file.</summary>
        private static string Dir { get { return Assets.ModuleConfigDir("Presentation"); } }
        /// <summary>Full path of BusPassengerPose.json.</summary>
        private static string FilePath { get { return Path.Combine(Dir, "BusPassengerPose.json"); } }

        /// <summary>Reads and validates the preset file; any problem keeps the built-in defaults with a warning.</summary>
        private static void Load()
        {
            _preset = new BusPassengerPreset();
            try
            {
                if (!File.Exists(FilePath)) return;
                var saved = JsonUtility.FromJson<BusPassengerPreset>(File.ReadAllText(FilePath));
                if (saved == null || saved.left == null || saved.right == null || saved.left.Length != 5 || saved.right.Length != 5) throw new InvalidDataException("Expected five joints per side");
                for (int i = 0; i < 5; i++) { saved.left[i] = Safe(saved.left[i], 180f); saved.right[i] = Safe(saved.right[i], 180f); }
                saved.seatOffset = Safe(saved.seatOffset, 1f); _preset = saved;
            }
            catch (Exception e) { Log.Warn(PresentationModule.WorldOwner, "Bus passenger pose: " + e.Message); }
        }

        /// <summary>Clamps a joint vector to +-<paramref name="limit"/>; NaN / infinity reject the whole file.</summary>
        private static Vector3 Safe(Vector3 value, float limit)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.y) || float.IsNaN(value.z) || float.IsInfinity(value.z)) throw new InvalidDataException("Invalid joint value");
            return new Vector3(Mathf.Clamp(value.x, -limit, limit), Mathf.Clamp(value.y, -limit, limit), Mathf.Clamp(value.z, -limit, limit));
        }

        /// <summary>Back to the built-in defaults (not saved).</summary>
        internal static void Reset() { _preset = new BusPassengerPreset(); }

        /// <summary>Writes the current preset (temp file + <see cref="AtomicFile.Replace(string,string,string)"/> with a .bak); returns the status line for the editor.</summary>
        internal static string Save()
        {
            try
            {
                Directory.CreateDirectory(Dir); string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(Current, true));
                AtomicFile.Replace(temp, FilePath, FilePath + ".bak");
                return "Saved BusPassengerPose.json (all bus passengers)";
            }
            catch (Exception e) { Log.Warn(PresentationModule.WorldOwner, "Bus passenger pose save: " + e.Message); return "Save failed: " + e.Message; }
        }
    }
}
