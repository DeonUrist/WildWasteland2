using System;
using System.IO;
using UnityEngine;

namespace WildWasteland.Core
{
    internal static class VoiceAudio
    {
        internal static AudioClip LoadWav(string path, string name, float targetRms = 0f)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a RIFF WAV");
                reader.ReadUInt32(); if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAV");
                ushort format = 0, channels = 0, bits = 0; int rate = 0; byte[] data = null;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunk = new string(reader.ReadChars(4)); uint size = reader.ReadUInt32();
                    long end = reader.BaseStream.Position + size;
                    if (end > reader.BaseStream.Length) throw new InvalidDataException("Truncated WAV");
                    if (chunk == "fmt ")
                    {
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                        reader.ReadUInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
                    }
                    else if (chunk == "data") data = reader.ReadBytes((int)size);
                    reader.BaseStream.Position = Math.Min(reader.BaseStream.Length, end + (size & 1));
                }
                if (format != 1 || bits != 16 || channels < 1 || rate < 1 || data == null || data.Length % (channels * 2) != 0)
                    throw new InvalidDataException("Expected 16-bit PCM WAV");
                var samples = new float[data.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = (short)(data[i * 2] | data[i * 2 + 1] << 8) / 32768f;
                if (targetRms > 0f && samples.Length > 0)
                {
                    double energy = 0;
                    for (int i = 0; i < samples.Length; i++) energy += (double)samples[i] * samples[i];
                    double rms = Math.Sqrt(energy / samples.Length), gain = rms > 0 ? Math.Min(8.0, targetRms / rms) : 1.0;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        double value = samples[i] * gain, peak = Math.Abs(value);
                        samples[i] = (float)(peak > 0.7 ? Math.Sign(value) * (0.7 + 0.3 * Math.Tanh((peak - 0.7) / 0.3)) : value);
                    }
                    if (Log.Verbose) Log.Info("[Core] Voice sound " + name + " gain " + gain.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                }
                var clip = AudioClip.Create(name, samples.Length / channels, channels, rate, false);
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (!clip.SetData(samples, 0)) { UnityEngine.Object.Destroy(clip); throw new InvalidDataException("Could not upload WAV samples"); }
                return clip;
            }
        }
    }
}
