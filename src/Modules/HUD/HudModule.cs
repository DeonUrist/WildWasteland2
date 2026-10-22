using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.HUD
{
    /// <summary>
    /// Combat HUD (ex Gunplay HUD 1.2.2, plugin class GunplayHUD.Plugin): weapon crosshair styles, selection dot toggle,
    /// hit marker, floating damage numbers / damage list and the headshot sound. Provides <see cref="Services.Hud"/>
    /// (ex GunplayHUD.Api) that the Guns module reports its projectile hits to.
    /// </summary>
    public sealed class HudModule : ModuleBase
    {
        public const string OldCfg = "com.denis.apocalypter.gunplayhud.cfg";
        public const string RaiderCfg = "com.denis.apocalypter.apocaraider.cfg";

        internal static HudModule Instance;
        /// <summary>BepInEx\plugins\WildWasteland\HUD\ (Textures/, Sounds/).</summary>
        internal new static string Dir;

        // F6 (player-facing). HudEnabled = old [General] EnableHudEffects (numbers, marker and headshot sound; not the crosshair).
        internal static ConfigEntry<bool> HudEnabled, HitMarker, SelectionDot, HeadshotSound;
        internal static ConfigEntry<int> DamageNumbers, Crosshair, HeadshotVolume;
        // F7 (tools.cfg). DamageFontSize / HitMarkerSize were the old hidden never-saved settings.
        internal static ConfigEntry<int> DamageFontSize, HitMarkerSize, ListLines;
        internal static ConfigEntry<float> NumberSeconds, ListSeconds, MarkerSeconds, MergeSeconds;
        internal static ConfigEntry<float> DotScale, SearchSeconds, HeadshotGain, HeadshotMinInterval;

        static bool _running;
        Job _crosshairJob;

        public override string Id { get { return "HUD"; } }
        public override string DisplayName { get { return "Combat HUD"; } }
        public override string[] ConflictsWith { get { return new[] { "com.denis.apocalypter.gunplayhud" }; } }

        public override void BindConfig(ModuleConfig cfg)
        {
            Instance = this;
            // ---- F6: one toggle per feature first (crosshair style, selection dot, hit feedback group, hit marker, damage numbers, headshot sound)
            Crosshair = cfg.Player("", "Crosshair", 1, "Weapon crosshair: 0 = none, 1 = vanilla, 2 = two horizontal lines, 3 = dot. Follows the game's weapon and aiming visibility.",
                new AcceptableValueRange<int>(0, 3));
            SelectionDot = cfg.Player("", "SelectionDot", true, "Show the game's selection dot without a weapon. Independent of the weapon crosshair.");
            HudEnabled = cfg.Player("", "HitFeedback", true, "Damage numbers, hit marker and headshot sound for your hits (master switch for HitMarker, FloatingDamage and HeadshotSound). The crosshair settings are independent.");
            HitMarker = cfg.Player("", "HitMarker", true, "A diagonal cross flashes at the screen centre when your bullet hits a creature (white, red for a headshot).");
            DamageNumbers = cfg.Player("", "FloatingDamage", 2,
                "Damage your hits do, shown as: 0 = nothing, 1 = a list in the top right corner, 2 = numbers floating up from the hit point. White = damage, red with '!' = headshot, light blue = % of condition taken off a vehicle part.",
                new AcceptableValueRange<int>(0, 2));
            HeadshotSound = cfg.Player("", "HeadshotSound", true, "Play a sound for your headshots. Near-simultaneous hits are coalesced and overlap is limited without restarting sounds.");
            HeadshotVolume = cfg.Player("", "HeadshotVolume", 100, "Volume of the headshot sound, percent.", new AcceptableValueRange<int>(0, 100));

            // ---- F7 (tools.cfg): the old hidden sizes and the technical constants of 1.2.2
            DamageFontSize = cfg.Tool("Hit feedback", "DamageFontSize", 14, "Font size of the damage list, px; floating numbers use +4.", new AcceptableValueRange<int>(8, 40));
            HitMarkerSize = cfg.Tool("Hit feedback", "HitMarkerSize", 22, "Hit marker size, px.", new AcceptableValueRange<int>(6, 100));
            NumberSeconds = cfg.Tool("Hit feedback", "NumberSeconds", 1.2f, "Lifetime of a floating damage number, s.", new AcceptableValueRange<float>(0.3f, 5f));
            ListSeconds = cfg.Tool("Hit feedback", "ListSeconds", 4f, "Lifetime of a line in the top-right damage list, s.", new AcceptableValueRange<float>(1f, 15f));
            ListLines = cfg.Tool("Hit feedback", "ListLines", 8, "Maximum lines in the top-right damage list.", new AcceptableValueRange<int>(1, 20));
            MarkerSeconds = cfg.Tool("Hit feedback", "MarkerSeconds", 0.18f, "How long the hit marker shows, s.", new AcceptableValueRange<float>(0.05f, 1f));
            MergeSeconds = cfg.Tool("Hit feedback", "MergeSeconds", 0.05f, "Hits on the same target within this time are merged into one number (shotgun pellets), s.", new AcceptableValueRange<float>(0f, 0.5f));
            DotScale = cfg.Tool("Crosshair", "DotScale", 0.3f, "Size of the dot crosshair (style 3) relative to the vanilla crosshair.", new AcceptableValueRange<float>(0.1f, 2f));
            SearchSeconds = cfg.Tool("Crosshair", "SearchSeconds", 1f, "While the game's Canvas/MousePoint and MouseCrosshair are not found (menus, loading), look for them at most this often, s.", new AcceptableValueRange<float>(1f, 10f));
            HeadshotGain = cfg.Tool("Headshot sound", "Gain", 1.3f, "Sample gain applied once when headshot.mp3 is decoded (AudioSource volume cannot exceed 1). Applies on the next load (module off/on).", new AcceptableValueRange<float>(0.5f, 3f));
            HeadshotMinInterval = cfg.Tool("Headshot sound", "MinInterval", 0.075f, "Headshots closer together than this play one sound, s.", new AcceptableValueRange<float>(0f, 0.5f));

            LegacyConfig.Import(cfg);

            // React live to F6/F7 edits (subscribed once; handlers do nothing while the module is off).
            EventHandler flags = (s, a) => OnFeedbackSettingChanged();
            HudEnabled.SettingChanged += flags; HitMarker.SettingChanged += flags; DamageNumbers.SettingChanged += flags; HeadshotSound.SettingChanged += flags;
            EventHandler cross = (s, a) => { if (_running && _crosshairJob != null) _crosshairJob.RunNow(); };
            Crosshair.SettingChanged += cross; SelectionDot.SettingChanged += cross; DotScale.SettingChanged += cross;
            HeadshotVolume.SettingChanged += (s, a) => { if (_running) HeadshotAudio.ApplyVolume(); };
        }

        public override void Init()
        {
            Instance = this;
            Dir = Assets.ModuleDir(Id);
            _running = true;
            HitFeedbackService.External = false;
            Services.Hud = new HitFeedbackService();
            // Guns initialises before HUD (module order) and cannot set ExternalReporter while Services.Hud is null: seed it here.
            var guns = Services.Guns;
            if (guns != null) HitFeedbackService.External = guns.ProjectilesEnabled;
            VanillaHits.UpdateFlags();

            Hooks.SetFsmFloatEnter.Prefix(this, VanillaHits.BeforeSetFsmFloat);

            // old Runner: Update = crosshair + headshot audio, LateUpdate = VanillaHits.Flush + Hud.Flush, OnGUI = Hud.OnGUI
            _crosshairJob = Scheduler.Interval(this, "Crosshair", 0.25f, CrosshairController.Tick);
            Scheduler.EveryFrame(this, "Hit feedback flush", FlushHits, Phase.LateUpdate);
            Scheduler.Gui(this, "Hit feedback draw", Hud.OnGUI, Hud.GuiWanted);   // (WW perf) IMGUI only while a number, line or marker shows
            _crosshairJob.RunNow();
            HeadshotAudio.Refresh();
            Log.Info("[HUD] Combat HUD (ex Gunplay HUD 1.2.2) started");
        }

        static void FlushHits() { VanillaHits.Flush(); Hud.Flush(); }

        static void OnFeedbackSettingChanged()
        {
            if (!_running) return;
            VanillaHits.UpdateFlags();
            HeadshotAudio.Refresh();
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            VanillaHits.Clear(); Hud.OnSceneLoaded(); CrosshairController.OnSceneLoaded(); HeadshotAudio.Stop();
            HeadshotAudio.Refresh();
            if (_crosshairJob != null) _crosshairJob.RunNow();
        }

        public override void OnOriginShift(Vector3 delta) { Hud.Shift(delta); VanillaHits.Shift(delta); }

        public override void Shutdown()
        {
            _running = false;
            if (Services.Hud is HitFeedbackService) Services.Hud = null;
            HitFeedbackService.External = false;
            VanillaHits.Clear();
            VanillaHits.UpdateFlags();
            CrosshairController.Shutdown();
            HeadshotAudio.Shutdown();
            Hud.Shutdown();
            _crosshairJob = null;
        }

        // ------------------------------------------------------------------ assets

        /// <summary>Path of a module asset: BepInEx\plugins\WildWasteland\HUD\&lt;sub&gt;\&lt;name&gt;, else the old BepInEx\plugins\GunplayHUD\ folder. Null if neither exists.</summary>
        internal static string AssetPath(string sub, string name)
        {
            string p = Path.Combine(Path.Combine(Dir ?? Assets.ModuleDir("HUD"), sub), name);
            if (File.Exists(p)) return p;
            string legacy = Path.Combine(Path.Combine(Assets.LegacyPluginDir("GunplayHUD"), sub), name);
            return File.Exists(legacy) ? legacy : null;
        }

        /// <summary>Asset bytes: the current/legacy file wins, otherwise use the packaged disk-asset index.</summary>
        internal static byte[] ReadAsset(string sub, string name)
        {
            string p = AssetPath(sub, name);
            if (p != null) return File.ReadAllBytes(p);
            using (var s = WildWasteland.Core.DiskResources.Open("HUD." + sub + "." + name))
            {
                if (s == null) throw new FileNotFoundException("not found in " + Path.Combine(Dir ?? "", sub) + " or the packaged disk assets", name);
                var buf = new byte[s.Length];
                int read = 0;
                while (read < buf.Length) { int n = s.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
                return buf;
            }
        }

        // ------------------------------------------------------------------ F7

        public override IEnumerable<ToolPage> ToolPages()
        {
            return new ToolPage[] { new HudToolPage(this, Config.Tools) };
        }

        sealed class HudToolPage : ToolPage
        {
            readonly ConfigPage _settings;
            public HudToolPage(IModule owner, ConfigFile tools)
            {
                Owner = owner; Group = ToolGroups.Weapons; Title = "HUD"; ShowWhenModuleOff = true; IsTool = true;
                _settings = new ConfigPage(owner, ToolGroups.Weapons, "HUD", tools, "HUD / *");
            }

            public override void Draw(ToolLayout l)
            {
                bool on = ModuleHost.IsRunning(Owner);
                l.Text("Crosshair, hit marker, damage numbers and headshot sound (ex Gunplay HUD 1.2.2). The player settings are on the Game page.");
                l.Heading("Status");
                l.Value("Crosshair UI", CrosshairController.Status);
                l.Value("Headshot sound", HeadshotAudio.Status);
                l.Value("Hit source", !on ? "-" : VanillaHits.Quiet ? "Guns module (projectiles)" : VanillaHits.Observing ? "vanilla hits (SetFsmFloat observer)" : "none (feedback off)");
                l.Space(4);
                l.Text("Tests close this window and fake one hit in front of the camera with the current settings.");
                int b = l.Buttons("TEST HIT", "TEST HEADSHOT");
                if (b >= 0 && on) { bool head = b == 1; ToolWindow.CloseThen(() => Hud.Test(head)); }
                _settings.Draw(l);
            }
        }
    }

    /// <summary>Services.Hud implementation (ex public static class GunplayHUD.Api).</summary>
    internal sealed class HitFeedbackService : IHitFeedback
    {
        internal static bool External;

        public bool Enabled
        {
            get
            {
                return HudModule.HudEnabled != null && HudModule.HudEnabled.Value &&
                       (HudModule.DamageNumbers.Value != 0 || HudModule.HitMarker.Value || HudModule.HeadshotSound.Value);
            }
        }
        public void PlayerHit(GameObject target, Vector3 point, float damage, bool head) { Hud.PlayerHit(target, point, damage, head); }
        public void PartHit(GameObject target, Vector3 point, float percent) { Hud.PartHit(target, point, percent); }
        public bool ExternalReporter
        {
            get { return External; }
            set { if (External == value) return; External = value; VanillaHits.Clear(); VanillaHits.UpdateFlags(); }
        }
    }
}
