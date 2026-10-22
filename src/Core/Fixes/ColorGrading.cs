using HutongGames.PlayMaker.Actions;
using UnityEngine.UI;

namespace WildWasteland.Core
{
    internal static class ColorGrading
    {
        static readonly FixOwner Owner = new FixOwner("ColorGrading");
        static bool _installed;
        /// <summary>Removes this fix's hooks and patches so installation can be repeated safely.</summary>
        internal static void Remove() { Owner.Remove(); _installed = false; }

        internal static void Install()
        {
            if (_installed) return; _installed = true;
            Hooks.ActivateGameObjectDo.Prefix(Owner, BeforeActivate);
            Log.Always("[Core] Color grading policy ready (native graphics option forced off)");
        }

        private static bool BeforeActivate(ActivateGameObject action)
        {
            var fsm = action.Fsm;
            if (fsm == null || fsm.Name.Length != 13 || fsm.Name != "color grading" || fsm.GameObject.name != "color_grading") return true;
            var target = fsm.GetOwnerDefaultTarget(action.gameObject);
            if (target == null || target.name != "PP color grading") return true;
            target.SetActive(false);
            var enabled = fsm.Variables.FindFsmBool("color_grading");
            if (enabled != null) enabled.Value = false;
            var toggle = fsm.GameObject.GetComponent<Toggle>();
            if (toggle != null) toggle.SetIsOnWithoutNotify(false);
            return false; // OnEnter still finishes this non-repeating action through the original lifecycle.
        }
    }
}
