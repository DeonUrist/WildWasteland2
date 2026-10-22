using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WildWasteland.Core;

namespace WildWasteland.Core
{
    /// <summary>One part of a template: parent part index, hinge path, prefab, hinge and part poses.</summary>
    public class TemplatePart
    {
        public int parent = -1;             // index of the part this one is attached to; -1 = the frame
        public string hinge = "";           // path from the frame (or the parent part) to the hinge / collider it sits on, e.g. "parts/hinge_wheel_FL"
        public string prefab = "";          // part prefab (asset root name)
        public float[] hingePos = new float[0];   // hinge local position / euler angles when moved off the prefab's (adjust tool); empty = untouched
        public float[] hingeRot = new float[0];
        public float[] pos = new float[0];        // the part's own local pose under its hinge (plates / spikes placed freely); empty = zero
        public float[] rot = new float[0];
    }
}

namespace WildWasteland.Core
{
    /// <summary>Resolved mechanical structure. The submitting owner does not mutate it after preparation.</summary>
    public sealed class VehicleStructure
    {
        public string Name, Body, Wheel, RearWheel, Engine, Radiator, SteeringWheel, Exhaust, Seat, PassengerSeat;
        public TemplatePart[] Parts;
        public readonly System.Collections.Generic.Dictionary<string, UnityEngine.GameObject> Prefabs = new System.Collections.Generic.Dictionary<string, UnityEngine.GameObject>();
        /// <summary>Looks up the exact prefab resolved by the authoring owner before preparation.</summary>
        public UnityEngine.GameObject Resolve(string name, string kind = "") { UnityEngine.GameObject value; return Prefabs.TryGetValue(kind + "|" + name, out value) ? value : null; }
    }
    /// <summary>Readiness of one exact mechanical variant, owned by Vehicles.</summary>
    public interface IPreparedVehicle { bool Ready { get; } bool Failed { get; } }
    /// <summary>Vehicles prepares dormant mechanical structures and consumes their exact native-preprocessed instances.</summary>
    public interface IVehicleAssembly
    {
        /// <summary>Queues or retrieves an exact structural variant.</summary>
        IPreparedVehicle Require(VehicleStructure structure, string bumper, bool trailer);
        /// <summary>Consumes a ready reserve and queues its replacement; never clones preprocessed state.</summary>
        UnityEngine.GameObject Consume(IPreparedVehicle prepared, UnityEngine.Vector3 scenePosition, UnityEngine.Quaternion rotation);
        /// <summary>Clears prepared structures when the submitting world ends.</summary>
        void Reset();
        bool Warming { get; }
        float LoadFraction { get; }
    }
}
