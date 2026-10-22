using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;
namespace WildWasteland.Modules.Presentation
{
    internal sealed partial class Body
    {
        /// <summary>Delivers aim context at its original animation point; Tools owns the input and resulting hints.</summary>
        private void AimKeys(string weapon,string clip,float dt)
        {
            if(!GunPose.Editing || string.IsNullOrEmpty(weapon) || !ThirdPerson.On)return;
            float gap=float.NaN;
            if(_prop!=null && Core.PlayerState.PlayerCamera!=null){var rs=_prop.GetComponentsInChildren<Renderer>();if(rs.Length>0){var b=rs[0].bounds;for(int i=1;i<rs.Length;i++)b.Encapsulate(rs[i].bounds);gap=Vector3.Dot(Core.PlayerState.PlayerCamera.position-b.center,_anim.up)*100f-b.extents.y*100f;}}
            PoseEditorApi.AimInput(weapon,clip,_aimCrouched,dt,gap);
        }
    }
}
