using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    // Optional humanoid clips from an AssetBundle (Models/apocaplayer_anims.bundle) built with Unity 2020.3.49f1 from Mixamo FBX files
    // (UnityAnims/ project in the repo). Clips are named after their FBX file: Idle, Walk, WalkBack, StrafeLeft, StrafeRight, Run,
    // CrouchIdle, CrouchWalk, the same with a "Rifle" prefix, RifleAim, RifleFire, RifleReload, PistolAim, PistolFire, PistolReload,
    // Melee, Throw. Humanoid clips retarget onto her (Flexa's avatar) through the Animator, whatever character they were made on.
    internal static class Anims
    {
        private static readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        private static bool _tried;
        private static AssetBundle _bundle;

        /// <summary>True once the bundle loaded at least one clip (loads it on first use).</summary>
        public static bool Loaded { get { Load(); return _clips.Count > 0; } }
        /// <summary>(WW) F7 "Animation framework" page</summary>
        internal static bool Tried { get { return _tried; } }
        /// <summary>Number of loaded clips.</summary>
        internal static int Count { get { return _clips.Count; } }
        internal static bool BundleOpen { get { return _bundle != null; } }

        /// <summary>A bundle clip by name (case-insensitive), or null.</summary>
        public static AnimationClip Get(string name)
        {
            Load();
            AnimationClip c;
            return name != null && _clips.TryGetValue(name, out c) ? c : null;
        }

        /// <summary>first of the names that the bundle has</summary>
        public static AnimationClip First(params string[] names)
        {
            foreach (var n in names) { var c = Get(n); if (c != null) return c; }
            return null;
        }

        /// <summary>(B05 perf, README 8 #6) module start: opens the bundle one frame after Init instead of on the first clip request mid-play.</summary>
        internal static System.Collections.IEnumerator Preload()
        {
            yield return null;
            if (PresentationModule.Running) Load();
        }

        /// <summary>Opens the animation bundle once per session, keeps its clips (Strafe/Shoot alias names added) and logs what it has.</summary>
        private static void Load()
        {
            if (_tried) return;
            _tried = true;
            string path = PresentationModule.AssetPath(CharacterSettings.AnimBundleFile.Value);
            if (!File.Exists(path)) { if (Core.Log.L1) Core.Log.Main(PresentationModule.Instance, "No animation bundle (" + Path.GetFileName(path) + "): using the game's raider clips + procedural walk"); return; }
            try
            {
                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null) { Core.Log.Error(PresentationModule.Instance, "Animation bundle " + path + " could not be opened (built with a different Unity version / platform?)"); return; }
                foreach (var c in _bundle.LoadAllAssets<AnimationClip>())
                {
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    c.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    _clips[c.name.Replace("LeftStrafe", "StrafeLeft").Replace("RightStrafe", "StrafeRight")] = c;
                }
                // "...Shoot" files stand for "...Fire" (PistolShoot -> PistolFire) unless a Fire one exists
                foreach (var k in new List<string>(_clips.Keys))
                    if (k.IndexOf("Shoot", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string f = System.Text.RegularExpressions.Regex.Replace(k, "Shoot", "Fire", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (!_clips.ContainsKey(f)) _clips[f] = _clips[k];
                    }
                var names = new List<string>(_clips.Keys); names.Sort();
                int nonHuman = 0; foreach (var c in _clips.Values) if (!c.humanMotion) nonHuman++;
                if (Core.Log.L1) Core.Log.Main(PresentationModule.Instance, "Animation bundle: " + _clips.Count + " clips (" + string.Join(", ", names.ToArray()) + ")"
                                   + (nonHuman > 0 ? "; " + nonHuman + " are NOT humanoid and won't fit her - set Animation Type = Humanoid in Unity" : ""));
            }
            catch (Exception e) { Core.Log.Error(PresentationModule.Instance, "Animation bundle: " + e.Message); }
        }
    }
}
