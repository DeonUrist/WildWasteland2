using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
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
