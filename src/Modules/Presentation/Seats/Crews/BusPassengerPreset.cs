using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.Patrols
{
    [Serializable] internal sealed class BusPassengerPreset
    {
        // Forward bend, sideways swing, axial twist. Knees bend opposite to thighs.
        public Vector3[] left={new Vector3(85f,-5f,0f),new Vector3(-90f,0f,0f),new Vector3(25f,-5f,0f),new Vector3(25f,0f,0f),Vector3.zero};
        public Vector3[] right={new Vector3(85f,5f,0f),new Vector3(-90f,0f,0f),new Vector3(25f,5f,0f),new Vector3(25f,0f,0f),Vector3.zero};
        public Vector3 seatOffset;
    }
}
