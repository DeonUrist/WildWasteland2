using System;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>Equipment snapshot consuming optional logical custody or Core native slots.</summary>
    internal sealed class EquipmentInventory
    {
        public readonly GameObject[] Items=new GameObject[6];
        public int Drawn=-1;
        private Transform _camera;
        /// <summary>Native weapon state used to detect held-item changes.</summary>
        public string WeaponState { get { return PlayerInventory.WeaponState; } }
        /// <summary>Retains the prior snapshot while an external inventory is busy.</summary>
        public bool Read()
        {
            if(_camera!=PlayerState.PlayerCamera){_camera=PlayerState.PlayerCamera;Array.Clear(Items,0,Items.Length);Drawn=-1;}
            var external=ModApi.Get<ILogicalEquipmentInventory>();
            if(external!=null)return external.Read(Items,ref Drawn);
            PlayerInventory.Read(Items,out Drawn);return true;
        }
    }
}
