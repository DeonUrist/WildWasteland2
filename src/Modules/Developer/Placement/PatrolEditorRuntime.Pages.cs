using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Developer
{
    /// <summary>F7 pages of the numpad editors: turret spots, bus passenger pose, merchant item placement (Wave A split of PatrolsPages.cs).</summary>
    internal sealed partial class PatrolEditorRuntime
    {
        // ---------------------------------------------------------------- Seat & turret poses

        /// <summary>Draws Developer turret placement controls and current native selection.</summary>
        void TurretSpotPage(ToolLayout l)
        {
            l.Text("Glowing markers on the human turret spots of the nearest raider car (within 80 m): yellow = the selected spot (the feet), orange = the other spots, green = its forward. Move it with the numpad in the game (F7 closed) and save it into the car's template file (riderPos / riderPos2 ...).");
            l.Toggle(TurretSpotEditor, "Turret spot editor on");
            l.Heading("Keys (in the game, F7 closed)");
            l.Text("Numpad 8 / 2 forward / back    4 / 6 left / right    7 / 9 down / up    Shift = x5\nNumpad 0 next turret    5 save the spot    . markers on / off");
            var s = Instance != null ? Instance.Spotter : null;
            l.Heading("Selection");
            if (Core.ModApi.Get<Core.ITurretPlacement>()==null || !TurretSpotEditor.Value || s == null) { l.Text("Switch the editor on and stand near a raider car with turret spots."); return; }
            if (BusPassengerPoseEditor.Active) l.Text("The bus passenger pose editor is on: it owns the numpad, the turret spot editor waits.", Ledger.Danger);
            if (s.Car == null) { l.Text("No raider car with a turret spot within 80 m."); if (l.Button("Search again", true, false, 220)) s.Refind(); return; }
            l.Value("Nearest car", s.Car.name + "  [" + Core.ModApi.Get<Core.ITurretPlacement>().TemplateName(s.Car) + "]");
            var a = s.SelectedAnchor;
            l.Value("Selected spot", s.SlotName + " (" + s.SelectedRider + ")");
            if (a != null) l.Value("Local position", a.localPosition.ToString("F3"));
            s.Markers = l.Toggle("Markers", s.Markers);
            int hit = l.Buttons("Next turret", "SAVE spot");
            if (hit == 0) s.NextSlot(); else if (hit == 1) s.SaveSpot();
            if (s.Status.Length > 0) l.Value("Status", s.Status);
        }

        /// <summary>Draws Developer bus passenger pose controls and current authoring values.</summary>
        void BusPosePage(ToolLayout l)
        {
            l.Text("Live arm, leg and seat adjustment for seated bus passengers (merchant buses, the War rig bus): the nearest one within 80 m is edited, the saved pose applies to every bus passenger and merchant type (BusPassengerPose.json).");
            l.Toggle(BusPassengerPoseEditorEnabled, "Bus passenger pose editor on");
            l.Value("Toggle key", KeyName(BusPassengerPoseEditorKey));
            l.Entry(BusPassengerPoseEditorKey);
            l.Heading("Keys (in the game, F7 closed)");
            l.Text("Num0 next joint    Num/ both (mirrored) / left / right    Shift faster\nNum8 / 2 bend    Num4 / 6 sideways    Num7 / 9 twist\nSeat position: Num8 / 2 forward / back    Num4 / 6 left / right    Num7 / 9 down / up\nNum5 save    Num. reset to defaults\nDisable the other numpad editors (turret spots, merchant items) while using this one.");
            var b = Instance != null ? Instance.BusPose : null;
            l.Heading("Selection");
            if (!BusPassengerPoseEditor.Active || b == null) { l.Text("Switch the editor on and stand near a seated bus passenger."); return; }
            l.Value("Passenger", BusPassengerPoseEditor.Target != null ? BusPassengerPoseEditor.Target.name : "none within 80 m (spawn a bus template with a passenger)");
            l.Value("Joint / side", b.JointName + " / " + b.SideName);
            l.Value("Value", b.ValueText);
            int hit = l.Buttons("Next joint", "Next side", "SAVE", "RESET");
            if (hit == 0) b.NextJoint(); else if (hit == 1) b.NextSide(); else if (hit == 2) b.SavePose(); else if (hit == 3) b.ResetPose();
            if (b.Status.Length > 0) l.Value("Status", b.Status);
        }

        /// <summary>Draws Developer merchant placement controls and current native selection.</summary>
        void MerchantSpotPage(ToolLayout l)
        {
            l.Text("Placement of the shop objects of the nearest merchant bus (within 80 m): the box, board, name and price labels and the sale items. The bus is held still while you edit; saving writes the layout to MerchantLayouts.json, shared by every merchant type of that template. (Absorbed debug MerchantSpotter.)");
            l.Toggle(MerchantEditorEnabled, "Merchant item editor on");
            l.Value("Toggle key", KeyName(MerchantEditorKey));
            l.Entry(MerchantEditorKey);
            l.Heading("Keys (in the game, F7 closed)");
            l.Text("Num/ object type    Num0 next object    Num5 save\nNum8 / 2 forward / back    Num4 / 6 sideways    Num7 / 9 down / up    Shift = x5    Num+ / Num- step size\nCtrl + Num4 / 6 yaw    Ctrl + Num7 / 9 pitch    Ctrl + Num8 / 2 roll\nDisable the turret spot editor while using this one.");
            var e = Instance != null ? Instance.MerchantSpot : null;
            l.Heading("Selection");
            if (Core.ModApi.Get<Core.IMerchantPlacement>()==null || !MerchantEditor.Enabled || e == null) { l.Text("Switch the editor on and stand near a merchant bus (spawn one from the patrol editor's MERCHANTS tab, not as an ordinary vehicle)."); return; }
            if (e.Car == null) { l.Text("No merchant bus within 80 m."); return; }
            l.Value("Bus", e.Car.name);
            l.Value("Object type", e.Kind);
            l.Value("Selected", e.Selected.Length > 0 ? e.Selected : "waiting for shop objects");
            l.Value("Step", e.StepSize.ToString("0.000") + " m");
            int hit = l.Buttons("Next type", "Next object", "Hold bus still", "SAVE layout");
            if (hit == 0) e.NextKind(); else if (hit == 1) e.NextObject(); else if (hit == 2) e.Freeze(); else if (hit == 3) e.SaveLayout();
            if (e.Status.Length > 0) l.Value("Status", e.Status);
        }
    }
}
