using BepInEx.Configuration;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): what the module draws on the HUD - vehicle status
    /// and hotkey hints, the TAB-screen character picture reaction, the Gui job. Target: HUD (B10).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        internal static ConfigEntry<bool> VehicleStatusHint, VehicleHotkeyHint;

        /// <summary>Binds [PlayerBody / Vehicles] VehicleStatusHint and VehicleHotkeyHint; hooks the TAB picture to ReplaceDriver.</summary>
        partial void BindHud(ModuleConfig cfg)
        {
            VehicleStatusHint = cfg.Player("Vehicles", "VehicleStatusHint", true, "Show ignition off, handbrake engaged and playing cassette with volume in the top right while driving.");
            VehicleHotkeyHint = cfg.Player("Vehicles", "VehicleHotkeyHint", true, "Show the vehicle hotkeys on the left while driving. Hiding the hints keeps the keys working.");
            ReplaceDriver.SettingChanged += (s, e) => { if (!Running) return; if (ReplaceDriver.Value) InventoryModel.Reset(); else InventoryModel.Off(); };
        }

        /// <summary>Init: the one IMGUI job (editor hints + vehicle HUD), only while one of them shows.</summary>
        partial void InitHud()
        {
            Scheduler.Gui(this, "Weapon pose hint + vehicle HUD", BodyRunner.Gui, BodyRunner.GuiWanted);   // (WW perf) OnGUI only while one of them shows
        }
    }
}
