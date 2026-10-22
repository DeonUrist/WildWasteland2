using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
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
            var cache = new ES3Settings(slot) { location = ES3.Location.Cache };
            ES3.Save<string>(Key, value, cache);
            // Native save actions flush their cache before AfterSave. Repeat into that same cache,
            // then verify the disk record while the robust working-file transaction is still open.
            SaveBus.AfterSave(() =>
            {
                try
                {
                    ES3.Save<string>(Key, value, cache); ES3.StoreCachedFile(slot);
                    var disk = new ES3Settings(slot) { location = ES3.Location.File };
                    if (!ES3.KeyExists(Key, disk) || ES3.Load<string>(Key, disk) != value)
                        throw new IOException("Embedded Towns record did not persist");
                    Log.Always("[Towns] embedded save verified: " + slot + "; layouts, spawners and interior identities participate in the world transaction/backups.");
                }
                catch (Exception error) { SaveBus.ReportFailure("Embedded Towns save failed: " + error.Message); Log.Error("[Towns] embedded save verification failed", error); }
            });
        }
        internal static Dictionary<string, TownRecord> Load(string slot, string legacy)
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
            // Read-only compatibility import. Never prefer old config data over an embedded record,
            // and never hide a corrupt embedded record by falling back to stale sidecars.
            var old = TownStore.Read(legacy); InteriorPopulation.Load(legacy + ".interiors.txt");
            if (File.Exists(legacy) || File.Exists(legacy + ".interiors.txt")) Log.Always("[Towns] imported legacy town data; the next successful save stores it inside the world file, without new Towns sidecars.");
            return old;
        }
    }
}
