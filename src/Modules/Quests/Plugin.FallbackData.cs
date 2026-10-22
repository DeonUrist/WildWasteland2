using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Exposes pending quest notifications and theme data without owning HUD drawing.</summary>
    internal sealed partial class Plugin : IQuestFallbackData
    {
        /// <summary>Returns retained pending notices; HUD ignores expired rows.</summary>
        public int NoticeCount { get { return toasts.Count; } }
        /// <summary>Reads one pending notification text.</summary>
        public string NoticeText(int index) { return toasts[index].Text; }
        /// <summary>Reads the unscaled deadline of one pending notification.</summary>
        public float NoticeUntil(int index) { return toasts[index].Until; }
        /// <summary>Supplies the optional Talk theme to the HUD renderer.</summary>
        public bool WindowSkin(out Texture2D texture, out RectOffset border) { return bridge.WindowSkin(out texture, out border); }
        /// <summary>Matches native pause and unlocked-cursor conversation admission.</summary>
        static bool GamePaused() { return Time.timeScale < .01f || (Cursor.visible && Cursor.lockState != CursorLockMode.Locked); }
    }
}
