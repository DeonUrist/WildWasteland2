using System;

namespace WildWasteland.Modules.Guns
{
    internal struct Magazine
    {
        internal int Rounds;
        internal bool Hammer;
        internal Magazine(int rounds, bool hammer)
        { Rounds = Math.Max(0, rounds); Hammer = Rounds > 0 && hammer; }
    }
}
