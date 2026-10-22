using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Former NPCAI part of the old Guns entry (Wave A split). Empty since B07: the human boss health (config [NPCAI] BossHealth /
    /// [NPCAI / Bosses], detection hook, CreateObject registration, "NPCAI.Bosses" job, F7 page) is run by NPCAIModule. Wave C deletes it.
    /// </summary>
    public sealed partial class GunsModule
    {
        /// <summary>Empty (B07): [NPCAI] BossHealth is bound by NpcaiSettings.</summary>
        partial void BindNpcaiToggles(ModuleConfig cfg) { }
        /// <summary>Empty (B07): [NPCAI / Bosses] AdjustHumanBossHP is bound by NpcaiSettings.</summary>
        partial void BindNpcaiValues(ModuleConfig cfg) { }
        /// <summary>Empty (B07): NPCAIModule subscribes the boss detection observer.</summary>
        partial void InitNpcaiHooks() { }
        /// <summary>Empty (B07): NPCAIModule registers the "NPCAI.Bosses" job.</summary>
        partial void InitNpcaiJobs() { }
        /// <summary>Empty (B07): NPCAIModule resets the boss registry.</summary>
        static partial void ResetNpcai() { }
        /// <summary>Empty (B07): NPCAIModule registers spawned bosses from its own CreateObject postfix.</summary>
        static partial void BossCreated(UnityEngine.GameObject prefab, UnityEngine.GameObject made) { }
    }
}
