using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    // Release tuning and layout are fixed. Player options control new spawns.
    internal static class S
    {
        public static ConfigEntry<bool> SpawnEnabled;
        public static ConfigEntry<bool> GroundRecoveryEnabled;
        public const float Distance = 14f;
        public const bool EquipWheels = false; // Same bare-frame discovery as native trailers.
        public const string WheelItem = "truck_wheel_1";
        public const string ModelFile = "WarRig.glb";
        public const float Scale = 1f;
        public const float Yaw = 180f;
        public const string Offset = "0, 0, 0";
        public const float Mass = 2000f;
        public const string CenterOfMass = "0, 0.3, -1.2";
        public const string Axles = "1.82, 3.31, 4.82";
        public const float AxleTrack = 1.45f;
        public const float HubHeight = -0.78f;
        public const float SpringForce = 0f;
        public const float SpringLength = 0f;
        public const float DamperBump = 0f;
        public const float DamperRebound = 0f;
        public const string Attacher = "0, -0.45, -10.87";
        public const string AllowedTrucks = "Rustchief";
        public const float AttachRadius = 0.3f;
        public const string Leg = "0.9, -0.1, -9.6";
        public const float LegLength = 0f;
        public const float HitchHeight = 0.95f;
        public const string ColliderFile = "WarRig_colliders.glb";
        public const string TankBox = "0, 1.4, -1.25 ; 4.8, 2.9, 19.0";
        public const string FrameBox = "0, -0.31, -1.8 ; 1.9, 0.62, 18.9";
        public const string FrontBox = "-0.15, 3.51, -8.68 ; 3.75, 1.32, 3.1";
        public const string RearBox = "0, 3.59, 6.9 ; 2.06, 1.48, 1.7";
        public const string LockBox = "0, 0.95, -1.25 ; 4.4, 2.0, 17.7";

        // Patrols binds the spawn/recovery toggles; POI owns the world-generation chance.

        public static Vector3 V3(string s, Vector3 def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            var p = s.Split(',');
            if (p.Length != 3) return def;
            float x, y, z;
            if (!F(p[0], out x) || !F(p[1], out y) || !F(p[2], out z)) return def;
            return new Vector3(x, y, z);
        }

        public static bool Box(string s, out Vector3 center, out Vector3 size)
        {
            center = size = Vector3.zero;
            if (string.IsNullOrEmpty(s) || s.IndexOf(';') < 0) return false;
            var p = s.Split(';');
            center = V3(p[0], new Vector3(float.NaN, 0, 0)); size = V3(p[1], new Vector3(float.NaN, 0, 0));
            return !float.IsNaN(center.x) && !float.IsNaN(size.x) && size.x > 0 && size.y > 0 && size.z > 0;
        }

        public static float[] Floats(string s)
        {
            var p = (s ?? "").Split(',');
            var r = new System.Collections.Generic.List<float>();
            foreach (var x in p) { float f; if (F(x, out f)) r.Add(f); }
            return r.ToArray();
        }

        private static bool F(string s, out float f) { return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f); }
    }
}
