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

namespace WildWasteland.Modules.HUD
{
    // ------------------------------------------------------------------ settings model
    internal sealed class SettingDef
    {
        internal string Tab, Label, Key, Kind, Path, Fmt;
        internal string FsmName, OnState;      // kind "fsm": toggles an FSM between two states (god mode, god hands)
        internal float Min, Max, Def;
        internal bool TitleOnly, GameOnly;
    }

    internal static class SettingsModel
    {
        /// <summary>Resolves tabs for the native settings binding.</summary>
        internal static List<string> Tabs()
        {
            var l = new List<string> { "GRAPHICS", "AUDIO", "GAMEPLAY", "PLAYER & CAR", "CONTROLS" };
            if (HudModule.EnableCheats.Value) l.Add("CHEATS");
            return l;
        }
        internal static readonly List<SettingDef> Defs = new List<SettingDef>();
        /// <summary>Constructs the native settings binding and captures its owning resources.</summary>
        static SettingsModel()
        {
            // Graphics: display options (Settings.xml, applied with APPLY) + the game's SaveSettings.es3 keys (defaults = the game's UI defaults)
            D("GRAPHICS", "RESOLUTION", null, "res");
            D("GRAPHICS", "SCREEN MODE", null, "mode");
            D("GRAPHICS", "QUALITY", null, "quality");
            F("GRAPHICS", "FIELD OF VIEW", "FOV", 40, 120, 75, "Graphics_Canvas/Graphics/fov/FOV", "0");
            B("GRAPHICS", "AMBIENT OCCLUSION", "ambient_occlusion", true, "Graphics_Canvas/Graphics/ambient occlusion/ambient_occlusion");
            B("GRAPHICS", "BLOOM", "bloom", true, "Graphics_Canvas/Graphics/bloom/bloom");
            B("GRAPHICS", "COLOR GRADING", "color_grading", false, "Graphics_Canvas/Graphics/color grading/color_grading");
            B("GRAPHICS", "SCREEN SPACE SHADOWS", "screen_space_shadows", true, "Graphics_Canvas/Graphics/screen space shadows/screen_space_shadows");
            B("GRAPHICS", "SHARPEN", "sharpen", true, "Graphics_Canvas/Graphics/sharpen/sharpen");
            B("GRAPHICS", "SHOW FPS", "toggleFPS", false, "Graphics_Canvas/Graphics/show fps/show_fps");
            F("AUDIO", "MASTER VOLUME", "audio_volume", 0, 1, .3f, "Audio_Canvas/Audio/audio volume/audio_volume", "%");
            F("AUDIO", "AMBIENT MUSIC", "ambient_music", 0, 1, .3f, "Audio_Canvas/Audio/ambient music/ambient_music", "%");
            F("AUDIO", "AMBIENT SOUNDS", "ambient_volume", 0, 1, .15f, "Audio_Canvas/Audio/ambient volume/ambient_volume", "%");
            F("AUDIO", "BOSS FIGHT MUSIC", "boss_music", 0, 1, .3f, "Audio_Canvas/Audio/boss music/boss_music", "%");
            F("AUDIO", "ENGINE SOUND", "enginesoundvolume", 0, 1, .3f, "Audio_Canvas/Audio/engine sound volume/enginesoundvolume", "%");
            B("AUDIO", "FOOTSTEP SOUNDS", "toggleFootstepSounds", true, "Audio_Canvas/Audio/footstep sounds/footstep_sounds");
            var mm = F("AUDIO", "MENU MUSIC", "main_menu_music", 0, 1, .3f, "Canvas_MM/MainMenu/Music", "%"); mm.TitleOnly = true;
            B("GAMEPLAY", "DISABLE FATIGUE", "disableFatigue", false, "Gameplay_Canvas/Gameplay/disable fatigue/disable_fatigue");
            B("GAMEPLAY", "DISABLE THIRST / HUNGER", "disableThirstHunger", false, "Gameplay_Canvas/Gameplay/disable thirst hunger/disable_thirst_hunger");
            B("GAMEPLAY", "DISABLE INSANITY", "disableInsanity", false, "Gameplay_Canvas/Gameplay/disable insanity/disable_insanity");
            B("GAMEPLAY", "DISABLE SANDSTORM", "disableSandstorm", false, "Gameplay_Canvas/Gameplay/disable sandstorm/disable_sandstorm");
            F("PLAYER & CAR", "MOUSE SENSITIVITY X", "mouse_sensy_x", -10, 10, 2, "PlayerCar_Canvas/PlayerCar/mouse sensy x/mouse_sensy_x", "0.0");
            F("PLAYER & CAR", "MOUSE SENSITIVITY Y", "mouse_sensy_y", -10, 10, 2, "PlayerCar_Canvas/PlayerCar/mouse sensy y/mouse_sensy_y", "0.0");
            F("PLAYER & CAR", "ITEM ROTATE SPEED", "itemRotateSpeed", -10, 10, 2, "PlayerCar_Canvas/PlayerCar/item rotate/item_rotate", "0.0");
            B("PLAYER & CAR", "TOGGLE AIM DOWN SIGHTS", "toggleADS", false, "PlayerCar_Canvas/PlayerCar/aim down sights/aim_down_sights");
            B("PLAYER & CAR", "HEAD BOB", "toggleHeadbob", true, "PlayerCar_Canvas/PlayerCar/headbob/headbob");
            F("PLAYER & CAR", "STEERING SPEED", "steeringspeed", 10, 300, 50, "PlayerCar_Canvas/PlayerCar/steeringspeed/steeringspeed", "0");
            B("PLAYER & CAR", "AUTOMATIC GEARBOX", "automatic_gearbox", true, "PlayerCar_Canvas/PlayerCar/automatic gearbox/automatic_gearbox");
            B("PLAYER & CAR", "AUTO CLUTCH", "autoclutch", true, "PlayerCar_Canvas/PlayerCar/autoclutch/autoclutch");
            B("PLAYER & CAR", "H-SHIFTER", "hshifter", false, "PlayerCar_Canvas/PlayerCar/hshifter/hshifter");
            B("PLAYER & CAR", "SMOOTH INPUT", "smoothinput", true, "PlayerCar_Canvas/PlayerCar/smoothinput/smoothinput");
            B("PLAYER & CAR", "NORMALIZE INPUT", "normalizeinput", false, "PlayerCar_Canvas/PlayerCar/normalizeinput/normalizeinput");
            // CHEATS (shown with [Menu] EnableCheats)
            B("CHEATS", "PEACEFUL MODE", "peacefulMode", false, "Gameplay_Canvas/Gameplay/peaceful mode/peaceful_mode");
            B("CHEATS", "MAP CHEAT (WHOLE MAP)", "mapCheat", false, "Gameplay_Canvas/Gameplay/map cheat/map_cheat");
            B("CHEATS", "PERMA BASE (HIDDEN GAME OPTION)", "permaBase", false, "Gameplay_Canvas/Gameplay/perma base/perma_base");
            var gm = D("CHEATS", "GOD MODE: FLY, NO CLIP (F11)", null, "fsm"); gm.Path = "Player"; gm.FsmName = "GODMODE"; gm.OnState = "active"; gm.GameOnly = true;
            var gh = D("CHEATS", "GOD HANDS (F10)", null, "fsm"); gh.Path = "PlayerCameraHolder/PlayerCamera"; gh.FsmName = "GodHands"; gh.OnState = "on"; gh.GameOnly = true;
            var dbg = D("CHEATS", "DEBUG SCREEN", null, "screen"); dbg.Path = "MainMenu_Canvas/MainMenu/Debug"; dbg.Key = "Debug_Canvas"; dbg.GameOnly = true;
        }
        /// <summary>Defines a native settings row and its presentation type.</summary>
        private static SettingDef D(string tab, string label, string key, string kind)
        { var d = new SettingDef { Tab = tab, Label = label, Key = key, Kind = kind }; Defs.Add(d); return d; }
        /// <summary>Defines a boolean native settings row with its default and FSM path.</summary>
        private static SettingDef B(string tab, string label, string key, bool def, string path)
        { var d = D(tab, label, key, "bool"); d.Def = def ? 1 : 0; d.Path = path; return d; }
        /// <summary>Defines a numeric native settings row with its range, default and formatting.</summary>
        private static SettingDef F(string tab, string label, string key, float min, float max, float def, string path, string fmt)
        { var d = D(tab, label, key, "float"); d.Min = min; d.Max = max; d.Def = def; d.Path = path; d.Fmt = fmt; return d; }

        // ---- storage: the game's own UI control when it exists (in game it applies the change at once), plus SaveSettings.es3
        /// <summary>Creates file-backed native settings serialization options.</summary>
        private static ES3Settings FileSet() { return new ES3Settings("SaveSettings.es3", ES3.Location.File); }
        /// <summary>Creates cache-backed native settings serialization options.</summary>
        private static ES3Settings CacheSet() { return new ES3Settings("SaveSettings.es3", ES3.Location.Cache); }

        /// <summary>Resolves path for the native settings binding.</summary>
        internal static Transform FindPath(string path) { return SceneCache.FindPath(path); }

        /// <summary>Resolves fsm of for the native settings binding.</summary>
        private static PlayMakerFSM FsmOf(SettingDef d)
        {
            var t = FindPath(d.Path);
            if (t != null) foreach (var f in t.GetComponents<PlayMakerFSM>()) if (f.FsmName == d.FsmName) return f;
            return null;
        }

        /// <summary>Reads the native settings binding.</summary>
        internal static float Read(SettingDef d)
        {
            if (d.Key == "color_grading") return 0;
            if (d.Kind == "fsm") { var f = FsmOf(d); return f != null && f.ActiveStateName == d.OnState ? 1 : 0; }
            var t = FindPath(d.Path);
            if (t != null)
            {
                if (d.Kind == "bool") { var tg = t.GetComponent<Toggle>(); if (tg != null) return tg.isOn ? 1 : 0; }
                else { var sl = t.GetComponent<Slider>(); if (sl != null) return sl.value; }
            }
            try
            {
                var fs = FileSet();
                if (ES3.KeyExists(d.Key, fs))
                    return d.Kind == "bool" ? (ES3.Load<bool>(d.Key, fs) ? 1 : 0) : ES3.Load<float>(d.Key, fs);
            }
            catch (Exception e) { HudModule.Warn("Settings read " + d.Key + ": " + e.Message); }
            return d.Def;
        }

        /// <summary>Writes the native settings binding.</summary>
        internal static void Write(SettingDef d, float v)
        {
            if (d.Key == "color_grading") v = 0;
            if (d.Kind == "fsm")
            {
                var f = FsmOf(d);
                if (f == null || !f.isActiveAndEnabled) return;
                bool on = f.ActiveStateName == d.OnState;
                if (on != v > .5f) f.SendEvent(on ? "back" : "next");   // the same transitions the F-key fires
                return;
            }
            var t = FindPath(d.Path);
            if (t != null)
            {
                if (d.Kind == "bool") { var tg = t.GetComponent<Toggle>(); if (tg != null && tg.isOn != (v > .5f)) tg.isOn = v > .5f; }
                else { var sl = t.GetComponent<Slider>(); if (sl != null) sl.value = v; }
            }
            try
            {
                if (d.Kind == "bool") { ES3.Save<bool>(d.Key, v > .5f, FileSet()); var cs = CacheSet(); if (ES3.FileExists(cs)) ES3.Save<bool>(d.Key, v > .5f, cs); }
                else { ES3.Save<float>(d.Key, v, FileSet()); var cs = CacheSet(); if (ES3.FileExists(cs)) ES3.Save<float>(d.Key, v, cs); }
            }
            catch (Exception e) { HudModule.Warn("Settings write " + d.Key + ": " + e.Message); }
        }

        /// <summary>Formats the native settings binding.</summary>
        internal static string Format(SettingDef d, float v)
        {
            if (d.Kind == "bool" || d.Kind == "fsm") return v > .5f ? "ON" : "OFF";
            if (d.Fmt == "%") return Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";
            return v.ToString(d.Fmt ?? "0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>Reads string for the native settings binding.</summary>
        internal static string ReadString(string key)
        {
            try { var fs = FileSet(); if (ES3.KeyExists(key, fs)) return ES3.Load<string>(key, fs); }
            catch { }
            return null;
        }
    }

    // Display options = the game's Settings.xml (screenWidth, screenHeight, screenMode, qualityLevel).
    internal sealed class Display
    {
        internal static readonly string[] ModeNames = { "FULLSCREEN", "BORDERLESS", "MAXIMIZED", "WINDOWED" };
        internal int W, H, Mode, Quality;
        internal readonly List<Vector2Int> Res = new List<Vector2Int>();
        /// <summary>Resolves combine for the native settings binding.</summary>
        private static string PathXml { get { return Path.Combine(Application.persistentDataPath, "Settings.xml"); } }

        /// <summary>Loads the native settings binding.</summary>
        internal static Display Load()
        {
            var d = new Display { W = Screen.width, H = Screen.height, Mode = (int)Screen.fullScreenMode, Quality = QualitySettings.GetQualityLevel() };
            try
            {
                if (File.Exists(PathXml))
                {
                    var x = File.ReadAllText(PathXml);
                    d.W = Tag(x, "screenWidth", d.W); d.H = Tag(x, "screenHeight", d.H); d.Mode = Tag(x, "screenMode", d.Mode); d.Quality = Tag(x, "qualityLevel", d.Quality);
                }
            }
            catch { }
            foreach (var r in Screen.resolutions) { var v = new Vector2Int(r.width, r.height); if (!d.Res.Contains(v)) d.Res.Add(v); }
            var cur = new Vector2Int(d.W, d.H);
            if (!d.Res.Contains(cur)) d.Res.Add(cur);
            d.Res.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            d.Mode = Mathf.Clamp(d.Mode, 0, 3);
            d.Quality = Mathf.Clamp(d.Quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            return d;
        }
        /// <summary>Reads an integer display setting from native XML with a safe fallback.</summary>
        private static int Tag(string x, string tag, int def)
        {
            var m = Regex.Match(x, "<" + tag + ">\\s*(-?\\d+)\\s*</" + tag + ">");
            int v; return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def;
        }
        /// <summary>Applies the native settings binding.</summary>
        internal void Apply()
        {
            Screen.SetResolution(W, H, (FullScreenMode)Mode);
            QualitySettings.SetQualityLevel(Quality, true);
            Core.Log.Always(null,"[CleanHUD] Display apply requested: " + ResText + " " + ModeNames[Mode] + ", quality " + QualityText);
            try
            {
                File.WriteAllText(PathXml, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Settings xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
                    "  <screenWidth>" + W + "</screenWidth>\r\n  <screenHeight>" + H + "</screenHeight>\r\n  <screenMode>" + Mode + "</screenMode>\r\n  <qualityLevel>" + Quality + "</qualityLevel>\r\n</Settings>");
            }
            catch (Exception e) { HudModule.Warn("Settings.xml not written: " + e.Message); }
        }
        /// <summary>Formats the current display resolution for the settings menu.</summary>
        internal string ResText { get { return W + "x" + H; } }
        internal string QualityText { get { var n = QualitySettings.names; return Quality >= 0 && Quality < n.Length ? n[Quality].ToUpperInvariant() : Quality.ToString(); } }
    }
}
