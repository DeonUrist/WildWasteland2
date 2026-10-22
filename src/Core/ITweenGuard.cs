using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Core
{
    // iTween Guard 1.0.0 (d7f5411): always-on fix, no config or module switch.
    // iTween bug: a tween started on an object with a Rigidbody sets physics = true and keeps calling
    // GetComponent<Rigidbody>().MoveRotation/MovePosition in FixedUpdate. When the body is later removed (item picked up,
    // part bolted onto a car) every physics tick throws "NullReferenceException at iTween.ApplyRotateToTargets".
    // Fix: before each tween update, if the body is gone, switch the tween to non-physics mode (iTween's Update then drives it).
    internal static class ITweenGuard
    {
        internal const string GUID = "com.denis.apocalypter.itweenguard";
        static int _fixed;

        internal static void Init()
        {
            var stop = AccessTools.Method(typeof(iTween), "Stop", new[] { typeof(GameObject), typeof(string) });
            if (Patches.Patch(null, stop, transpiler: AccessTools.Method(typeof(ITweenGuard), nameof(StopTranspiler))))
                Log.Always("[Core] iTween stop guard ready (always on)");
            BepInEx.PluginInfo info;
            if (Chainloader.PluginInfos.TryGetValue(GUID, out info) && !ReferenceEquals(info.Instance, null))
            {
                Log.Warn("[Core] iTween guard: " + ReplacementGuard.Status(GUID) + ". Built-in fix waits until restart.");
                return;
            }
            var target = AccessTools.Method(typeof(iTween), "TweenUpdate");
            if (target == null) { Log.Warn("[Core] iTween guard: iTween.TweenUpdate not found; nothing patched"); return; }
            if (Patches.Patch(null, target, prefix: AccessTools.Method(typeof(ITweenGuard), nameof(Prefix))))
                Log.Always("[Core] iTween guard ready (always on)");
        }

        static IEnumerable<CodeInstruction> StopTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var substring = AccessTools.Method(typeof(string), "Substring", new[] { typeof(int), typeof(int) });
            var guarded = AccessTools.Method(typeof(ITweenGuard), nameof(StopPrefix));
            int replaced = 0;
            foreach (var ci in code)
                if (ci.Calls(substring)) { ci.opcode = OpCodes.Call; ci.operand = guarded; replaced++; }
            if (replaced != 1) throw new InvalidOperationException("iTween.Stop: expected one Substring call, found " + replaced);
            return code;
        }

        // A shorter tween name cannot match the requested prefix; leave it short so Stop continues with the other tweens.
        static string StopPrefix(string value, int startIndex, int length)
        {
            return value.Length < length ? value : value.Substring(startIndex, length);
        }

        // ___physics / ___kinematic: iTween's private fields, accessed directly by Harmony (no reflection per call).
        static void Prefix(iTween __instance, ref bool ___physics, ref bool ___kinematic)
        {
            if (!___physics) return;
            try
            {
                if (__instance.GetComponent<Rigidbody>() != null) return;
                ___physics = false;
                ___kinematic = false;   // iTween would also touch the missing body when the tween ends
                _fixed++;
                if (Log.Verbose && _fixed <= 20)
                {
                    string path = __instance.name; for (var p = __instance.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
                    Log.Info("[Core] iTween guard: repaired a tween whose Rigidbody was removed: " + path);
                }
            }
            catch (Exception) { }
        }
    }
}
