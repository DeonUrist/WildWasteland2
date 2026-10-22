using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Exposes the actual carrier through the owning body instead of third-party reflection into private state.</summary>
    internal sealed partial class Body
    {
        internal GameObject WeaponCarrier=>_prop;
    }
}
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Supplies body visibility and current held carrier only while native first-person presentation is eligible.</summary>
    internal sealed class PlayerWeaponVisual : IPlayerWeaponVisual
    {
        /// <summary>Validates the real body and settings before exposing current native visual objects.</summary>
        public bool TryGet(object value,out GameObject root,out GameObject carrier)
        {var body=value as WildWasteland.Modules.Presentation.Body;root=null;carrier=null;if(!PresentationModule.Running||body==null||!CharacterSettings.BodyFirstPerson.Value||PlayerState.InCar&&!CharacterSettings.BodyInCar||body.Root==null||!body.Root.activeInHierarchy)return false;root=body.Root;carrier=body.WeaponCarrier;return true;}
        /// <summary>Reads current native prop visibility intent when restoring suppressed carrier renderers.</summary>
        public bool Visible(object value){var body=value as WildWasteland.Modules.Presentation.Body;return body!=null&&body.WeaponPropVisible;}
    }
}
