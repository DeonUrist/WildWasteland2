using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>POI-owned encounter locations and notice deadline, queried by HUD through a typed service.</summary>
    internal sealed class EncounterNotices:IEncounterNotices
    {
        internal static readonly List<GameObject> Rigs=new List<GameObject>(); internal static float NoticeUntilValue;
        public float NoticeUntil=>NoticeUntilValue;
        public int RigCount {get{for(int i=Rigs.Count-1;i>=0;i--)if(Rigs[i]==null)Rigs.RemoveAt(i);return Rigs.Count;}}
        public Vector3 RigPosition(int index)=>Origin.World(Rigs[index].transform.position);
        internal static void Clear(){Rigs.Clear();NoticeUntilValue=0;}
    }
}
