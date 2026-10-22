using System;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Former 16-bit PCM WAV loader, merged into Core <see cref="Audio.LoadWav"/> (W0-B; same RMS gain/soft clip/log, broader
    /// decoder accepted by the coordinator 2026-10-22). Kept as a forwarder while PlayerBody FemalePain calls it (A2/B05 switch;
    /// Wave C deletes this file).
    /// </summary>
    internal static class VoiceAudio
    {
        /// <summary>Forwards to <see cref="Audio.LoadWav"/>: kept-loaded clip, optional RMS normalisation (gain at most 8).</summary>
        [Obsolete("refactor: use Core Audio.LoadWav")]
        internal static AudioClip LoadWav(string path, string name, float targetRms = 0f) { return Audio.LoadWav(path, name, targetRms); }
    }
}
