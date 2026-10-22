using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Read-only hire contract state consumed by NPC animation without exposing the quest engine.</summary>
    public interface IQuestHireState
    {
        /// <summary>Whether a live hired companion is available.</summary>
        bool Active { get; }
        /// <summary>Current companion object, or null while its native saved object is restoring.</summary>
        GameObject Actor { get; }
        /// <summary>Current combat target, or null.</summary>
        GameObject Target { get; }
        /// <summary>Whether the companion is mounted.</summary>
        bool Mounted { get; }
        /// <summary>Gameplay time at which the current reload completes.</summary>
        float ReloadUntil { get; }
        /// <summary>Current weapon magazine capacity.</summary>
        int MagazineSize { get; }
    }

    /// <summary>NPCAI-owned companion body lifecycle and shot presentation.</summary>
    public interface IQuestFollowerBody
    {
        /// <summary>Updates body ownership and current pose intent.</summary>
        void Tick();
        /// <summary>Applies the final mounted body pose after seating.</summary>
        void Late();
        /// <summary>Releases the animation lease and restores native behavior.</summary>
        void Release();
        /// <summary>Shows a shot and its existing reload interval.</summary>
        void Shot();
        /// <summary>Plays a native state only when the shared body does not own this animator.</summary>
        void Play(Animator animator, string state, int layer, float time);
    }

    /// <summary>Optional external conversation flags, provided by the active Compatibility bridge.</summary>
    public interface IConversationFlags
    {
        /// <summary>Sets an external conversation flag when that API supports it.</summary>
        void Set(string name, bool value);
    }
}
