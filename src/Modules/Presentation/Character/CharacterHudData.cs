using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Exposes selected character metadata without giving HUD access to body implementation types.</summary>
    internal sealed class CharacterHudData : ICharacterHudData
    {
        /// <summary>Reads the selected authored portrait index.</summary>
        public int Choice { get { return CharacterPortraits.Canonical((int)CharacterSettings.Character.Value); } }
        /// <summary>Returns whether the TAB portrait should replace the native driver picture.</summary>
        public bool ReplaceDriver { get { return CharacterSettings.ReplaceDriver.Value; } }
    }
}
