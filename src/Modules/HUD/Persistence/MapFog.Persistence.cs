using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    /// <summary>HUD-owned exploration record embedded in the ES3 world save; the gzip payload retains its seed and current format.</summary>
    internal static partial class MapFog
    {
        private static readonly SaveKey FogSave = SaveBus.Key("WildWasteland.HUD.Fog.v1");
        private static string _fogReadError;

        /// <summary>World seed from the SaveLoadGame FSM, or int.MinValue when unknown.</summary>
        private static int Seed()
        {
            var fsm = GameRefs.SaveLoadGame; if (fsm == null) return int.MinValue;
            var v = fsm.FsmVariables.FindFsmInt("seed"); return v != null ? v.Value : int.MinValue;
        }

        /// <summary>Resets the fog and reads the embedded record when its seed matches the loaded world.</summary>
        internal static void WorldReady(string slot)
        {
            Chunks.Clear(); _fogReadError = null; _window = false; _havePosition = false; _idle = false; _painting = false; _loaded = true; _seed = Seed();
            if (_standDown) return;
            string from = "nothing saved for this world yet";
            try
            {
                if (_seed == int.MinValue) from = "unknown world seed";
                else if (!string.IsNullOrEmpty(slot))
                {
                    string record = FogSave.Read(slot);
                    if (record != null)
                    {
                        if (!Read(Convert.FromBase64String(record))) throw new InvalidDataException("Exploration record has an invalid format or world seed");
                        from = "world save";
                    }
                }
            }
            catch (Exception e) { Chunks.Clear(); _fogReadError = "Fog of war could not be read: " + e.Message; HudModule.Warn(_fogReadError); }
            HudModule.Log("Fog of war loaded from " + from + " (" + Chunks.Count + " chunks, seed " + _seed + ")");
        }

        /// <summary>Writes the embedded exploration record through the verified save transaction.</summary>
        internal static void SaveEnded(string slot)
        {
            if (_fogReadError != null) { SaveBus.ReportFailure(HudModule.Instance, _fogReadError); return; }
            if (!_loaded || _standDown || string.IsNullOrEmpty(slot)) return;
            int seed = Seed();
            if (seed == int.MinValue || seed != _seed)
            {
                SaveBus.ReportFailure(HudModule.Instance, "Fog of war save rejected: world seed differs from loaded exploration");
                return;
            }
            try
            {
                var data = Write(seed);
                FogSave.Write(HudModule.Instance, slot, Convert.ToBase64String(data));
            }
            catch (Exception e) { SaveBus.ReportFailure(HudModule.Instance, "Fog of war save failed: " + e.Message); HudModule.Warn("Fog of war could not be saved: " + e.Message); }
        }

        /// <summary>Captures exploration once when native serialization starts.</summary>
        private static void SaveState(string previous, string now)
        {
            if (now == "SaveGame") SaveEnded(SaveBus.Slot);
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
                if (count < 0) return false;
                for (int n = 0; n < count; n++)
                {
                    int cx = r.ReadInt32(), cz = r.ReadInt32(); var bytes = r.ReadBytes(4096); if (bytes.Length != 4096) return false;
                    var c = new Chunk(); for (int i = 0; i < 4096; i++) c.D[i] = (byte)(255 - bytes[i]);
                    if (read.ContainsKey(Key(cx, cz))) return false;
                    read.Add(Key(cx, cz), c);
                }
                if (r.ReadInt32() != 0 || r.ReadInt32() != 0 || r.BaseStream.ReadByte() != -1) return false;
            }
            Chunks.Clear(); foreach (var pair in read) Chunks[pair.Key] = pair.Value;
            return true;
        }
    }
}
