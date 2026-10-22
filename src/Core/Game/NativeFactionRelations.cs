using HutongGames.PlayMaker;
using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Native faction-to-player state used when no owned faction relation has been published.</summary>
    public static class NativeFactionRelations
    {
        static PlayMakerFSM _coyotes;
        static FsmInt _onOff;
        static float _retry;
        /// <summary>Releases native scene-owned relation variables.</summary>
        public static void Reset() { _coyotes = null; _onOff = null; _retry = 0; }
        /// <summary>Preserves native Coyotes friendship/enmity and Scrapyard hostility; owned faction declarations take precedence.</summary>
        public static bool PlayerHostile(string faction)
        {
            var stance = Factions.ToPlayer(faction);
            if (stance != Stance.Unknown) return stance == Stance.Hostile;
            if (!string.Equals(faction, "Coyotes", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (_coyotes == null && Time.time >= _retry)
            {
                _retry = Time.time + 2f; _onOff = null;
                _coyotes = FsmUtil.Find(GameRefs.GameManager, "FactionRelation_Coyotes");
            }
            if (_coyotes == null || !_coyotes.Fsm.Initialized) return false;
            if (_coyotes.ActiveStateName == "PlayerEnemy") return true;
            if (_onOff == null) _onOff = _coyotes.FsmVariables.FindFsmInt("OnOff");
            return _onOff != null && _onOff.Value > 0;
        }
    }
}
