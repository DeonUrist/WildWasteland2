using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WildWasteland
{
    /// <summary>
    /// Entry point (AGENTS.md 5.2): binds config, starts modules, applies the shared dispatchers and creates the Runner.
    /// The game destroys plugin GameObjects on scene load, so nothing runs on this component after Awake.
    /// The soft dependencies on the standalone mods only force them to load first, so the conflict check (5.8) sees them.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInIncompatibility("com.denis.apocalypter.wildwasteland")]
    [BepInIncompatibility("com.denis.apocalypter.wildwastelandmp")]
    [BepInDependency("com.denis.apocalypter.apocasetter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaplayer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaclimber", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.npcai", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.womenofwasteland", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.gunplay", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaunloader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.healthstacks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.itweenguard", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.stewpot", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.gunplayhud", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.cleanhud", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("local.immersivemap", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocapocket", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocapatrol", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.warrig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.merchantspotter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaraider", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.motorcyclemod", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("apocalypter.dogcompanion", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("local.apocalypter.duststorm", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaspawner", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.partadjustment", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.example.apocalypter.talk", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.burner.apocalypter.enginesim", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("apocalypter.worldamp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.denis.apocalypter.wildwasteland2";
        public const string NAME = "Wild Wasteland 2";
        public const string VERSION = "2.0.10";

        public static Plugin Instance;
        public static ConfigFile Main, Settings, Tools;
        public static ConfigEntry<Key> ToolWindowKey;
        public static ConfigEntry<bool> EnableDebug, DeveloperMode;
        public static ConfigEntry<float> FrameBudgetMs;

        /// <summary>BepInEx entry: binds Core config, registers and binds modules, creates the runner, installs Core fixes and starts the modules (AGENTS 5.2).</summary>
        void Awake()
        {
            Instance = this;
            Core.Log.Source = Logger;
            try
            {
                Core.Assets.Init(Info.Location);
                Core.UI.Skin.Init(Info.Location);
                var toolsPath = Path.Combine(Core.Assets.ConfigDir, "tools.cfg");
                Directory.CreateDirectory(Core.Assets.ConfigDir);
                Main = Config;
                // D12 fresh start: settings.cfg is no longer seeded from the old main cfg and no legacy key is migrated.
                var settingsPath = Path.Combine(Core.Assets.ConfigDir, "settings.cfg");
                Settings = new ConfigFile(settingsPath, true);
                Tools = new ConfigFile(toolsPath, true);

                // Apocasetter hides its own opt-in entry; only these two settings are shown in F6.
                Core.TunedSettings.Bind(Main, "General", "Apocasetter", true, "Lists Wild Wasteland in the Apocasetter Mods menu (F6).");
                ToolWindowKey = Core.TunedSettings.Bind(Main, "Keys", "In-Game Settings Button", Key.F3,
                    "Opens Wild Wasteland settings: Game, Modules and, when enabled, Debug.");
                EnableDebug = Core.TunedSettings.Bind(Main, "General", "Enable Debug", false,
                    "Show Debug in Wild Wasteland settings: logging, switches, patrol editor access and the optional Developer menu.");

                Core.CoreSettings.Bind(Tools);
                Core.UI.UiTheme.Bind(Settings);
                Core.Optimization.OptimizationRuntime.Bind(Settings, Tools);
                Core.ModuleRegistry.RegisterAll();
                Core.ModuleHost.BindAll(Settings, Tools);

                DeveloperMode = Core.TunedSettings.Bind(Settings, "Debug", "Developer mode", false, "Show Developer while Enable Debug is on. Contains advanced settings, editors and diagnostics.");
                FrameBudgetMs = Core.TunedSettings.Bind(Tools, "Core", "Frame budget (ms)", 2f, new ConfigDescription("Sliced background work (bakes, scans) stops for the frame once Wild Wasteland used this much time.", new AcceptableValueRange<float>(0.5f, 8f)));
                Core.Scheduler.FrameBudgetMs = FrameBudgetMs.Value;
                FrameBudgetMs.SettingChanged += (s, a) => Core.Scheduler.FrameBudgetMs = FrameBudgetMs.Value;

                Core.Runner.Ensure();
                RegisterCoreJobs();
                Core.UI.UiTheme.Init();
                Core.VehicleOwnership.Install();
                Core.ReplacementGuard.Prepare();
                Core.Fixes.Install();
                Core.UI.ToolWindow.Init();
                SceneManager.sceneLoaded += OnSceneLoaded;

                // Reset audio before any module creates clips.
                var audioVoices = Core.ModuleRegistry.AudioVoicesSetting();
                if (audioVoices != null) ApplyAudioVoices(audioVoices.Value);
                Core.ModuleHost.StartAll();
                Core.Optimization.OptimizationRuntime.Instance.Init();
                Core.Optimization.OptimizationRuntime.Instance.ProvideApis();
                Core.Log.Always(null,Core.ModuleHost.Summary());
            }
            catch (Exception e)
            {
                Core.Log.Error(null,"Wild Wasteland failed to start", e);
            }
        }

        /// <summary>Applies the Core real-voice budget before any module creates clips.</summary>
        static void ApplyAudioVoices(int want)
        {
            try
            {
                var cfg = UnityEngine.AudioSettings.GetConfiguration();
                if (want <= 0 || cfg.numRealVoices == want) { Core.Log.Main(null,"[Core] Audio: " + cfg.numRealVoices + " real voices (unchanged)"); return; }
                int before = cfg.numRealVoices;
                cfg.numRealVoices = want;
                if (cfg.numVirtualVoices < want) cfg.numVirtualVoices = want * 8;
                bool ok = UnityEngine.AudioSettings.Reset(cfg);
                Core.Log.Always(null,"[Core] Audio: real voices " + before + " -> " + UnityEngine.AudioSettings.GetConfiguration().numRealVoices + (ok ? "" : " (Reset failed)"));
            }
            catch (Exception e) { Core.Log.Warn(null,"[Core] Audio: could not set the voice count: " + e.Message); }
        }

        /// <summary>Registers the per-frame Core job.</summary>
        static void RegisterCoreJobs()
        {
            Core.Scheduler.EveryFrame(null, "Core.Frame", CoreFrame, Core.Phase.EarlyUpdate);
        }

        /// <summary>Core.Frame job (EarlyUpdate): editor session, SaveBus watcher, hotkeys, tool window.</summary>
        static void CoreFrame()
        {
            Core.UI.EditorSession.Tick();
            Core.SaveBus.Tick();
            Core.Hotkeys.Tick();
            Core.UI.ToolWindow.Tick();
        }

        /// <summary>SceneManager.sceneLoaded: resets Core caches (refs, origin, entities, save watcher, log limits) and forwards to modules.</summary>
        static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            Core.ReplacementGuard.SceneLoaded(s);
            Core.Runner.Ensure();
            Core.GameRefs.OnSceneLoaded(s, m);
            Core.Entities.OnSceneLoaded();
            Core.Origin.Reset();
            Core.SceneLookup.Reset();
            Core.ActorQuery.Reset(); Core.Money.Reset(); Core.PlayerInventory.Reset(); Core.VehicleEngine.Reset(); Core.NativeFactionRelations.Reset();
            Core.ItemSpawner.ResetRegistrations();
            Core.SaveBus.OnSceneLoaded();
            Core.UI.EditorSession.CancelAll();
            Core.Log.ResetRateLimits();
            Core.ModuleHost.SceneLoaded(s, m);
        }
    }
}
