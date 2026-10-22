using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Modules.Presentation;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    // The game's first-person arms (player_arm_left / player_arm_left.002, 8 bones each, one pair per weapon, tool and item animation)
    // and the kick leg (Player_leg) all use the material "Player2" - the same texture atlas as the seated driver. We give those
    // renderers a copy of the material with our atlas (the arm/leg islands repainted from the character's texture, the rest unchanged),
    // so every vanilla animation - reloads, melee, items - keeps working, just with her (or Max's) arms.
    //  - Female: Player2_female_arms.png on both arms and the leg (her arms are alike).
    //  (WW2) the never-used separate left-arm texture path (Model.ArmsLeft, always null) is gone (README 7).
    internal static class Arms
    {
        private static Material _mat, _matKick;
        private static readonly Dictionary<Renderer, Material> _orig = new Dictionary<Renderer, Material>();
        private static float _next;

        public static void Tick()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 3f;
            if (PlayerState.CameraHolder == null) return;
            var tex = Model.Arms();
            if (tex == null) return;
            int added = 0;
            foreach (var smr in PlayerState.CameraHolder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || _orig.ContainsKey(smr)) continue;
                var m = smr.sharedMaterial;
                if (m == null || !m.name.StartsWith("Player2")) continue;
                if (_mat == null)
                {
                    _mat = new Material(m) { name = "Player2 (Apocaplayer)", mainTexture = tex };
                    _mat.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                _orig[smr] = m;
                bool kick = smr.sharedMesh != null && smr.sharedMesh.name == "Player_leg";
                if (kick && _matKick == null)
                {
                    var leg = Model.Kick();
                    if (leg != null) _matKick = new Material(m) { name = "Player2 (Apocaplayer, kick)", mainTexture = leg, hideFlags = HideFlags.DontUnloadUnusedAsset };
                }
                smr.sharedMaterial = kick && _matKick != null ? _matKick : _mat;
                added++;
            }
            if (added > 0) { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Arms: " + added + " first-person arm/leg renderer(s) dressed"); }
        }

        public static void Restore()
        {
            foreach (var kv in _orig) if (kv.Key != null) kv.Key.sharedMaterial = kv.Value;
            _orig.Clear();
            _mat = _matKick = null;   // the next character's textures
            _next = 0f;
        }

        public static void OnSceneLoaded() { _orig.Clear(); _next = 0f; }
    }
}
