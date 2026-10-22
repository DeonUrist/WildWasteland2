using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>Fog-of-war sidecar I/O: config/WildWasteland/HUD/Saves/&lt;slot&gt;.fog.bin (gzip "IMFP" v4, world seed checked), read at world ready, written when a save starts and at SaveAll.</summary>
    internal static partial class MapFog
    {
        private static string _saves;

        // ---- saves: config/WildWasteland/HUD/Saves/<slot>.fog.bin (Immersive Map slot import removed, D12)
        /// <summary>World seed from the SaveLoadGame FSM, or int.MinValue when unknown.</summary>
        private static int Seed()
        {
            var fsm = GameRefs.SaveLoadGame; if (fsm == null) return int.MinValue;
            var v = fsm.FsmVariables.FindFsmInt("seed"); return v != null ? v.Value : int.MinValue;
        }

        /// <summary>Resets the fog and reads the slot's fog sidecar when its seed matches the loaded world.</summary>
        internal static void WorldReady(string slot)
        {
            Chunks.Clear(); _window = false; _havePosition = false; _idle = false; _painting = false; _loaded = true; _seed = Seed();
            if (_standDown) return;
            string from = "nothing saved for this world yet";
            try
            {
                if (_seed == int.MinValue) from = "unknown world seed";
                else if (!string.IsNullOrEmpty(slot))
                {
                    string own = Path.Combine(_saves, Path.GetFileName(slot) + ".fog.bin");
                    if (File.Exists(own) && Read(File.ReadAllBytes(own))) from = own;
                }
            }
            catch (Exception e) { Chunks.Clear(); HudModule.Warn("Fog of war could not be read: " + e.Message); }
            HudModule.Log("Fog of war loaded from " + from + " (" + Chunks.Count + " chunks, seed " + _seed + ")");
        }

        /// <summary>Writes the slot's fog sidecar through AtomicFile (seed checked); failures fail the save transaction.</summary>
        internal static void SaveEnded(string slot)
        {
            if (!_loaded || _standDown || string.IsNullOrEmpty(slot)) return;
            int seed = Seed();
            if (seed == int.MinValue || seed != _seed) { HudModule.Warn("Fog of war not saved: world seed " + seed + " differs from " + _seed); return; }
            // Plain sidecar, not ES3: written at once. Save-and-quit leaves for the title during the save, before isPlay/AfterSave.
            try
            {
                var data = Write(seed);
                Directory.CreateDirectory(_saves);
                string path = Path.Combine(_saves, Path.GetFileName(slot) + ".fog.bin");
                File.WriteAllBytes(path + ".tmp", data);
                AtomicFile.Replace(path + ".tmp", path);
                HudModule.Log("Fog of war saved: " + Path.GetFileName(path) + " (" + Chunks.Count + " chunks, " + data.Length + " bytes)");
            }
            catch (Exception e) { Core.SaveBus.ReportFailure("Fog of war save failed: " + e.Message); HudModule.Warn("Fog of war could not be saved: " + e.Message); }
        }

        // SaveGame (global event) -> wait -> SaveGame -> isPlay. Write when the save starts and again at SaveAll.
        /// <summary>SaveBus state observer: writes the fog when a save starts (wait) and again at SaveGame.</summary>
        private static void SaveState(string previous, string now)
        {
            if (now == "wait" || now == "SaveGame") SaveEnded(SaveBus.Slot);
        }

        /// <summary>Encodes the gzip "IMFP" v4 fog file (inverted reveal bytes per 64x64 chunk).</summary>
        private static byte[] Write(int seed)
        {
            var reveal = new byte[4096];
            using (var memory = new MemoryStream())
            {
                using (var gz = new GZipStream(memory, CompressionMode.Compress, true))
                using (var w = new BinaryWriter(gz))
                {
                    w.Write(Magic); w.Write(FormatVersion); w.Write(seed); w.Write(Chunks.Count);
                    foreach (var pair in Chunks)
                    {
                        w.Write((int)(pair.Key >> 32)); w.Write((int)(pair.Key & 0xFFFFFFFFu));
                        var d = pair.Value.D; for (int i = 0; i < 4096; i++) reveal[i] = (byte)(255 - d[i]);
                        w.Write(reveal);
                    }
                    w.Write(0); w.Write(0);   // base and POI marker lists of the source format
                }
                return memory.ToArray();
            }
        }

        /// <summary>Decodes a current-version (v4) fog file of this seed; false for any other version or seed.</summary>
        private static bool Read(byte[] data)
        {
            var read = new Dictionary<long, Chunk>();
            using (var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
            using (var r = new BinaryReader(gz))
            {
                if (r.ReadInt32() != Magic) return false;
                int version = r.ReadInt32(); if (version != FormatVersion) return false;   // D18: only the current format is read
                if (r.ReadInt32() != _seed) return false;
                int count = r.ReadInt32();
                for (int n = 0; n < count; n++)
                {
                    int cx = r.ReadInt32(), cz = r.ReadInt32(); var bytes = r.ReadBytes(4096); if (bytes.Length != 4096) return false;
                    var c = new Chunk(); for (int i = 0; i < 4096; i++) c.D[i] = (byte)(255 - bytes[i]);
                    read[Key(cx, cz)] = c;
                }
            }
            Chunks.Clear(); foreach (var pair in read) Chunks[pair.Key] = pair.Value;
            return true;
        }
    }
}
