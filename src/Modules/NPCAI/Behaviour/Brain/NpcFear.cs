using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPCAI {
    /// <summary>Owns native fear decisions; optional weapons only supply contact intent.</summary>
    internal sealed class NpcFear : INpcFear {
        public void FireRetreat(GameObject actor,Vector3 source,bool burning)=>Brain.FireRetreat(actor,source,burning);
        public void ChainsawWitnesses(GameObject corpse)=>Brain.ChainsawWitnesses(corpse);
    }
}
