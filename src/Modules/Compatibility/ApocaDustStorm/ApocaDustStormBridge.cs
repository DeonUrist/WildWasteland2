using BepInEx.Bootstrap;
using WildWasteland.Core;
using WildWasteland.Modules.Compatibility;
using WildWasteland.Modules.Compatibility.WarRig;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary>
    /// The single ApocaDustStorm (local.apocalypter.duststorm) bridge: one detection shared by its two parts - the War Rig shelter patches
    /// (<see cref="DustStormBridge"/>: the rig's tank is complete storm shelter) installed under the Compatibility.ApocaDustStorm scope,
    /// and the NPC storm-senses reader (Storm.DustStorm.cs: dust storms count as sandstorms for NPC sight and hearing).
    /// </summary>
    internal static class ApocaDustStormBridge
    {
        /// <summary>BepInEx GUID of ApocaDustStorm.</summary>
        internal const string Guid = "local.apocalypter.duststorm";
        private static bool _enabled;
        private static IModule _scope;

        /// <summary>True while the bridge switch is on (the NPC storm reader only resolves the mod while it is).</summary>
        internal static bool Enabled { get { return _enabled; } }

        /// <summary>ApocaDustStorm's plugin info when it is loaded and the bridge is on, else null.</summary>
        internal static BepInEx.PluginInfo Plugin
        {
            get
            {
                BepInEx.PluginInfo info;
                return _enabled && Chainloader.PluginInfos.TryGetValue(Guid, out info) && !ReferenceEquals(info.Instance, null) ? info : null;
            }
        }

        /// <summary>Installs the War Rig shelter patches under <paramref name="scope"/> and enables the NPC reader.</summary>
        internal static void Start(IModule scope)
        {
            _enabled = true; _scope=scope;ExternalStormField.Clear();
            ModApi.Provide<IExternalStormField>(scope,new ExternalStormField());
            DustStormBridge.Install(scope, Plugin);
        }

        /// <summary>Disables both parts (the NPC reader re-resolves when switched on again; the scope removes the patches).</summary>
        internal static void Shutdown() { _enabled = false; if(_scope!=null)ModApi.Withdraw<IExternalStormField>(_scope);_scope=null; DustStormBridge.Shutdown(); ExternalStormField.Clear(); }
    }
}
