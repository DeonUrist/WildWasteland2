using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Apocasaver part of the Patrols Convoy (partial, called by Convoy spawning): probe of the standalone's autosave timer by
    /// reflection so spawns wait for its autosaves. README 9 / A4: Patrols (B11) replaces the call by the WW Saves query; since the session
    /// takeover disables the standalone runner it answers false in practice (question Q1 in reports/B17.md).</summary>
    internal partial class Convoy
    {
        // Apocasaver (optional, by reflection): true while it autosaves, or when its next autosave is due within `within` s. An autosave
        // that is overdue (it waits for the player to be on foot) does not block spawns - the builds themselves wait out a running save.
        private static bool _asResolved;
        private static FieldInfo _asCurrent, _asLast, _asArmed;
        private static PropertyInfo _asSaving;
        private static BepInEx.Configuration.ConfigEntry<float> _asInterval;
        private static BepInEx.Configuration.ConfigEntry<bool> _asEnabled;
        /// <summary>True while the standalone autosaves, or when its next autosave is due within <paramref name="within"/> seconds.</summary>
        internal static bool AutosaveSoon(float within)
        {
            try
            {
                if (!_asResolved)
                {
                    _asResolved = true;
                    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Apocasaver");
                    var plugin = asm != null ? asm.GetType("Apocasaver.Plugin") : null;
                    var runner = asm != null ? asm.GetType("Apocasaver.Runner") : null;
                    if (plugin == null || runner == null) return false;
                    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    _asCurrent = plugin.GetField("Current", S);
                    var iv = plugin.GetField("IntervalMinutes", S); _asInterval = iv != null ? iv.GetValue(null) as BepInEx.Configuration.ConfigEntry<float> : null;
                    var en = plugin.GetField("Enabled", S); _asEnabled = en != null ? en.GetValue(null) as BepInEx.Configuration.ConfigEntry<bool> : null;
                    _asLast = runner.GetField("_lastSave", I); _asArmed = runner.GetField("_armed", I); _asSaving = runner.GetProperty("IsAutosaving", I);
                    PatrolsModule.Verbose("Convoy: Apocasaver found, spawns wait for its autosaves" + (_asCurrent != null && _asLast != null && _asInterval != null ? "" : " (timer not readable)"));
                }
                if (_asCurrent == null) return false;
                var r = _asCurrent.GetValue(null) as UnityEngine.Object;
                if (r == null) return false;
                if (_asSaving != null && _asSaving.GetValue(r, null) is bool && (bool)_asSaving.GetValue(r, null)) return true;
                if (_asLast == null || _asInterval == null || (_asEnabled != null && !_asEnabled.Value)) return false;
                if (_asArmed != null && _asArmed.GetValue(r) is bool && !(bool)_asArmed.GetValue(r)) return false;
                float remaining = _asInterval.Value * 60f - (Time.realtimeSinceStartup - (float)_asLast.GetValue(r));
                return remaining > -3f && remaining < within;
            }
            catch (Exception e) { PatrolsModule.Verbose("Convoy: Apocasaver timer: " + e.Message); _asCurrent = null; return false; }
        }
    }
}
