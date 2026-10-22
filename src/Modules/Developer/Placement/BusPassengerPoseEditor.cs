using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.Developer
{
    // Keyboard overlay: no cursor capture, scene scans, or extra work while disabled.
    internal sealed class BusPassengerPoseEditor : MonoBehaviour
    {
        internal static GameObject Target;
        /// <summary>Reports whether the actual Developer bus-pose tool is enabled.</summary>
        internal static bool Active=>PatrolEditorRuntime.BusPassengerPoseEditorEnabled!=null&&PatrolEditorRuntime.BusPassengerPoseEditorEnabled.Value;
        private static readonly string[] Joints={"Thigh","Knee","Upper arm","Elbow","Wrist","Seat position"};
        private int _joint,_side;private float _nextFind;private string _status="";
        // ---- F7 "Bus passenger pose editor" page
        /// <summary>Returns the currently selected bus-pose joint label.</summary>
        internal string JointName=>Joints[_joint];
        /// <summary>Returns the selected mirrored, left or right editing mode.</summary>
        internal string SideName=>_side==0?"Both (mirrored)":_side==1?"Left":"Right";
        /// <summary>Returns the current bounded placement editor status message.</summary>
        internal string Status=>_status;
        internal string ValueText {get{var api=Core.ModApi.Get<Core.IBusPassengerAuthoring>();return api==null?"unavailable":_joint==5?api.Value(5,0).ToString("F2"):"L "+api.Value(_joint,1).ToString("F0")+"  R "+api.Value(_joint,2).ToString("F0");}}
        /// <summary>Selects the next supported joint or seat-offset vector.</summary>
        internal void NextJoint(){_joint=(_joint+1)%Joints.Length;}
        /// <summary>Cycles mirrored, left and right joint editing modes.</summary>
        internal void NextSide(){_side=(_side+1)%3;}
        /// <summary>Publishes current Presentation-owned bus-pose values atomically.</summary>
        internal void SavePose(){_status=Core.ModApi.Get<Core.IBusPassengerAuthoring>()?.Save()??"Presentation unavailable";}
        /// <summary>Restores current Presentation-owned unsaved bus-pose defaults.</summary>
        internal void ResetPose(){Core.ModApi.Get<Core.IBusPassengerAuthoring>()?.Reset();_status="Defaults restored; press Num5 to save";}
        /// <summary>Formats the configured native tool toggle key.</summary>
        private static string KeyText=>PatrolEditorRuntime.BusPassengerPoseEditorKey!=null&&PatrolEditorRuntime.BusPassengerPoseEditorKey.Value!=Key.None?PatrolEditorRuntime.BusPassengerPoseEditorKey.Value.ToString():"F7 page";
        // WW: was Update (Update job); the toggle key is the Core hotkey [Keys] Bus passenger pose editor
        /// <summary>Updates enabled placement selection, input and temporary native markers.</summary>
        internal void Tick()
        {
            var api=Core.ModApi.Get<Core.IBusPassengerAuthoring>();
            if(!Active||api==null){Target=null;api?.Select(null);return;}
            if(Time.unscaledTime>=_nextFind)
            {
                _nextFind=Time.unscaledTime+.5f;var player=Core.PlayerState.Transform;var camera=Core.GameRefs.MainCamera;
                Target=camera!=null||player!=null?api.Nearest(camera!=null?camera.transform.position:player.position,80):null;api.Select(Target);
            }
            if(Target==null)return;
            if(Time.timeScale<=0f)return;   // WW: no numpad edits while F7 (or another editor) pauses the game
            if(Core.Hotkeys.Pressed(Key.Numpad0))NextJoint();
            if(Core.Hotkeys.Pressed(Key.NumpadDivide))NextSide();
            if(Core.Hotkeys.Pressed(Key.Numpad5))SavePose();
            if(Core.Hotkeys.Pressed(Key.NumpadPeriod))ResetPose();
            var kb=Keyboard.current;bool shift=kb!=null&&(kb.leftShiftKey.isPressed||kb.rightShiftKey.isPressed);
            float step=_joint==5?.01f:1f;if(shift)step*=5f;
            var delta=Vector3.zero;
            if(Core.Hotkeys.Pressed(Key.Numpad8))delta.x+=step;if(Core.Hotkeys.Pressed(Key.Numpad2))delta.x-=step;
            if(Core.Hotkeys.Pressed(Key.Numpad6))delta.y+=step;if(Core.Hotkeys.Pressed(Key.Numpad4))delta.y-=step;
            if(Core.Hotkeys.Pressed(Key.Numpad9))delta.z+=step;if(Core.Hotkeys.Pressed(Key.Numpad7))delta.z-=step;
            if(delta==Vector3.zero)return;
            api.Adjust(_joint,_side,delta);
            _status="Unsaved live changes";
        }
        /// <summary>Clamps a native authoring vector to its existing configured limits.</summary>
        private static Vector3 Clamp(Vector3 value,float limit)=>new Vector3(Mathf.Clamp(value.x,-limit,limit),Mathf.Clamp(value.y,-limit,limit),Mathf.Clamp(value.z,-limit,limit));
        /// <summary>Draws the current keyboard placement overlay without capturing the cursor.</summary>
        internal void Gui()
        {
            if(!Active||Event.current.type!=EventType.Repaint)return;
            if(Target==null){GUI.Box(new Rect(12,12,560,65),"Bus passenger pose editor: no seated bus passenger within 80 m\nSpawn a bus template with a passenger. Toggle: "+KeyText);return;}
            string value=ValueText;
            GUI.Box(new Rect(12,12,670,145),"Bus passenger pose: "+Target.name+"\n"+Joints[_joint]+" / "+(_side==0?"Both (mirrored)":_side==1?"Left":"Right")+"  "+value
                +"\nNum0 next joint   Num/ both/left/right   Shift faster"
                +"\nNum8/2 bend   Num4/6 sideways   Num7/9 twist"
                +"\nSeat: Num8/2 forward/back   Num4/6 left/right   Num7/9 down/up"
                +"\nNum5 save   Num. reset   "+KeyText+" close\n"+_status);
        }
        /// <summary>Releases selected visual edit ownership and destroys owned temporary marker materials.</summary>
        private void OnDestroy(){Target=null;Core.ModApi.Get<Core.IBusPassengerAuthoring>()?.Select(null);}
    }
}
