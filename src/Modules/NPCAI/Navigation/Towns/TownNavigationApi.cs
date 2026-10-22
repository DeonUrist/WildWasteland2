using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.NPCAI
{
    /// <summary>Owns live street routes, per-instance overlays and NPC entrance decisions for supplied town geometry.</summary>
    internal sealed class TownNavigationApi : ITownNavigation,IForcedWaypointProvider
    {
        private sealed class Isolated:IIsolatedNavWork{private readonly Nav.IsolatedBake _bake;internal Isolated(GameObject p){_bake=new Nav.IsolatedBake(p);}public bool Ready=>_bake.Ready;public float Progress=>_bake.Progress;public string MapFile=>_bake.MapFile;public void Step()=>_bake.Step();public void Dispose()=>_bake.Release();}
        /// <summary>Returns create isolated for the current owner state.</summary>
        public IIsolatedNavWork CreateIsolated(GameObject prefab)=>new Isolated(prefab);
        private sealed class Routing:ITownNavWork
        {internal TownNavigation Route;public bool Ready=>Route.Ready;public bool Failed=>Route.Failed;public float Progress=>Route.Progress;public bool Step()=>Route.Step();public void Dispose(){} }
        private sealed class Overlay:ITownOverlayWork
        {internal TownOverlap Work;public bool Ready=>Work.Ready;public bool Failed=>false;public float Progress=>Work.Progress;public bool Step()=>Work.Step();public void Reapply(bool d)=>Work.Reapply(d);public void FinishPartial()=>Work.FinishPartial();public void Dispose()=>Work.Dispose();}
        /// <summary>Describes the entry data used by this owner.</summary>
        private sealed class Entry {internal TownNavContext Site;internal TownApproach Approach;internal Routing Routing;internal Overlay Overlay;}
        /// <summary>Describes the gate data used by this owner.</summary>
        private sealed class Gate {internal Func<Transform,bool> Ready;internal int Waypoint;}
        private static readonly Dictionary<TownNavContext,Entry> Sites=new Dictionary<TownNavContext,Entry>();
        private readonly Dictionary<IModule,Gate> _gates=new Dictionary<IModule,Gate>();
        private IModule _owner;private int _waypoints;
        /// <summary>Starts the sliced catalog generation once.</summary>
        internal void Start(IModule owner){_owner=owner;TownOverlap.Install();_waypoints=Nav.AddWaypointProvider(owner,this,0);}
        /// <summary>Returns stop for the current owner state.</summary>
        internal void Stop(){Nav.RemoveWaypointProvider(_waypoints);foreach(var gate in _gates.Values){Nav.RemoveMakeGate(gate.Ready);Nav.RemoveWaypointProvider(gate.Waypoint);}_gates.Clear();foreach(var e in Sites.Values)e.Overlay?.Dispose();Sites.Clear();TownOverlap.Uninstall();_owner=null;}
        /// <summary>Builds or refreshes the detached live navigation context for the town.</summary>
        private static Entry Of(TownNavContext site){Entry e;if(!Sites.TryGetValue(site,out e))Sites[site]=e=new Entry{Site=site,Approach=new TownApproach(site.Plan)};return e;}
        /// <summary>Returns create routing for the current owner state.</summary>
        public ITownNavWork CreateRouting(TownNavContext site){var e=Of(site);if(e.Routing==null)e.Routing=new Routing();e.Routing.Route=new TownNavigation(site);return e.Routing;}
        /// <summary>Returns reset routing for the current owner state.</summary>
        internal static void ResetRouting(TownNavContext site){Entry e;if(Sites.TryGetValue(site,out e)&&e.Routing!=null)e.Routing.Route=new TownNavigation(site);}
        /// <summary>Returns create overlay for the current owner state.</summary>
        public ITownOverlayWork CreateOverlay(TownNavContext site,bool dressing){var e=Of(site);e.Overlay?.Dispose();e.Overlay=new Overlay{Work=new TownOverlap(site,dressing)};return e.Overlay;}
        /// <summary>Returns try next for the current owner state.</summary>
        public bool TryNext(GameObject actor,Vector3 from,Vector3 goal,out Vector3 point,out bool force){foreach(var e in Sites.Values)if(e.Site.Anchor!=null&&Waypoint(e.Site,actor,from,goal,out point,out force))return true;point=goal;force=false;return false;}
        /// <summary>Returns try next for the current owner state.</summary>
        public bool TryNext(GameObject actor,Vector3 from,Vector3 goal,out Vector3 point){bool force;return TryNext(actor,from,goal,out point,out force);}
        /// <summary>Returns waypoint for the current owner state.</summary>
        public bool Waypoint(TownNavContext s,GameObject actor,Vector3 from,Vector3 goal,out Vector3 point,out bool approach)
        {point=goal;approach=false;if(s.Anchor==null)return false;var e=Of(s);var center=s.Anchor.TransformPoint(s.Plan.WorldCenter);float radius=s.Plan.Radius+s.Plan.Feather+30;var a=from-center;var b=goal-center;float dx=b.x-a.x,dz=b.z-a.z;if(a.x*a.x+a.z*a.z>radius*radius&&b.x*b.x+b.z*b.z>radius*radius){float t=Mathf.Clamp01(-(a.x*dx+a.z*dz)/Mathf.Max(.0001f,dx*dx+dz*dz));float x=a.x+dx*t,z=a.z+dz*t;if(x*x+z*z>radius*radius)return false;}var local=s.Anchor.InverseTransformPoint(from)-s.Plan.WorldCenter;var target=s.Anchor.InverseTransformPoint(goal)-s.Plan.WorldCenter;if(!e.Approach.Inside(local)){Vector3 next;if(!e.Approach.Inside(target)){if(!s.HasWalls||!e.Approach.Around(actor,local,target,out next))return false;}else next=e.Approach.Entrance(local);point=Ground(s,s.Anchor.TransformPoint(s.Plan.WorldCenter+next));approach=true;return true;}if(e.Routing!=null&&e.Routing.Ready&&e.Routing.Route.Waypoint(from,goal,out point))return true;bool hold;Vector3 step;if(s.HasWalls&&Nav.StructureRootAt(from)==null&&s.Boundary!=null&&s.Boundary.Waypoint(local,target,out step,out hold)){point=Ground(s,s.Anchor.TransformPoint(s.Plan.WorldCenter+step));return true;}return false;}
        /// <summary>Projects a point onto the supplied town terrain when present.</summary>
        private static Vector3 Ground(TownNavContext s,Vector3 p){if(s.GroundTerrain!=null)p.y=s.GroundTerrain.SampleHeight(p)+s.GroundTerrain.transform.position.y;return p;}
        /// <summary>Returns release for the current owner state.</summary>
        public void Release(TownNavContext site){Entry e;if(site!=null&&Sites.TryGetValue(site,out e)){e.Overlay?.Dispose();Sites.Remove(site);}}
        /// <summary>Returns forget for the current owner state.</summary>
        public void Forget(Transform root){TownOverlap.Forget(root);foreach(var e in Sites.Values)if(e.Site.Anchor==null){e.Overlay?.Dispose();} }
        /// <summary>Queries actual NPC navigation tuning with an explicit provider-off fallback.</summary>
        public float Tuning(string name,float fallback){switch(name){case "NavMargin":return NavSettings.Margin;case "NavCellSize":return NavSettings.CellSize;case "NavMaxStep":return NavSettings.MaxStep;case "NavBakeRange":return NavSettings.BakeRange;case "NavBakeBudgetMs":return NavSettings.BakeBudgetMs;case "NavFieldSeconds":return NavSettings.FieldSeconds;default:return fallback;}}
        /// <summary>Copies bounded floor candidates from the native navigation provider.</summary>
        public int Cells(Transform root,bool roofed,List<Vector3> into,out bool known,out bool baked)=>Nav.FloorCells(root,roofed,into,256,out known,out baked);
        /// <summary>Queries the navigation root containing a scene point.</summary>
        public Transform Root(Vector3 point)=>Nav.StructureRootAt(point);
        /// <summary>Registers a freshly spawned actor with the NPC behavior owner.</summary>
        public void Actor(GameObject actor){if(actor!=null)Brain.Spawned(actor);}
        /// <summary>Returns register poi gate for the current owner state.</summary>
        public void RegisterPoiGate(IModule owner,Func<Transform,bool> ready,IWaypointProvider exits){RemovePoiGate(owner);if(_owner==null)return;Nav.AddMakeGate(ready);_gates[owner]=new Gate{Ready=ready,Waypoint=Nav.AddWaypointProvider(owner,exits,100)};}
        /// <summary>Returns remove poi gate for the current owner state.</summary>
        public void RemovePoiGate(IModule owner){Gate g;if(!_gates.TryGetValue(owner,out g))return;Nav.RemoveMakeGate(g.Ready);Nav.RemoveWaypointProvider(g.Waypoint);_gates.Remove(owner);}
    }
}
