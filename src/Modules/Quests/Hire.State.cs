using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    internal static partial class Hire
    {
        /// <summary>Projects the live hire contract without transferring mutation rights to presentation consumers.</summary>
        internal sealed class StateApi : IQuestHireState
        {
            /// <summary>Whether a hired native actor is currently available.</summary>
            public bool Active { get { return Hire.Active && npc != null; } }
            /// <summary>Native hired actor.</summary>
            public GameObject Actor { get { return npc; } }
            /// <summary>Current hired actor combat target.</summary>
            public GameObject Target { get { return target; } }
            /// <summary>Current mounted state after crew admission.</summary>
            public bool Mounted { get { return mounted; } }
            /// <summary>Existing reload completion time.</summary>
            public float ReloadUntil { get { return reloadUntil; } }
            /// <summary>Existing weapon capacity used for per-round reload presentation.</summary>
            public int MagazineSize { get { return magSize; } }
        }
    }
}
