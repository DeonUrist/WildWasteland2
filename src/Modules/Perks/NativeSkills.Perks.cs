using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using WildWasteland.Core;

namespace WildWasteland.Modules.Perks
{
    internal sealed partial class NativeSkills
    {
        private readonly Dictionary<FloatAdd,FsmFloat> _drains = new Dictionary<FloatAdd,FsmFloat>();
        private readonly Dictionary<Wait,FsmFloat> _hostility = new Dictionary<Wait,FsmFloat>();
        private void InitNativePerks(IModule owner)
        { Hooks.EnterState.Prefix(owner,PerkState); Scheduler.Interval(owner,"Perks.Native effects",.25f,RefreshNativePerks); }
        private bool PerkState(Fsm fsm,FsmState state)
        {
            if (fsm.Name == "Hunger" || fsm.Name == "Thirst")
                foreach (var action in state.Actions)
                {
                    var add = action as FloatAdd;
                    if (add != null && add.perSecond && add.add.Value > 0 && (add.floatVariable.Name == "Hunger" || add.floatVariable.Name == "Thirst") && !_drains.ContainsKey(add))
                    { _drains[add] = add.add; add.add = new FsmFloat(add.add.Value); }
                }
            if (fsm.Name == "FactionRelation_Coyotes" && state.Name == "PlayerEnemy")
                foreach (var action in state.Actions) { var wait = action as Wait; if (wait == null) continue; FsmFloat original; if (!_hostility.TryGetValue(wait,out original)) { original = wait.time; _hostility.Add(wait,original); } wait.time = new FsmFloat(original.Value * (SkillPerks.Has("FriendlyFace") ? .5f : 1)); }
            RefreshNativePerks(); return true;
        }
        private void RefreshNativePerks()
        { foreach (var pair in _drains) pair.Key.add.Value = pair.Value.Value / (SkillPerks.Has("Fasting") ? 1.5f : 1); }
        private void ClearNativePerks()
        { foreach (var pair in _drains) pair.Key.add = pair.Value; foreach (var pair in _hostility) pair.Key.time = pair.Value; _drains.Clear(); _hostility.Clear(); }
    }
}
