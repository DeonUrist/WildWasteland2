using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.POI
{
    /// <summary>POI-owned encounter locations and notice deadline, queried by HUD through a typed service.</summary>
    internal sealed class EncounterNotices:IEncounterNotices
    {
        internal static readonly List<GameObject> Rigs=new List<GameObject>(); internal static float NoticeUntilValue;
        /// <summary>Returns the deadline of the current encounter notice.</summary>
        public float NoticeUntil=>NoticeUntilValue;
        /// <summary>Returns for for the current owner state.</summary>
        public int RigCount {get{for(int i=Rigs.Count-1;i>=0;i--)if(Rigs[i]==null)Rigs.RemoveAt(i);return Rigs.Count;}}
        /// <summary>Returns an encounter marker in stable world coordinates.</summary>
        public Vector3 RigPosition(int index)=>Origin.World(Rigs[index].transform.position);
        /// <summary>Releases current resources and resets the associated world or catalog state.</summary>
        internal static void Clear(){Rigs.Clear();NoticeUntilValue=0;}
    }
}
