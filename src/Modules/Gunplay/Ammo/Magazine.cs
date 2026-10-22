using System;

namespace WildWasteland.Modules.Guns
{
    internal struct Magazine
    {
        internal int Rounds;
        internal bool Hammer;
        /// <summary>A magazine of <paramref name="rounds"/> (never negative); the hammer is only cocked with rounds in it.</summary>
        internal Magazine(int rounds, bool hammer)
        { Rounds = Math.Max(0, rounds); Hammer = Rounds > 0 && hammer; }
    }
}
