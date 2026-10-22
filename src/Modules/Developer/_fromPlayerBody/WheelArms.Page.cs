using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ES3Internal;
using NWH.VehiclePhysics2;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// WheelArms part (split from PlayerBody/Vehicles/WheelArms.cs in Wave A): the F7 "Steering-wheel hands" page and the grip-setup save
    /// (WheelGrips/<key>.txt). Target: Developer (B16), through Presentation seat-pose authoring in Wave B.
    /// </summary>
    internal static partial class WheelArms
    {
        private static void Save()
        {
            try
            {
                if (!_handlebars) for (int pose = 0; pose < 2; pose++) MirrorPose(_setup.V, pose, 1);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var fields = new string[_setup.V.Length];
                for (int i = 0; i < fields.Length; i++) fields[i] = _setup.V[i].ToString("R", Inv);
                File.WriteAllText(FilePath, string.Join(" ", fields)); _dirty = false;
                PlayerBodyModule.Info("Wheel grip setup saved: " + FilePath);
            }
            catch (Exception e) { PlayerBodyModule.Warn("Saving wheel grip setup: " + e.Message); }
        }

        internal static void Draw(ToolLayout l)
        {
            l.Heading("Steering-wheel hands");
            l.Text(_handlebars ? "One setup for all motorcycles, with independent left/right hand, wrist, shoulder and elbow calibration for each steering pose. Sitting offset applies to every turn; torso bend/rotation is saved for each steering pose." : "Each wheel's right-arm fit is mirrored to its left arm. Editing either side updates the mirrored pair; left-turn motion mirrors the right-turn sequence.");
            l.Text(_handlebars ? "Start with the button below; numpad decimal stops the editor. Num9 selects STRAIGHT / LEFT TURN / RIGHT TURN; Num3 preview/live. Num0 selects left/right hand/shoulder/elbow, sitting offset, torso rotation/bend. Num5 hand move/rotate; 8/2 up/pitch, 6/4 right/yaw, 7/1 forward-back/roll. Enter saves." : "Per-wheel SIDE and TOP poses. Start with the button below; numpad decimal stops the editor. 0 selects hand/shoulder/elbow, 5 move/rotate, 9 side/top, 3 live steering. 8/2 up/down, 6/4 right/left, 7/1 toward/away. Numpad Enter saves. Edits also save on key release.");
            l.Value("Wheel model / setup", _setup != null ? _name + " / " + _key : "no fitted wheel");
            l.Value("Arm owner", _owner);
            if (_setup == null || _mount == null || _wheel == null) return;
            if (l.Button(_editor ? "Stop live wheel editor" : "START live numpad editor", true, true))
            {
                if (_editor) StopEditor(); else ToolWindow.CloseThen(StartEditor);
            }
            DrawPoseEditor(l);
            var v = _setup.V;
            if (!_handlebars) l.Value("Measured rim diameter (cm)", (200f * _radius.x * Mathf.Abs(_mount.lossyScale.x)).ToString("F1", Inv));
            if (!_handlebars) v[0] = l.Slider("Rim radius (%)", _key + "/Radius", v[0] * 100f, 50f, 110f) * .01f;
            v[1] = l.Slider(_handlebars ? "Palm lift above grip (cm)" : "Palm lift toward driver (cm)", _key + "/Lift", v[1] * 100f, -10f, 10f) * .01f;
            v[2] = l.Slider("Palm reach", _key + "/Reach", v[2], 0f, 1.2f);
            if (!_handlebars) v[3] = l.Slider("Grip elevation (degrees)", _key + "/Elevation", v[3], -35f, 35f);
            v[4] = l.Slider("Elbow swing (degrees)", _key + "/Elbow", v[4], -60f, 60f);
            if (!_handlebars) v[5] = l.Slider("Cross-wheel pull (degrees)", _key + "/Stroke", Mathf.Clamp(v[5] * 2f, 120f, 180f), 120f, 180f) * .5f;
            int hit = l.Buttons("SAVE this wheel", "RESET this wheel");
            if (hit == 0) Save();
            else if (hit == 1) { _setup = Load(true); _setups[_key] = _setup; Save(); }
        }
    }
}
