using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class QuestFollowers
    {
        /// <summary>Marks the actor hired and asks Presentation to apply its real seat pose.</summary>
        public Component SeatPose(GameObject actor,Transform seat,string name,string armLike){if(actor!=null)Hired.Add(actor.GetInstanceID());return ModApi.Get<IFollowerSeatPresentation>()?.Apply(actor,seat,name,armLike);}
        /// <summary>Delegates actual pose resource release to the active Presentation owner.</summary>
        public void ReleasePose(Component pose){ModApi.Get<IFollowerSeatPresentation>()?.Release(pose);}
        /// <summary>Marks hired actors and delegates their seated arm animation intent.</summary>
        public void Animated(GameObject actor,bool enabled){if(actor!=null)Hired.Add(actor.GetInstanceID());ModApi.Get<IFollowerSeatPresentation>()?.Animated(actor,enabled);}
    }
}
