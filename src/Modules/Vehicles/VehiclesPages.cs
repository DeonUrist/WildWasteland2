using System;
using System.IO;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WarRig = WildWasteland.Modules.Vehicles.WarRig;   // refs-ok: Vehicles-owned namespace until the Wave C rename

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>F3 tool pages of the Vehicles module (the War Rig page moved from the Patrols entry).</summary>
    internal static class VehiclesPages
    {
        private static string _rigStatus = "";

        /// <summary>War Rig page: spawn / recovery switches, model and template status, spawn and nearest-rig layout/visual tools.</summary>
        internal static void DrawWarRig(ToolLayout l)
        {
            l.Text("The War Rig: a six-wheel tanker semi-trailer for the Rustchief. The POI module places abandoned trucks with tankers, worn wheels, no engine and nearby zombies. World chance is in Points of interest. Temporary map markers are yellow Xs. The tanker shelters the player from sandstorms and remains available in truck templates and manual spawns.");
            l.Toggle(VehiclesSettings.WarRigSpawns, "War Rig spawns");
            l.Toggle(VehiclesSettings.WarRigGroundRecovery, "Ground recovery");
            l.Heading("Status");
            string model = Path.Combine(WarRig.WarRigFeature.Dir, WarRig.S.ModelFile);
            l.Value("Model", File.Exists(model) ? WarRig.S.ModelFile + " found" : WarRig.S.ModelFile + " MISSING in " + WarRig.WarRigFeature.Dir);
            l.Value("Template", WarRig.Factory.Cached != null ? "built" : "not built yet (first spawn / save load)");
            l.Value("Rigs loaded", WarRig.WarRigUnit.All.Count.ToString());
            var near = Nearest();
            if (near != null) l.Value("Nearest rig", near.name + "  (truck in reach: " + (string.IsNullOrEmpty(near.NearestTruck) ? "none" : near.NearestTruck) + ")");
            l.Heading("Tools");
            if (l.Button("Spawn a War Rig 14 m ahead", VehiclesModule.Running && GameRefs.InPlay && VehiclesSettings.WarRigSpawns.Value, true, 320))
                ToolWindow.CloseThen(SpawnAhead);
            int hit = l.Buttons("Re-apply layout (nearest)", "Rebind visuals (nearest)");
            if (hit >= 0 && near != null)
            {
                try
                {
                    if (hit == 0) { WarRig.Layout.Apply(near.gameObject, true); _rigStatus = "layout re-applied, leg " + WarRig.Layout.LastLegLength.ToString("0.00") + " m"; }
                    else { near.Rebind(); _rigStatus = "visuals rebound"; }
                }
                catch (Exception e) { _rigStatus = e.Message; }
            }
            if (_rigStatus.Length > 0) l.Value("Last action", _rigStatus);
        }

        /// <summary>Spawns a War Rig in front of the player after the tool window closed; failures go to the page status and the log.</summary>
        private static void SpawnAhead()
        {
            try { WarRig.WarRigFeature.SpawnNearPlayer(WarRig.S.EquipWheels); _rigStatus = "spawned"; }
            catch (Exception e) { _rigStatus = e.Message; Log.Warn(VehiclesModule.Instance, "War Rig: " + e.Message); }
        }

        /// <summary>The loaded War Rig nearest to the main camera (null when none).</summary>
        private static WarRig.WarRigUnit Nearest()
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
