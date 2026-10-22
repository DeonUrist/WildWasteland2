using System.IO;
using BepInEx.Bootstrap;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Women of Wasteland (D9): female bodies and voices for Flexa/Sprokka raiders (Gungirl/Shoota), the female population roll on
    /// spawn and crew dressing, and the female player models. Started/stopped by PresentationModule; serves the INpcAppearance and
    /// ICharacterAssets contracts. Models and voices ship under plugins/WildWasteland/Presentation/{Models,Sounds/Gungirl} (D13).
    /// </summary>
    internal sealed class WomenFeature : INpcAppearance, ICharacterAssets
    {
        internal const string GUID = "com.denis.apocalypter.womenofwasteland";
        /// <summary>True while the built-in female bodies run (Presentation running and no standalone Women of Wasteland).</summary>
        internal static bool Active;
        static readonly WomenFeature _api = new WomenFeature();

        /// <summary>ICharacterAssets: the model / texture paths of the Gungirl or Shoota identity (used for the player's character choice too).</summary>
        public bool TryPaths(string identity, out string model, out string texture) { return Paths(identity, out model, out texture); }

        /// <summary>Model / texture file of the Gungirl or Shoota identity under the Presentation asset folder; false for other identities.</summary>
        internal static bool Paths(string identity, out string model, out string texture)
        {
            model = texture = null;
            if (identity != "Gungirl" && identity != "Shoota") return false;
            model = PresentationModule.AssetPath(identity == "Gungirl" ? CharacterSettings.GungirlModel.Value : CharacterSettings.ShootaModel.Value);
            texture = PresentationModule.AssetPath(identity == "Gungirl" ? CharacterSettings.GungirlTexture.Value : CharacterSettings.ShootaTexture.Value);
            return true;
        }

        /// <summary>Starts the female bodies unless the standalone Women of Wasteland plugin is loaded: models, voices, the CreateObject
        /// postfix (spawn roll + rename), the restoration / discovery / voice / cleanup jobs and the debug spawn key.</summary>
        internal static void Initialize(PresentationModule module)
        {
            BepInEx.PluginInfo standalone;
            if (Chainloader.PluginInfos.TryGetValue(GUID, out standalone) && !ReferenceEquals(standalone.Instance, null))
            {
                Core.Log.Always(module, "Women of Wasteland: " + ReplacementGuard.Status(GUID) + ". Built-in female bodies wait until restart.");
                return;
            }
            Active = true;
            Gungirl.Initialize();
            Voice.EnsureLoading();
            Hooks.CreateObjectEnter.Postfix(module, Gungirl.AfterCreateObject);
            Scheduler.Sliced(module, "Presentation.FemaleRestoration", 500, Gungirl.Step);
            Scheduler.Interval(module, "Presentation.FemaleDiscovery", 5f, Gungirl.SafetyScan).RunNow();   // (WW perf) a full walk only when the world is ready, then every 60 s (Gungirl)
            Scheduler.Interval(module, "Presentation.FemaleVoices", .25f, Voice.Tick).RunNow();
            Scheduler.Interval(module, "Presentation.FemaleCacheCleanup", 10f, Gungirl.Sweep);
            Hotkeys.Register(module, CharacterSettings.FemaleSpawnKey, Gungirl.SpawnInFront, CanSpawn);
            Core.Log.Always(module, "Women of Wasteland 1.0.0 integrated; female crew conversion available.");
        }

        /// <summary>Registers the appearance providers: ICharacterAssets always (the player's female characters), INpcAppearance while active.</summary>
        internal static void ProvideApis(PresentationModule module)
        {
            ModApi.Provide<ICharacterAssets>(module, _api);
            if (Active) ModApi.Provide<INpcAppearance>(module, _api);
        }

        /// <summary>Hotkey condition of the debug spawn key.</summary>
        private static bool CanSpawn() { return Active && GameRefs.InGame; }

        /// <summary>INpcAppearance: dresses a crew member by identity (Gungirl/Shoota names, or the population roll when randomize).</summary>
        public void Dress(GameObject root, string identity, bool randomize)
        {
            if (Active) Gungirl.DressCrew(root, identity, randomize);
        }

        /// <summary>Stops the female bodies: providers withdrawn, voices stopped, every swapped body restored.</summary>
        internal static void Shutdown()
        {
            Active = false;
            var module = PresentationModule.Instance;
            if (module != null) ModApi.Withdraw<INpcAppearance>(module);
            Voice.Shutdown();
            Gungirl.Shutdown();
        }
    }
}
