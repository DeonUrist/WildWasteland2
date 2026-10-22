using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// Running state of the module a bridge serves (the "host": the HUD for icon overrides, NPC AI for fear, the player body for
    /// shadows), read through Core's ModuleHost by module id so Compatibility never references another module's type. A bridge only
    /// acts while its host runs, exactly as when the host installed it. Ids are tried in order: the current (pre-Wave C) entry id first,
    /// then the final module id, so the check keeps working when Wave C merges the old entries.
    /// </summary>
    internal sealed class CompatibilityHost
    {
        private readonly string[] _ids;
        private ModuleHost.Entry _entry;
        private bool _resolved;

        /// <summary>Creates a host check for the first registered module among <paramref name="ids"/>.</summary>
        internal CompatibilityHost(params string[] ids) { _ids = ids; }

        /// <summary>True while the host module runs (one cached entry reference, no allocation).</summary>
        internal bool Running
        {
            get
            {
                if (!_resolved) Resolve();
                return _entry != null && _entry.State == ModuleState.Running;
            }
        }

        /// <summary>Finds the host entry once; modules are registered before any module initializes, so the result is final.</summary>
        private void Resolve()
        {
            _resolved = true;
            var entries = ModuleHost.Entries;
            foreach (var id in _ids)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Module != null && entries[i].Module.Id == id) { _entry = entries[i]; return; }
        }

        /// <summary>Old player-body entry (Presentation after Wave C).</summary>
        internal static readonly CompatibilityHost PlayerBody = new CompatibilityHost("PlayerBody", "Presentation");
        /// <summary>Old NPC entry (NPCAI after Wave C).</summary>
        internal static readonly CompatibilityHost Npc = new CompatibilityHost("NPC", "NPCAI");
        /// <summary>Old Guns entry (Gunplay after Wave C).</summary>
        internal static readonly CompatibilityHost Guns = new CompatibilityHost("Guns", "Gunplay");
        /// <summary>Old CleanHUD entry (HUD after Wave C).</summary>
        internal static readonly CompatibilityHost CleanHud = new CompatibilityHost("CleanHUD", "HUD");
        /// <summary>Patrols entry (it still installs the War Rig and vehicle parts).</summary>
        internal static readonly CompatibilityHost Patrols = new CompatibilityHost("Patrols");
        /// <summary>Survival entry (Stew Pot).</summary>
        internal static readonly CompatibilityHost Survival = new CompatibilityHost("Survival");
        /// <summary>Quests entry (Apocatalker conversation bridge).</summary>
        internal static readonly CompatibilityHost Quests = new CompatibilityHost("Quests");
    }
}
