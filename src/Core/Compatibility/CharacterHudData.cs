using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Presentation-owned character selection metadata used by HUD portrait drawing.</summary>
    public interface ICharacterHudData
    {
        int Choice { get; }
        bool ReplaceDriver { get; }
    }
    /// <summary>HUD-owned editable character portrait images used by player settings.</summary>
    public interface ICharacterPortraits
    {
        void Prepare();
        Texture Picture(int choice);
    }
    /// <summary>HUD-owned native cursor positioning requested by the presentation camera.</summary>
    public interface ICursorPresentation
    {
        void Move(Vector2 screen);
        void Restore();
    }
}
