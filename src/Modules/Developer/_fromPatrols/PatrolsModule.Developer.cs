using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols entry part owned by Developer: turret spot, bus passenger pose and merchant item editors (components, jobs, hotkeys) (Wave A split of PatrolsModule.cs).</summary>
    public sealed partial class PatrolsModule
    {
        TurretSpotter _spotter; BusPassengerPoseEditor _busPose; MerchantEditor _merchantEditor;

        internal TurretSpotter Spotter { get { return _spotter; } }
        internal BusPassengerPoseEditor BusPose { get { return _busPose; } }
        internal MerchantEditor MerchantSpot { get { return _merchantEditor; } }

        /// <summary>Adds the turret spot and bus passenger pose editors to the runner object (old component order).</summary>
        partial void AddDeveloperSeatComponents()
        {
            _spotter = _host.AddComponent<TurretSpotter>();
            _busPose = _host.AddComponent<BusPassengerPoseEditor>();
        }

        /// <summary>Adds the merchant item editor to the runner object (last, old component order).</summary>
        partial void AddDeveloperMerchantComponent() { _merchantEditor = _host.AddComponent<MerchantEditor>(); }

        /// <summary>Drops the numpad editor component references and the bus pose target on Shutdown.</summary>
        partial void ShutdownDeveloper()
        {
            _spotter = null; _busPose = null; _merchantEditor = null;
            BusPassengerPoseEditor.Target = null;
        }

        /// <summary>Registers the turret spot / bus pose editor Update jobs before Convoy.Tick (old job order).</summary>
        partial void RegisterDeveloperSeatTicks()
        {
            Scheduler.EveryFrame(this, "TurretSpotter.Tick", () => { if (_spotter != null) _spotter.Tick(); });
            Scheduler.EveryFrame(this, "BusPassengerPoseEditor.Tick", () => { if (_busPose != null) _busPose.Tick(); });
        }

        /// <summary>Registers the merchant editor Update job after Cleanup.Tick (old job order).</summary>
        partial void RegisterDeveloperMerchantTick()
        {
            Scheduler.EveryFrame(this, "MerchantEditor.Tick", () => { if (_merchantEditor != null) _merchantEditor.Tick(); });
        }

        /// <summary>Registers the turret spot / bus pose editor Gui jobs before WorldTraffic.Gui (old order).</summary>
        partial void RegisterDeveloperSeatGui()
        {
            Scheduler.Gui(this, "TurretSpotter.Gui", () => { if (_spotter != null) _spotter.Gui(); }, () => _spotter != null && _spotter.GuiWanted());
            Scheduler.Gui(this, "BusPassengerPoseEditor.Gui", () => { if (_busPose != null) _busPose.Gui(); }, () => _busPose != null && BusPassengerPoseEditor.Active);
        }

        /// <summary>Registers the merchant editor Gui job last (old order).</summary>
        partial void RegisterDeveloperMerchantGui()
        {
            Scheduler.Gui(this, "MerchantEditor.Gui", () => { if (_merchantEditor != null) _merchantEditor.Gui(); }, () => _merchantEditor != null && MerchantEditor.Enabled);
        }

        /// <summary>Registers the bus passenger pose and merchant item editor toggle hotkeys.</summary>
        partial void RegisterDeveloperHotkeys()
        {
            Hotkeys.Register(this, BusPassengerPoseEditorKey, () => BusPassengerPoseEditorEnabled.Value = !BusPassengerPoseEditorEnabled.Value);
            Hotkeys.Register(this, MerchantEditorKey, () => { MerchantEditorEnabled.Value = !MerchantEditorEnabled.Value; Log.Info("[Patrols] Merchant editor " + (MerchantEditorEnabled.Value ? "enabled" : "disabled")); });
        }
    }
}
