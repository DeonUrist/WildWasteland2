using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols entry part for Core/UI: the editors' game-font request (Skin). Wave A split of PatrolsModule.cs (B01 folds it into Skin/EditorSession).</summary>
    public sealed partial class PatrolsModule
    {
        // the game's menu font as captured by Apocasetter (ex EditorSession.Resolve -> PatrolSkin.SetGameFont): asked when an editor opens or
        // the spawn warning shows, at most once a second while unknown
        static float _nextFontAsk;
        /// <summary>Gives Skin the game's menu font from Apocasetter when it is still unknown (at most once a second).</summary>
        internal static void EnsureGameFont()
        {
            if (Skin.GameFont != null || Time.unscaledTime < _nextFontAsk) return;
            _nextFontAsk = Time.unscaledTime + 1f;
            var f = ApocasetterBridge.GameFont;
            if (f != null) Skin.SetGameFont(f);
        }
    }
}
