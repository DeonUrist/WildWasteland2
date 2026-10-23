using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    public interface ISkillMerchantChoices { void Append(GameObject speaker, IList<QuestDialogueButton> choices); bool ResetWanted(GameObject station); }
    public interface ISkillMerchantRuntime
    {
        bool Resolve(GameObject speaker, out GameObject merchant);
        string Identity(GameObject merchant);
        bool Reroll(GameObject merchant, bool stock);
    }
}
