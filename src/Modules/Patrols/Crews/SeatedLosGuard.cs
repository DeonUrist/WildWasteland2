using System;
using Micosmo.SensorToolkit.PlayMaker;
namespace WildWasteland.Modules.Patrols
{
    // A SensorToolkit line-of-sight action without a target object (seated crews): the stored outputs are cleared and the action
    // finishes instead of running the vanilla code. WW: Core.Hooks.LosEnter3D / LosUpdate3D prefixes.
    internal static class SeatedLosGuard
    {
        internal static void Install(Core.IModule owner)
        {
            Core.Hooks.LosEnter3D.Prefix(owner, Before);
            Core.Hooks.LosUpdate3D.Prefix(owner, Before);
        }
        internal static bool Before(SensorGetLineOfSightResult __instance)
        {
            // first line = the cheap filter: a target is set -> vanilla (no allocation, no string compare)
            var target=__instance.targetObject;
            if(target!=null&&target.Value!=null)return true;
            if(__instance.storeVisibility!=null&&!__instance.storeVisibility.IsNone)__instance.storeVisibility.Value=0f;
            if(__instance.storeIsVisible!=null&&!__instance.storeIsVisible.IsNone)__instance.storeIsVisible.Value=false;
            if(__instance.storeVisibleTransforms!=null&&!__instance.storeVisibleTransforms.IsNone)__instance.storeVisibleTransforms.Values=Array.Empty<object>();
            if(__instance.storeVisiblePositions!=null&&!__instance.storeVisiblePositions.IsNone)__instance.storeVisiblePositions.Values=Array.Empty<object>();
            if(!__instance.everyFrame)__instance.Finish();
            return false;
        }
    }
}
