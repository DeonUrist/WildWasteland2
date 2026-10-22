using UnityEngine;
using HutongGames.PlayMaker;
namespace WildWasteland.Modules.Patrols
{
    internal partial class Crew
    {
        /// <summary>Delegates native actor FSM muting to its NPC owner.</summary>
        internal static void MuteAi(GameObject actor,string[] allowed=null){Core.ModApi.Get<Core.IOccupantCombat>()?.Mute(actor,allowed!=null);}
        /// <summary>Delegates native cached FSM muting to its NPC owner.</summary>
        internal static void MuteAi(PlayMakerFSM[] fsms,string[] allowed){Core.ModApi.Get<Core.IOccupantCombat>()?.Mute(fsms,allowed!=null);}
        // a shooting driver keeps its combat FSMs (its guard gates the Attack FSM); everything else is muted
        private PlayMakerFSM[] _driverFsms; private GameObject _driverFsmsOf;   // the driver's FSMs, fetched once (the guard runs twice a second)
        /// <summary>The crew guard: mutes the driver's AI, keeping its combat FSMs when it is a shooting driver.</summary>
        internal void MuteAi()
        {
            if (_driver == null) return;
            if (_driverFsms == null || _driverFsmsOf != _driver) { _driverFsms = _driver.GetComponents<PlayMakerFSM>(); _driverFsmsOf = _driver; }
            Core.ModApi.Get<Core.IOccupantCombat>()?.Mute(_driverFsms, Marker?.SetupOnly!=true && Core.ModApi.Get<Core.IOccupantCombat>()?.Engaged(_driver)==true);
        }
    }
}
