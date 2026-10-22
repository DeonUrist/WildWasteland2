using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using WildWasteland.Core;

namespace WildWasteland.Modules.POI
{
    internal static class TownSaveRecord
    {
        internal const string Key = "WildWasteland.Towns.World.v1";
        private const int Magic = 0x57575453;
        internal static void Save(string slot, Dictionary<string, TownRecord> records)
        {
            string value;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                byte[] towns = TownStore.Serialize(records); string[] names = InteriorPopulation.CaptureNames();
                writer.Write(Magic); writer.Write(1); writer.Write(towns.Length); writer.Write(towns);
                writer.Write(names.Length); foreach (string name in names) writer.Write(name);
                writer.Flush(); byte[] payload = stream.ToArray();
                using (var hash = SHA256.Create()) writer.Write(hash.ComputeHash(payload));
                writer.Flush(); value = Convert.ToBase64String(stream.ToArray());
            }
            SaveBus.Key(Key).Write(null, slot, value);
        }
        internal static Dictionary<string, TownRecord> Load(string slot)
        {
            var disk = new ES3Settings(slot) { location = ES3.Location.File };
            if (File.Exists(disk.FullPath) && ES3.KeyExists(Key, disk))
            {
                byte[] bytes = Convert.FromBase64String(ES3.Load<string>(Key, disk));
                if (bytes.Length < 48) throw new InvalidDataException("Truncated embedded Towns record");
                int length = bytes.Length - 32;
                using (var hash = SHA256.Create())
                { var sum = hash.ComputeHash(bytes, 0, length); for (int i = 0; i < sum.Length; i++) if (sum[i] != bytes[length + i]) throw new InvalidDataException("Embedded Towns checksum mismatch"); }
                using (var reader = new BinaryReader(new MemoryStream(bytes, 0, length, false)))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported embedded Towns record");
                    int size = reader.ReadInt32(); if (size < 0 || size > length - 16) throw new InvalidDataException("Embedded Towns layout length");
                    var records = TownStore.Deserialize(reader.ReadBytes(size));
                    int count = reader.ReadInt32(); if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position)) throw new InvalidDataException("Embedded Towns interior count");
                    var names = new string[count]; for (int i = 0; i < count; i++) names[i] = reader.ReadString();
                    if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing embedded Towns data");
                    InteriorPopulation.RestoreNames(names);
                    Log.Always("[Towns] loaded " + records.Count + " chunk decisions/layouts directly from " + slot);
                    return records;
                }
            }
            // No embedded record: a new world (D12: the WW1 config/WildWasteland/Towns/Saves sidecar import is gone).
            InteriorPopulation.RestoreNames(new string[0]);
            return new Dictionary<string, TownRecord>(StringComparer.Ordinal);
        }
    }
}
