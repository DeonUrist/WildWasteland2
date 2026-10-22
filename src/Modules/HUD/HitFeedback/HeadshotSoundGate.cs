namespace WildWasteland.Modules.HUD
{
    // Pure timing policy: reserve two voices without ever interrupting a playing clip.
    // WW: MinimumInterval (0.075 s in 1.2.2) is [HUD / Headshot sound] MinInterval in tools.cfg.
    internal sealed class HeadshotSoundGate
    {
        internal static float MinimumInterval = 0.075f;
        private readonly float[] _ends = { float.NegativeInfinity, float.NegativeInfinity };
        private float _last = float.NegativeInfinity;
        /// <summary>Chooses a free or oldest voice slot and records its playback deadline.</summary>
        internal int Acquire(float now, float duration)
        {
            if (now - _last < MinimumInterval) return -1;
            for (int i = 0; i < _ends.Length; i++)
                if (now >= _ends[i]) { _ends[i] = now + duration; _last = now; return i; }
            return -1;
        }
        /// <summary>Clears transient headshot audio voice pool state so the next scene or session starts cleanly.</summary>
        internal void Reset()
        { _last = float.NegativeInfinity; for (int i = 0; i < _ends.Length; i++) _ends[i] = float.NegativeInfinity; }
    }
}
