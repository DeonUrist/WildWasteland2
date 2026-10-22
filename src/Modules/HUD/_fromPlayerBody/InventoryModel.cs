using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.HUD
{
    // Settings and TAB share editable RGBA PNGs. Native DXT1 has no alpha, and a GPU-only backup loses its pixels when
    // Low's masterTextureLimit changes. Bind the RawImages instead; CleanHUD mirrors their texture references.
    internal static class InventoryModel
    {
        private static WildWasteland.Core.ICharacterHudData State { get { return WildWasteland.Core.ModApi.Get<WildWasteland.Core.ICharacterHudData>(); } }
        private static int _choice = -1;
        /// <summary>Tracks actual provider and selected portrait changes through the HUD-owned frame job.</summary>
        internal static void Tick() { var state = State; if (state == null || !state.ReplaceDriver) { Off(); return; } if (_choice != state.Choice) { _choice = state.Choice; CharacterChanged(); } LateTick(); }
        private const string GameTexture = "player_character_2_UI";
        private static Texture2D _game;
        private static readonly System.Collections.Generic.Dictionary<RawImage, Rect> _switched = new System.Collections.Generic.Dictionary<RawImage, Rect>();
        private static bool _applied;
        private static float _nextTry;
        private static int _tries;

        internal static void PrepareSettingsPictures()
        {
            WildWasteland.Core.ModApi.Get<WildWasteland.Core.ICharacterPortraits>()?.Prepare();
        }

        // Runner calls this every frame, but it only works until the swap is done for the loaded scene (a cheap bool check after that)
        public static void LateTick()
        {
            // (WW) module on/off is handled by PlayerBodyModule (Init resets, Shutdown calls Off; ReplaceDriver changes call Off / Reset)
            if (_applied || State == null || !State.ReplaceDriver) return;
            if (Time.unscaledTime < _nextTry || _tries >= 15) return;   // the texture may load a moment after the scene: retry for ~30 s
            _nextTry = Time.unscaledTime + 2f; _tries++;
            Apply();
        }

        private static void Apply()
        {
            if (_game == null)
                foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
                    if (t != null && t.name == GameTexture) { _game = t; break; }
            if (_game == null) return;
            PrepareSettingsPictures();
            var mine = WildWasteland.Core.ModApi.Get<WildWasteland.Core.ICharacterPortraits>()?.Picture(State != null ? State.Choice : -1);
            if (mine == null) return;
            int n = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<RawImage>())
                if (r != null && r.gameObject.scene.IsValid() && r.texture == _game) { _switched.Add(r, r.uvRect); r.texture = mine; r.uvRect = new Rect(0, 0, 1, 1); n++; }
            _applied = n > 0;
            if (_applied) HudModule.Log("TAB screen picture: " + n + " UI element(s) switched to " + (State != null ? State.Choice.ToString() : "unknown") + " PNG");
        }
        internal static void RefreshPortrait()
        {
            var picture = WildWasteland.Core.ModApi.Get<WildWasteland.Core.ICharacterPortraits>()?.Picture(State != null ? State.Choice : -1);
            if (picture != null) foreach (var image in _switched.Keys) if (image != null) image.texture = picture;
        }

        public static void Off()
        {
            foreach (var pair in _switched) if (pair.Key != null && _game != null) { pair.Key.texture = _game; pair.Key.uvRect = pair.Value; }
            _switched.Clear();
            _applied = false; _tries = 0; _nextTry = 0f;
        }

        // Character switched: restore native references, then bind the other PNG on the next tick.
        public static void CharacterChanged()
        {
            Off();
        }

        // New scene: the native texture/UI may be new instances; the shared PNGs remain cached.
        public static void Reset()
        {
            Off(); _game = null;
        }
    }
}
