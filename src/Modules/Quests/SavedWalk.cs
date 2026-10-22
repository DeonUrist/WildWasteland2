using UnityEngine;

namespace WildWasteland.Modules.Quests
{
    // a walker as stored in the save
    internal class SavedWalk
    {
        public string Name;
        public Vector3 TargetRel;
        public string CampKey;      // "camp:<key>" or empty
    }

}
