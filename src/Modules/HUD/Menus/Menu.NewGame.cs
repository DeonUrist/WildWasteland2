using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.HUD
{
    internal sealed partial class Menu
    {
        private readonly NewGameSetup _newGameSetup;
        private int _startVehicle, _startLoadout;
        private RectTransform _startOptions;
        private GlyphText _startVehicleText, _startLoadoutText, _startDescription;
        private static readonly string[] StartDescriptions = { "NOTHING ADDED TO YOUR STARTING GEAR", "SINGLE-SHOT .22 PIPE PISTOL + 30 ROUNDS", "SMALL BACKPACK + MACHETE", "FOLK 17 + 30 ROUNDS", "CROSSBOW + 12 BOLTS", "DOUBLE-BARREL SHOTGUN + 12 SHELLS", "AKM TRASH + 30 ROUNDS" };

        /// <summary>Keeps the title menu visible beside the new-game choices.</summary>
        private void ShowNewGame()
        {
            CloseModal(); _startVehicle = _startLoadout = 0;
            _modal = R(_rootRt, "new game", 0, 0, 1920, 1080);
            Core.ReplacementGuard.MenuModalOpen = true;
            var blocker = _modal.gameObject.AddComponent<UnityEngine.UI.Image>(); blocker.color = Color.clear;
            _modal.gameObject.AddComponent<ClickZone>().OnClick = CloseModal;
            const float X = 660, Y = 320, W = 680;
            var panel = R(_modal, "starting choices", X, Y, W, 440);
            panel.gameObject.AddComponent<ClickZone>();
            Plate(panel, 0, 0, W, 440);
            T(panel, "heading", 28, 18, W - 100, 48, 36, TextAnchor.MiddleLeft, Gold, "NEW GAME");
            CenteredRow(panel, W - 68, 18, 48, 48, "X", 28, CloseModal);
            T(panel, "vehicle heading", 28, 80, W - 56, 30, 24, TextAnchor.MiddleLeft, Dim, "STARTING VEHICLE");
            StartDropdown(panel, 112, NewGameSetup.Vehicles[0], () => ShowStartOptions(true), out _startVehicleText);
            T(panel, "loadout heading", 28, 185, W - 56, 30, 24, TextAnchor.MiddleLeft, Dim, "LOADOUT");
            StartDropdown(panel, 217, NewGameSetup.Loadouts[0], () => ShowStartOptions(false), out _startLoadoutText);
            _startDescription = T(panel, "description", 28, 285, W - 56, 48, 23, TextAnchor.MiddleLeft, White, StartDescriptions[0]);
            var start = CenteredRow(panel, 28, 350, W - 56, 64, "START GAME", 34, StartGame);
            Frame(start.transform, 0, 0, W - 56, 64, 1.5f, Gold);
            BlockCancelFrame = Time.frameCount + 1;
        }

        /// <summary>Uses the menu's existing glyph rows and accent frame for a dropdown field.</summary>
        private void StartDropdown(Transform panel, float y, string value, Action click, out GlyphText text)
        {
            var row = Row(panel, 28, y, 624, 52, value, 28, click, out text);
            Frame(row.transform, 0, 0, 624, 52, 1.5f, BarBorder);
            var arrow = R(row.transform, "arrow", 576, 0, 32, 52);
            for (int side = -1; side <= 1; side += 2)
            {
                var stroke = Img(arrow, side < 0 ? "left" : "right", 0, 0, 12, 2, Gold).rectTransform;
                stroke.pivot = new Vector2(.5f, .5f);
                stroke.anchoredPosition = new Vector2(16 + side * 4, -26);
                stroke.localRotation = Quaternion.Euler(0, 0, side * 45);
            }
        }

        /// <summary>Opens a glyph-based dropdown above the remaining panel controls.</summary>
        private void ShowStartOptions(bool vehicle)
        {
            CloseStartOptions();
            _startOptions = R(_modal, "starting options", 0, 0, 1920, 1080);
            var blocker = _startOptions.gameObject.AddComponent<UnityEngine.UI.Image>(); blocker.color = Color.clear;
            _startOptions.gameObject.AddComponent<ClickZone>().OnClick = CloseStartOptions;
            var choices = vehicle ? NewGameSetup.Vehicles : NewGameSetup.Loadouts;
            const float X = 688, W = 624, RH = 44;
            float y = 320 + (vehicle ? 164 : 269);
            var list = R(_startOptions, "list", X, y, W, choices.Length * RH + 16);
            list.gameObject.AddComponent<ClickZone>();
            Plate(list, 0, 0, W, choices.Length * RH + 16);
            for (int i = 0; i < choices.Length; i++)
            {
                int choice = i;
                var row = Row(list, 8, 8 + i * RH, W - 16, RH, choices[i], 27, () =>
                {
                    if (vehicle) { _startVehicle = choice; _startVehicleText.Set(choices[choice]); }
                    else { _startLoadout = choice; _startLoadoutText.Set(choices[choice]); _startDescription.Set(StartDescriptions[choice]); }
                    CloseStartOptions();
                });
                row.Sticky = i == (vehicle ? _startVehicle : _startLoadout); row.Refresh();
                if (vehicle && choices[i] == "MOTORCYCLE" && ModApi.Get<IVehicleAuthoring>() == null) SetEnabled(row, false);
            }
            BlockCancelFrame = Time.frameCount + 1;
        }

        /// <summary>Closes only the open dropdown, leaving the starting choices visible.</summary>
        private void CloseStartOptions()
        {
            if (_startOptions != null) { _startOptions.gameObject.SetActive(false); UnityEngine.Object.Destroy(_startOptions.gameObject); }
            _startOptions = null;
        }

        /// <summary>Arms one new world and then hands scene loading to the native button.</summary>
        private void StartGame()
        {
            if (_loadingScreen.Active) return;
            if (NewGameSetup.Vehicles[_startVehicle] == "MOTORCYCLE" && ModApi.Get<IVehicleAuthoring>() == null)
            { Confirm("ENABLE VEHICLES TO START WITH A MOTORCYCLE", null); return; }
            ApplySeed(); _newGameSetup.Arm(_startVehicle, _startLoadout);
            if (BeginLoad("Canvas_MM/MainMenu/NewGame")) CloseModal();
            else _newGameSetup.Clear();
        }
    }
}
