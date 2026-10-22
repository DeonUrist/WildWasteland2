using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Presentation-owned voice source; NPCAI supplies shout intent and volume.</summary>
    public interface INpcVoice { bool Shout(float volume); }
    /// <summary>Presentation-owned reload audio driven by the NPC reload clock.</summary>
    public interface INpcReloadAudio { void Tick(string weapon,Vector3 at,float until,float length,ref int played,float now); }
}
