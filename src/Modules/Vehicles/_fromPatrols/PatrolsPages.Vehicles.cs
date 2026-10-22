using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>F7 War Rig page (status, spawn, layout/visual tools) (Wave A split of PatrolsPages.cs).</summary>
    public sealed partial class PatrolsModule
    {
        static string _rigStatus = "";
        void WarRigPage(ToolLayout l)
        {
            l.Text("The War Rig: a six-wheel tanker semi-trailer for the Rustchief. The POI module places abandoned trucks with tankers, worn wheels, no engine and nearby zombies. World chance is in Points of interest. Temporary map markers are yellow Xs. The tanker shelters the player from sandstorms and remains available in truck templates and manual spawns.");
            l.Toggle(WarRig.S.SpawnEnabled, "War Rig spawns");
            l.Toggle(WarRig.S.GroundRecoveryEnabled, "Ground recovery");
            l.Heading("Status");
            l.Value("Model", File.Exists(Path.Combine(WarRig.WarRigFeature.Dir, WarRig.S.ModelFile)) ? WarRig.S.ModelFile + " found" : WarRig.S.ModelFile + " MISSING in " + WarRig.WarRigFeature.Dir);
            l.Value("Template", WarRig.Factory.Cached != null ? "built" : "not built yet (first spawn / save load)");
            l.Value("Rigs loaded", WarRig.WarRigUnit.All.Count.ToString());
            WarRig.WarRigUnit near = Nearest();
            if (near != null) l.Value("Nearest rig", near.name + "  (truck in reach: " + (string.IsNullOrEmpty(near.NearestTruck) ? "none" : near.NearestTruck) + ")");
            l.Heading("Tools");
            if (l.Button("Spawn a War Rig 14 m ahead", Running && InPlay && WarRig.S.SpawnEnabled.Value, true, 320))
                ToolWindow.CloseThen(() => { try { WarRig.WarRigFeature.SpawnNearPlayer(WarRig.S.EquipWheels); _rigStatus = "spawned"; } catch (Exception e) { _rigStatus = e.Message; WarRig.WarRigFeature.Say("War Rig: " + e.Message); } });
            int hit = l.Buttons("Re-apply layout (nearest)", "Rebind visuals (nearest)");
            if (hit >= 0 && near != null)
            {
                try { if (hit == 0) { WarRig.Layout.Apply(near.gameObject, true); _rigStatus = "layout re-applied, leg " + WarRig.Layout.LastLegLength.ToString("0.00") + " m"; } else { near.Rebind(); _rigStatus = "visuals rebound"; } }
                catch (Exception e) { _rigStatus = e.Message; }
            }
            if (_rigStatus.Length > 0) l.Value("Last action", _rigStatus);
        }
        static WarRig.WarRigUnit Nearest()
        {
            var cam = GameRefs.MainCamera; WarRig.WarRigUnit best = null; float bd = float.MaxValue;
            foreach (var r in WarRig.WarRigUnit.All)
            {
                if (r == null) continue;
                float d = cam != null ? (r.transform.position - cam.transform.position).sqrMagnitude : 0f;
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }
    }
}
