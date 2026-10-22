using System;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// Config of the old NPC entry shell (B07): binds the [NPC / ...] entries of the parts other packages still own here (Presentation:
    /// Women of Wasteland, dog animation toggles, reload sounds, aim pose, Apocaplayer clips; Tools: the bake highlight) through the
    /// partial bind methods, and keeps [Obsolete] forwarders to the NPCAI entries (now <see cref="NpcaiSettings"/>, [NPCAI] sections) that
    /// code outside NPCAI still reads. Wave C deletes the shell.
    /// </summary>
    public sealed partial class NpcModule
    {
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.AimEnabled"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.AimEnabled)")] internal static ConfigEntry<bool> AimEnabled { get { return NpcaiSettings.AimEnabled; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.BrainEnabled"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.BrainEnabled)")] internal static ConfigEntry<bool> BrainEnabled { get { return NpcaiSettings.BrainEnabled; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.BurrowFxPitch"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.BurrowFxPitch)")] internal static ConfigEntry<float> BurrowFxPitch { get { return NpcaiSettings.BurrowFxPitch; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.BurrowFxRange"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.BurrowFxRange)")] internal static ConfigEntry<float> BurrowFxRange { get { return NpcaiSettings.BurrowFxRange; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.BurrowFxScale"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.BurrowFxScale)")] internal static ConfigEntry<float> BurrowFxScale { get { return NpcaiSettings.BurrowFxScale; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.BurrowFxVolume"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.BurrowFxVolume)")] internal static ConfigEntry<float> BurrowFxVolume { get { return NpcaiSettings.BurrowFxVolume; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.DogBiteEnabled"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.DogBiteEnabled)")] internal static ConfigEntry<bool> DogBiteEnabled { get { return NpcaiSettings.DogBiteEnabled; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.DogCompanionCompatEnabled"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.DogCompanionCompatEnabled)")] internal static ConfigEntry<bool> DogCompanionCompatEnabled { get { return NpcaiSettings.DogCompanionCompatEnabled; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.DogIdlePoseMax"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.DogIdlePoseMax)")] internal static ConfigEntry<float> DogIdlePoseMax { get { return NpcaiSettings.DogIdlePoseMax; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.DogIdlePoseMin"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.DogIdlePoseMin)")] internal static ConfigEntry<float> DogIdlePoseMin { get { return NpcaiSettings.DogIdlePoseMin; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.DogJumpAttack"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.DogJumpAttack)")] internal static ConfigEntry<bool> DogJumpAttack { get { return NpcaiSettings.DogJumpAttack; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.FriendsPassThrough"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.FriendsPassThrough)")] internal static ConfigEntry<bool> FriendsPassThrough { get { return NpcaiSettings.FriendsPassThrough; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.HuntFxPitch"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.HuntFxPitch)")] internal static ConfigEntry<float> HuntFxPitch { get { return NpcaiSettings.HuntFxPitch; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.HuntFxScale"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.HuntFxScale)")] internal static ConfigEntry<float> HuntFxScale { get { return NpcaiSettings.HuntFxScale; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.HuntFxVolume"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.HuntFxVolume)")] internal static ConfigEntry<float> HuntFxVolume { get { return NpcaiSettings.HuntFxVolume; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.MagazineTable"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.MagazineTable)")] internal static ConfigEntry<string> MagazineTable { get { return NpcaiSettings.MagazineTable; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.Magazines"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.Magazines)")] internal static ConfigEntry<bool> Magazines { get { return NpcaiSettings.Magazines; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.MaxDistance"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.MaxDistance)")] internal static ConfigEntry<float> MaxDistance { get { return NpcaiSettings.MaxDistance; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.NavBakeBudgetMs"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.NavBakeBudgetMs)")] internal static ConfigEntry<float> NavBakeBudgetMs { get { return NpcaiSettings.NavBakeBudgetMs; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.NavBakeRange"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.NavBakeRange)")] internal static ConfigEntry<float> NavBakeRange { get { return NpcaiSettings.NavBakeRange; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.NavCellSize"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.NavCellSize)")] internal static ConfigEntry<float> NavCellSize { get { return NpcaiSettings.NavCellSize; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.NavFieldSeconds"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.NavFieldSeconds)")] internal static ConfigEntry<float> NavFieldSeconds { get { return NpcaiSettings.NavFieldSeconds; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.NavMargin"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.NavMargin)")] internal static ConfigEntry<float> NavMargin { get { return NpcaiSettings.NavMargin; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.NavMaxStep"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.NavMaxStep)")] internal static ConfigEntry<float> NavMaxStep { get { return NpcaiSettings.NavMaxStep; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.ReloadSeconds"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.ReloadSeconds)")] internal static ConfigEntry<float> ReloadSeconds { get { return NpcaiSettings.ReloadSeconds; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.RoundSeconds"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.RoundSeconds)")] internal static ConfigEntry<float> RoundSeconds { get { return NpcaiSettings.RoundSeconds; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.SingleShotReload"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.SingleShotReload)")] internal static ConfigEntry<float> SingleShotReload { get { return NpcaiSettings.SingleShotReload; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.StormRadius"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.StormRadius)")] internal static ConfigEntry<float> StormRadius { get { return NpcaiSettings.StormRadius; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.TauntMax"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.TauntMax)")] internal static ConfigEntry<float> TauntMax { get { return NpcaiSettings.TauntMax; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.TauntMin"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.TauntMin)")] internal static ConfigEntry<float> TauntMin { get { return NpcaiSettings.TauntMin; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.TauntRange"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.TauntRange)")] internal static ConfigEntry<float> TauntRange { get { return NpcaiSettings.TauntRange; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.ShoutKey"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.ShoutKey)")] internal static ConfigEntry<Key> ShoutKey { get { return NpcaiSettings.ShoutKey; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)
        /// <summary>[Obsolete] Forwarder to <see cref="NpcaiSettings.ShoutModifier"/> ([NPCAI] config) for readers outside NPCAI.</summary>
        [Obsolete("refactor: NPCAI config (NpcaiSettings.ShoutModifier)")] internal static ConfigEntry<Key> ShoutModifier { get { return NpcaiSettings.ShoutModifier; } }   // refs-ok: [Obsolete] forwarder to NPCAI (Wave C deletes the NPC shell)

        private static ModuleConfig _cfg;
        /// <summary>Binds a tools.cfg entry in [NPC / section] (Presentation part).</summary>
        private static ConfigEntry<T> H<T>(string section, string key, T value, ConfigDescription description)
        {
            return _cfg.Tools.Bind(_cfg.Section(section), key, value, description);
        }
        /// <summary>H with a plain description.</summary>
        private static ConfigEntry<T> H<T>(string section, string key, T value, string description) { return H(section, key, value, new ConfigDescription(description)); }
        /// <summary>Binds a settings.cfg entry in [NPC] / [NPC / sub].</summary>
        private static ConfigEntry<T> V<T>(string sub, string key, T value, ConfigDescription description)
        {
            return _cfg.Main.Bind(_cfg.Section(sub), key, value, description);
        }
        /// <summary>V with a plain description.</summary>
        private static ConfigEntry<T> V<T>(string sub, string key, T value, string description) { return V(sub, key, value, new ConfigDescription(description)); }
        /// <summary>Binds an F6 toggle in [NPC] (Presentation part).</summary>
        private static ConfigEntry<bool> B(string key, bool value, string description)
        {
            return _cfg.Main.Bind(_cfg.Section(null), key, value, description);
        }

        // ---- binds owned by other target modules (Wave A): called in their old relative order.
        /// <summary>Presentation part of BindConfig: Women of Wasteland entries.</summary>
        static partial void BindWomen(ModuleConfig cfg);
        /// <summary>Presentation: the F6 toggles DogAnimations and DogsLieDown.</summary>
        static partial void BindPresentationToggles();
        /// <summary>Presentation: [NPC / Behaviour] DogLieFrontLift.</summary>
        static partial void BindPresentationDogPose();
        /// <summary>Presentation: [NPC / NpcAim] ReloadVolume and ReloadSoundRange.</summary>
        static partial void BindPresentationReload();
        /// <summary>Presentation: [NPC / Brain] AimPose.</summary>
        static partial void BindPresentationAimPose();
        /// <summary>Presentation: [NPC / Brain] ApocaplayerClips.</summary>
        static partial void BindPresentationClips();
        /// <summary>Tools owns the navigation bake collection aid setting.</summary>
        static partial void BindNavigationDiagnostics();

        /// <summary>Binds the remaining parts' entries in their old relative order (the NPCAI entries are bound by NPCAIModule).</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            _cfg = cfg;
            BindNavigationDiagnostics();
            BindWomen(cfg);
            BindPresentationToggles();
            BindPresentationDogPose();
            BindPresentationReload();
            BindPresentationAimPose();
            BindPresentationClips();
        }
    }
}
