using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    /// <summary>Save/load pieces of the title and pause menus (Wave A split; B04 provides an ISaveMenu, B10 calls it): load start, latest save, Continue, the save library and the legacy 10-slot screens used only without the WW save manager.</summary>
    internal sealed partial class Menu
    {
        private bool BeginLoad(string path)
        {
            if (Core.SaveSystem.RecoveryError != null) { Confirm(Core.SaveSystem.RecoveryError, null); return false; }
            _loadingScreen.Begin();
            if (Click(path)) return true;
            _loadingScreen.End(); return false;
        }

        private static string SaveFile(int n) { return Path.Combine(Application.persistentDataPath, "SaveGame" + n + ".es3"); }
        private static bool SlotUsed(int n) { var f = new FileInfo(SaveFile(n)); return f.Exists && f.Length > 1024; }

        private string _latestSaveFile;
        private string LatestSave(out int slot)
        {
            _latestSaveFile = Core.SaveRuntime.Latest(); slot = _latestSaveFile == null ? 0 : 1;
            if (_latestSaveFile == null) return "";
            string name = Core.SaveSystem.Info(_latestSaveFile).Name;
            return name.Length > 26 ? name.Substring(0, 25) + "…" : name;
        }

        private static readonly Regex SeedPart = new Regex(@"\s*\(Seed:[^)]*\)", RegexOptions.Compiled);
        private string SlotLabel(int n, bool shortForm)
        {
            string s = null;
            var path = _ctx == Ctx.Title ? "LoadGame_Canvas/LoadGame/load slot " + n + "/load " + n + " time" : "SaveGame_Canvas/SaveGame/save slot " + n + "/save " + n + " time";
            var t = SettingsModel.FindPath(path);
            var tx = t != null ? t.GetComponent<Text>() : null;
            if (tx != null && !string.IsNullOrEmpty(tx.text)) s = tx.text;
            if (string.IsNullOrEmpty(s)) s = SettingsModel.ReadString("saveTime" + n);
            if (string.IsNullOrEmpty(s)) s = SlotUsed(n) ? File.GetLastWriteTime(SaveFile(n)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
            s = SeedPart.Replace(s ?? "", "").Trim().ToUpperInvariant();
            if (shortForm && s.Length > 26) s = s.Substring(0, 25) + "…";
            return s;
        }

        private void ContinueLatest()
        { int slot; LatestSave(out slot); if (_latestSaveFile != null) Core.SinglePlayerSaving.Load(_latestSaveFile); }

        private void OpenSave()
        { if (Core.SaveSystem.Installed) WildWasteland.Modules.Saves.SaveLibraryPage.Open(true); else Confirm(Core.SaveSystem.RecoveryError ?? "The save manager is unavailable. Check the log.", null); }

        // ---------------------------------------------------------------- load / save slots
        private void ShowSlots(bool load)
        {
            if (Core.SaveSystem.Installed) { WildWasteland.Modules.Saves.SaveLibraryPage.Open(!load); return; }
            ResetToMain();
            _screen = load ? "load" : "save";
            _main.gameObject.SetActive(false);
            _sub = R(_rootRt, _screen, 0, 0, 1920, 1080);
            const float X = 120, Y = 280, W = 860, RH = 52;
            Plate(_sub, X, Y, W, 84 + 10 * RH + 16 + 64 + 24);
            T(_sub, "head", X + 30, Y + 18, W - 60, 50, 40, TextAnchor.MiddleLeft, Gold, load ? "LOAD GAME" : "SAVE GAME");
            float y = Y + 84;
            for (int n = 1; n <= 10; n++)
            {
                int slot = n;
                bool used = SlotUsed(n);
                string label = used ? SlotLabel(n, false) : "EMPTY";
                GlyphText t;
                var b = Row(_sub, X + 8, y, W - 16, RH, " " + n.ToString(CultureInfo.InvariantCulture), 30, () =>
                {
                    if (load) { if (BeginLoad("LoadGame_Canvas/LoadGame/load slot " + slot + "/load_game_" + slot)) ResetToMain(); }
                    else if (SlotUsed(slot)) Confirm("OVERWRITE SLOT " + slot + "?", () => SaveTo(slot));
                    else SaveTo(slot);
                }, out t);
                var lt = T(b.transform, "name", 90, 0, W - 16 - 110, RH, 26, TextAnchor.MiddleLeft, used ? White : Dim, label);
                var bt = b;
                var baseVisual = b.OnVisual;
                b.OnVisual = on => { baseVisual(on); lt.color = !bt.interactable ? Dim : on ? Gold : (used ? White : Dim); };
                if (load && !used) SetEnabled(b, false);
                b.Refresh();
                y += RH;
            }
            Row(_sub, X + 8, y + 16, 300, 64, "BACK", 34, Back);
            _nextFrame.Add(() => SelectFirst(_sub));
        }

        private void SaveTo(int slot)
        {
            if (Click("SaveGame_Canvas/SaveGame/save slot " + slot + "/save_game_" + slot)) Back();
        }
    }
}
