using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WildWasteland.Core
{
    /// <summary>
    /// One home for audio file handling: the WAV decoder (superset of Core VoiceAudio, Guns/NPC Wav, Quests ReadWav and the
    /// Patrols Rider Wav), level helpers (RMS normalisation of VoiceAudio, peak levelling of Quests, clamped gain of Rider and
    /// HUD, active-RMS loudness matching of NPC Women Level) and <see cref="LoadAsync"/>, which replaces the five
    /// UnityWebRequestMultimedia coroutines (StewPot, Women Voice, WeatherThunder, WeatherAudio, HeadshotAudio).
    /// Everything here is load-time work; nothing runs per frame.
    /// </summary>
    public static class Audio
    {
        /// <summary>Limiter knee used by <see cref="Process"/> (NPC Women Level.Knee): samples below it are untouched.</summary>
        public const float Knee = 0.7f;

        // ------------------------------------------------------------------ WAV decoding

        /// <summary>
        /// Decodes a RIFF/WAVE byte array to interleaved samples in -1..1 (Guns/NPC Wav.Read, verbatim): PCM 8/16/24/32-bit,
        /// IEEE float 32/64, WAVE_FORMAT_EXTENSIBLE; a wrong size in the last chunk is tolerated; the sample count is
        /// trimmed to whole frames. Throws InvalidDataException on anything it cannot read.
        /// </summary>
        public static float[] DecodeWav(byte[] b, out int channels, out int rate)
        {
            channels = 0; rate = 0;
            if (b == null || b.Length < 12 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E')
                throw new InvalidDataException("not a RIFF/WAVE file");
            int fmt = 0, bits = 0, dataOff = -1, dataLen = 0;
            int p = 12;
            while (p + 8 <= b.Length)
            {
                bool isFmt = b[p] == 'f' && b[p + 1] == 'm' && b[p + 2] == 't' && b[p + 3] == ' ';
                bool isData = b[p] == 'd' && b[p + 1] == 'a' && b[p + 2] == 't' && b[p + 3] == 'a';
                int len = BitConverter.ToInt32(b, p + 4);
                int body = p + 8;
                if (len < 0 || body + len > b.Length) len = b.Length - body;   // tolerate a wrong size in the last chunk
                if (isFmt)
                {
                    fmt = BitConverter.ToUInt16(b, body);
                    channels = BitConverter.ToUInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToUInt16(b, body + 14);
                    if (fmt == 0xFFFE && len >= 26) fmt = BitConverter.ToUInt16(b, body + 24);   // extensible: sub-format GUID starts with the real tag
                }
                else if (isData) { dataOff = body; dataLen = len; }
                p = body + len + (len & 1);
            }
            if (channels <= 0 || rate <= 0 || dataOff < 0) throw new InvalidDataException("missing fmt or data chunk");
            int bps = bits / 8;
            if (bps <= 0) throw new InvalidDataException("bad bit depth " + bits);
            int n = dataLen / bps;
            n -= n % channels;
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                int o = dataOff + i * bps;
                if (fmt == 3)
                    s[i] = bps == 8 ? (float)BitConverter.ToDouble(b, o) : BitConverter.ToSingle(b, o);
                else if (fmt == 1)
                {
                    switch (bps)
                    {
                        case 1: s[i] = (b[o] - 128) / 128f; break;
                        case 2: s[i] = BitConverter.ToInt16(b, o) / 32768f; break;
                        case 3: s[i] = ((b[o] | (b[o + 1] << 8) | (b[o + 2] << 16)) << 8 >> 8) / 8388608f; break;
                        case 4: s[i] = BitConverter.ToInt32(b, o) / 2147483648f; break;
                        default: throw new InvalidDataException("unsupported PCM bit depth " + bits);
                    }
                }
                else throw new InvalidDataException("unsupported WAV format " + fmt + " (save as PCM or float WAV)");
            }
            return s;
        }

        /// <summary>Reads and decodes a WAV file (see <see cref="DecodeWav"/>).</summary>
        public static float[] ReadWav(string path, out int channels, out int rate) { return DecodeWav(File.ReadAllBytes(path), out channels, out rate); }

        /// <summary>
        /// Creates an AudioClip from interleaved samples (length at least 1 frame, as NPC Women Voice). keepLoaded sets
        /// HideFlags.DontUnloadUnusedAsset (VoiceAudio, Rider, Women). Throws (and destroys the clip) when Unity refuses the data.
        /// </summary>
        public static AudioClip Clip(string name, float[] samples, int channels, int rate, bool keepLoaded = true)
        {
            var clip = AudioClip.Create(name, Math.Max(1, samples.Length / channels), channels, rate, false);
            if (keepLoaded) clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (samples.Length > 0 && !clip.SetData(samples, 0)) { UnityEngine.Object.Destroy(clip); throw new InvalidDataException("Could not upload WAV samples"); }
            return clip;
        }

        /// <summary>
        /// Core Core.Audio.LoadWav replacement: decodes path into a kept-loaded clip; targetRms &gt; 0 normalises the RMS to it
        /// (gain at most 8) with the 0.7 soft clip and logs the gain at verbose level. Throws on unreadable files.
        /// Difference: accepts every format of <see cref="DecodeWav"/> (VoiceAudio accepted 16-bit PCM only and threw on a truncated chunk).
        /// </summary>
        public static AudioClip LoadWav(string path, string name, float targetRms = 0f)
        {
            int ch, rate;
            var samples = ReadWav(path, out ch, out rate);
            if (targetRms > 0f && samples.Length > 0)
            {
                double gain = NormalizeRms(samples, targetRms);
                if (Log.L1) Log.Main(null,"[Core] Voice sound " + name + " gain " + gain.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            return Clip(name, samples, ch, rate, true);
        }

        /// <summary>
        /// Patrols Rider Wav.Load replacement: null when the file is missing or unreadable (never throws); gain != 1 is applied with
        /// a clamp to -1..1. The clip is kept loaded.
        /// </summary>
        public static AudioClip TryLoadWav(string path, string name, float gain = 1f)
        {
            try
            {
                if (!File.Exists(path)) return null;
                int ch, rate;
                var samples = ReadWav(path, out ch, out rate);
                if (gain != 1f) Gain(samples, gain);
                return Clip(name, samples, ch, rate, true);
            }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ levels

        /// <summary>Plain RMS of samples (0 when empty).</summary>
        public static double Rms(float[] s)
        {
            if (s == null || s.Length == 0) return 0;
            double e = 0;
            for (int i = 0; i < s.Length; i++) e += (double)s[i] * s[i];
            return Math.Sqrt(e / s.Length);
        }

        /// <summary>Absolute peak of samples.</summary>
        public static float Peak(float[] s)
        {
            float peak = 0f;
            if (s != null) for (int i = 0; i < s.Length; i++) { float a = Math.Abs(s[i]); if (a > peak) peak = a; }
            return peak;
        }

        /// <summary>
        /// VoiceAudio normalisation in place: gain = min(8, targetRms / rms), then a tanh soft clip above 0.7. Returns the gain.
        /// </summary>
        public static double NormalizeRms(float[] samples, float targetRms)
        {
            double rms = Rms(samples), gain = rms > 0 ? Math.Min(8.0, targetRms / rms) : 1.0;
            for (int i = 0; i < samples.Length; i++)
            {
                double value = samples[i] * gain, peak = Math.Abs(value);
                samples[i] = (float)(peak > 0.7 ? Math.Sign(value) * (0.7 + 0.3 * Math.Tanh((peak - 0.7) / 0.3)) : value);
            }
            return gain;
        }

        /// <summary>
        /// Quests Voice levelling in place: scales the peak to peakTarget (0.9 in Quests; skipped below 0.0005 peak), then
        /// multiplies by boost and, when boost &gt; 1, soft clips with tanh.
        /// </summary>
        public static void NormalizePeak(float[] data, float peakTarget, float boost)
        {
            float peak = Peak(data);
            float lvl = peak > 0.0005f ? peakTarget / peak : 1f;
            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i] * lvl * boost;
                data[i] = boost > 1f ? (float)Math.Tanh(v) : v;
            }
        }

        /// <summary>Multiplies samples by gain in place, clamped to -1..1 (Rider Wav, HUD headshot boost).</summary>
        public static void Gain(float[] samples, float gain)
        {
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
        }

        /// <summary>
        /// Loudness (NPC Women Level.ActiveRms): RMS over 1024-sample blocks whose RMS is above 5 % of the clip's peak, so
        /// leading/trailing silence does not skew it. 0 for silence.
        /// </summary>
        public static double Level(float[] s)
        {
            if (s == null || s.Length == 0) return 0;
            double peak = 0;
            for (int i = 0; i < s.Length; i++) { double a = Math.Abs(s[i]); if (a > peak) peak = a; }
            if (peak <= 0) return 0;
            const int W = 1024;
            double sum = 0; int blocks = 0;
            for (int i = 0; i < s.Length; i += W)
            {
                int n = Math.Min(W, s.Length - i);
                double e = 0;
                for (int k = 0; k < n; k++) e += (double)s[i + k] * s[i + k];
                e /= n;
                if (Math.Sqrt(e) > peak * 0.05) { sum += e; blocks++; }
            }
            return blocks > 0 ? Math.Sqrt(sum / blocks) : 0;
        }

        /// <summary>Gain then a soft limiter above <see cref="Knee"/> (|y| stays below 1); returns the number of limited samples.</summary>
        private static int GainLimit(float[] src, float[] dst, double gain)
        {
            int limited = 0;
            for (int i = 0; i < src.Length; i++)
            {
                double x = src[i] * gain, a = Math.Abs(x);
                if (a > Knee)
                {
                    a = Knee + (1.0 - Knee) * Math.Tanh((a - Knee) / (1.0 - Knee));
                    x = x < 0 ? -a : a;
                    limited++;
                }
                dst[i] = (float)x;
            }
            return limited;
        }

        /// <summary>
        /// NPC Women Level.Process: brings s (in place) to targetRms x volume, correcting the gain twice against the limited
        /// result; gain clamped to 1/16..16. targetRms &lt;= 0: volume only. Returns the gain applied (before limiting).
        /// </summary>
        public static double Process(float[] s, double targetRms, double volume, out int limited)
        {
            limited = 0;
            double mine = Level(s);
            if (mine <= 1e-6) return 1.0;
            double want = (targetRms > 0 ? targetRms : mine) * volume;
            double gain = ClampGain(want / mine);
            if (Math.Abs(gain - 1.0) < 1e-4) return 1.0;
            var tmp = new float[s.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                limited = GainLimit(s, tmp, gain);
                double got = Level(tmp);
                if (got <= 1e-9 || limited == 0) break;
                double g2 = ClampGain(gain * want / got);
                if (Math.Abs(g2 - gain) / gain < 0.01) break;
                gain = g2;
            }
            limited = GainLimit(s, s, gain);
            return gain;
        }

        /// <summary>Clamps a gain to 1/16..16 (+-24 dB).</summary>
        private static double ClampGain(double g) { return Math.Max(1.0 / 16.0, Math.Min(16.0, g)); }

        // ------------------------------------------------------------------ async loading

        /// <summary>
        /// A running <see cref="LoadAsync"/>: Done/Clip/Error once finished; Cancel() aborts the request (call it from the
        /// owning module's Shutdown, as the old loaders kept their own _request for that).
        /// </summary>
        public sealed class Load
        {
            /// <summary>File being loaded.</summary>
            public string Path;
            /// <summary>Decoded clip (null on failure or before Done).</summary>
            public AudioClip Clip;
            /// <summary>Failure text: "missing", the request error, or "could not be decoded"; null on success.</summary>
            public string Error;
            /// <summary>True once the callback ran (or the load was cancelled).</summary>
            public bool Done;
            internal UnityWebRequest Request;
            internal Coroutine Routine;

            /// <summary>Aborts the request and stops the coroutine; the callback is not invoked.</summary>
            public void Cancel()
            {
                if (Done) return;
                Done = true;
                if (Routine != null) { Scheduler.StopCoroutine(Routine); Routine = null; }
                if (Request != null) { try { Request.Abort(); Request.Dispose(); } catch (Exception) { } Request = null; }
            }
        }

        /// <summary>
        /// Loads an audio file (MP3/OGG/WAV...) through UnityWebRequestMultimedia on the runner's coroutine host and calls
        /// callback(clip, error) once: clip is null with error "missing" (file absent), the request error, or "could not be decoded".
        /// decompress=true sets compressed=false/streamAudio=false on the handler (short clips: decode fully on load; Thunder,
        /// Headshot, Women). HideFlags/naming/gain are left to the callback, as each old loader set its own.
        /// </summary>
        public static Load LoadAsync(string path, AudioType type, Action<AudioClip, string> callback, bool decompress = false)
        {
            var load = new Load { Path = path };
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { Finish(load, null, "missing", callback); return load; }
            load.Routine = Scheduler.StartCoroutine(Run(load, type, callback, decompress));
            if (load.Routine == null && !load.Done) Finish(load, null, "no coroutine host", callback);
            return load;
        }

        /// <summary>Coroutine body of <see cref="LoadAsync"/>.</summary>
        private static IEnumerator Run(Load load, AudioType type, Action<AudioClip, string> callback, bool decompress)
        {
            var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(load.Path).AbsoluteUri, type);
            load.Request = request;
            if (decompress)
            {
                var dh = request.downloadHandler as DownloadHandlerAudioClip;
                if (dh != null) { dh.compressed = false; dh.streamAudio = false; }
            }
            yield return request.SendWebRequest();
            if (load.Done) yield break;   // cancelled
            AudioClip clip = null; string error = null;
            try
            {
                if (request.result != UnityWebRequest.Result.Success) error = request.error ?? "request failed";
                else
                {
                    clip = DownloadHandlerAudioClip.GetContent(request);
                    if (clip == null) error = "could not be decoded";
                }
            }
            catch (Exception e) { clip = null; error = e.Message; }
            finally { request.Dispose(); load.Request = null; load.Routine = null; }
            Finish(load, clip, error, callback);
        }

        /// <summary>Stores the result and invokes the callback inside a try/catch (a throwing callback is logged, not propagated).</summary>
        private static void Finish(Load load, AudioClip clip, string error, Action<AudioClip, string> callback)
        {
            load.Done = true; load.Clip = clip; load.Error = error;
            if (callback == null) return;
            try { callback(clip, error); }
            catch (Exception e) { Log.Warn(null,"[Core] Audio load callback for " + load.Path + " failed: " + e.Message); }
        }
    }
}
