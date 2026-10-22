using System;
using WildWasteland.Core;

namespace WildWasteland.Modules.Saves
{
    /// <summary>Owns named singleplayer saves, transaction recovery and autosave admission.</summary>
    public sealed partial class SavesModule : ModuleBase
    {
        /// <summary>Settings, lifecycle and service owner for the save framework.</summary>
        public override string Id { get { return "Saves"; } }
        /// <summary>Name displayed by the module shell.</summary>
        public override string DisplayName { get { return "Saves"; } }
        bool _running, _recovering;

        /// <summary>Binds save and cleanup policy even when the module is disabled.</summary>
        public override void BindConfig(ModuleConfig cfg)
        {
            SaveSystem.Bind(cfg);
            // ModuleHost attaches its toggle handler after BindConfig: restore the switch before it can stop a live transaction.
            cfg.Enabled.SettingChanged += EnabledChanged;
        }
        /// <summary>Rejects runtime disabling while save/load work still owns native state.</summary>
        void EnabledChanged(object sender, EventArgs args)
        {
            if (!Config.Enabled.Value && _running && Busy)
            {
                Config.Enabled.Value = true;
                SaveSystem.Notify("Finish saving or loading before disabling Saves.");
            }
        }
        /// <summary>Starts native adapters and owner-scoped save protection, then recovers interrupted files.</summary>
        public override void Init()
        {
            SaveRuntime.Install(this);
            RegisterMenuActions();
            SaveSystem.Install(this);
            SaveBus.FailureReported += FailureReported;
            ModEvents.Subscribe<Core.Events.CombatActivity>(this, CombatActivity);
            ModEvents.Subscribe<Core.Events.WeaponFired>(this, WeaponFired);
            ModEvents.Subscribe<Core.Events.NpcStateChanged>(this, NpcStateChanged);
            _recovering = true;
            try { SaveSystem.RecoverFiles(); }
            finally { _recovering = false; }
            _running = true;
        }
        /// <summary>Provides the real save state only while the framework is enabled.</summary>
        public override void ProvideApis() { ModApi.Provide<ISaves>(this, this); }
        /// <summary>Unsubscribes and disposes all work, restoring native barriers even after partial initialization.</summary>
        public override void Shutdown()
        {
            _running = false;
            SaveBus.FailureReported -= FailureReported;
            ModEvents.Unsubscribe<Core.Events.CombatActivity>(this, CombatActivity);
            ModEvents.Unsubscribe<Core.Events.WeaponFired>(this, WeaponFired);
            ModEvents.Unsubscribe<Core.Events.NpcStateChanged>(this, NpcStateChanged);
            try { SaveRoutines.StopAll(); }
            finally { try { SaveSystem.Shutdown(); } finally { SaveRuntime.Shutdown(); _recovering = false; } }
        }
        /// <summary>Defers autosaving after typed weapon activity, including while custom NPC AI is disabled.</summary>
        static void WeaponFired(ref Core.Events.WeaponFired shot) { SaveCombat.Activity(); }
        /// <summary>Defers autosaving when a typed NPC transition enters combat.</summary>
        static void NpcStateChanged(ref Core.Events.NpcStateChanged change) { if (change.New == NpcMode.Combat) SaveCombat.Activity(); }
        /// <summary>Transfers sidecar failure into the current transaction's failure latch.</summary>
        static void FailureReported(IModule owner, string message) { SaveSystem.Fail(message); }
        /// <summary>Extends autosave combat deferral for activity from other modules.</summary>
        static void CombatActivity(ref Core.Events.CombatActivity activity) { SaveCombat.Activity(); }
    }
}
