using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Internal;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Generic helpers of Patrol (Health FSM get/set, player pose, child/FSM lookup); Core utilities (FsmUtil) in Wave B (Wave A split of Patrols Spawning/Patrol.cs).</summary>
    internal partial class Patrol
    {
        internal static float GetHealth(GameObject who)
        {
            if (who == null) return -1f;
            var f = Core.FsmUtil.Find(who, "Health");
            var h = f != null ? f.FsmVariables.GetFsmFloat("Health") : null;
            return h != null ? h.Value : -1f;
        }

        internal static void SetHealth(GameObject who, float value)
        {
            var f = Core.FsmUtil.Find(who, "Health");
            var h = f != null ? f.FsmVariables.GetFsmFloat("Health") : null;
            if (h != null) h.Value = value;
        }

    // =============================================================== helpers

        private static bool PlayerPose(out Vector3 p, out Vector3 fwd)
        {
            var pl = Core.PlayerState.Player;
            var cam = Core.GameRefs.MainCamera;
            if (pl == null && cam == null) { p = Vector3.zero; fwd = Vector3.forward; return false; }
            p = pl != null ? pl.transform.position : cam.transform.position;
            fwd = cam != null ? cam.transform.forward : pl.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            return true;
        }

        internal static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c.name == name) return c; }
            for (int i = 0; i < root.childCount; i++) { var r = FindChild(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        internal static PlayMakerFSM FindFsm(GameObject car, string objectName, string fsmName, PlayMakerFSM[] snapshot = null)
        {
            foreach (var f in snapshot ?? car.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f.gameObject.name == objectName && f.FsmName == fsmName) return f;
            return null;
        }

        private static bool Alive(PlayMakerFSM f) { return f != null && f.gameObject != null; }
    }
}
