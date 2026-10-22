using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Read-only quest fallback notices and optional external dialogue theme; rendering belongs to HUD.</summary>
    public interface IQuestFallbackData
    {
        int NoticeCount { get; }
        string NoticeText(int index);
        float NoticeUntil(int index);
        bool WindowSkin(out Texture2D texture, out RectOffset border);
    }
}
