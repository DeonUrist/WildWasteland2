using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols settings of the patrol editor and template recorder (keys, AllowDebugSpawns); Wave A split of PatrolsSettings.cs.</summary>
    public sealed partial class PatrolsModule
    {
        internal static ConfigEntry<Key> MenuKey, TemplateDumpKey;
        internal static ConfigEntry<bool> AllowDebugSpawns;

        /// <summary>Binds the [Keys] entries of the patrol editor and the template recorder.</summary>
        partial void BindToolsKeys(ModuleConfig cfg)
        {
            MenuKey = cfg.Hotkey("Patrol editor", Key.None, "Patrols: open the patrol / convoy / merchant / loot editor (also F7 > Patrols & vehicles > Patrol editor). None = unbound");
            TemplateDumpKey = cfg.Hotkey("Template recorder", Key.None, "Patrols: save the vehicle you sit in as a template (only while seated; also F7 > Template recorder). None = unbound");
        }

        /// <summary>Binds the tools.cfg Debug entry AllowDebugSpawns (patrol editor spawn controls).</summary>
        partial void BindToolsDebug(ModuleConfig cfg)
        {
            AllowDebugSpawns = cfg.Tool(TDebug, "AllowDebugSpawns", true, "Show Spawn Template, S group buttons and SPAWN in the patrol editor");
        }
    }
}
