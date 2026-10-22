using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Developer
{
    /// <summary>Developer-owned wheel and handlebar authoring input, clipboard and settings page.</summary>
    internal sealed class WheelGripEditorUI
    {
        private readonly IModule _owner;
        private IWheelGripAuthoring _api;
        private bool _editing,_rotate,_dirty;
        private int _target,_pose,_preview=2;
        private string _key;
        private bool _handlebars, _wasAvailable;
        private float[] _copy;
        private string _copyName="",_hint="";
        private GUIStyle _style;
        private static readonly string[] Targets={"LEFT HAND","RIGHT HAND","LEFT SHOULDER","RIGHT SHOULDER","LEFT ELBOW","RIGHT ELBOW","SITTING OFFSET","TORSO ROTATION / BEND"};
        internal WheelGripEditorUI(IModule owner){_owner=owner;}
        internal void BindProvider(){var next=ModApi.Get<IWheelGripAuthoring>();if(ReferenceEquals(next,_api))return;Stop();_api=next;if(_api!=null)_api.Register(_owner,Input);}
        internal void Stop(){try{if(_api!=null)_api.Remove(_owner);}finally{_api=null;_editing=_dirty=false;_wasAvailable=false;_key=null;_hint="";}}
        internal void Tick(){BindProvider();if(_editing&&(!Plugin.EnableDebug.Value||!Plugin.DeveloperMode.Value))End();}
        private void Context(WheelGripDraft draft)
        {
            bool changed = _key != draft.Key || _handlebars != draft.Handlebars || _wasAvailable != draft.Available;
            if (changed || _editing && !draft.Editing) { _editing = _dirty = false; _hint = ""; }
            if(changed){_target=0;_pose=0;_preview=draft.Handlebars?3:2;}
            _key=draft.Key;_handlebars=draft.Handlebars;_wasAvailable=draft.Available;
            _target=Mathf.Clamp(_target,0,draft.Handlebars?7:5);_pose=Mathf.Clamp(_pose,0,draft.Handlebars?2:1);
        }
        private void End(){if(_api!=null){if(_dirty)_api.Save();var d=_api.Capture();_api.Preview(false,_pose,d.Handlebars?3:2);}_editing=_dirty=false;_hint="";}
        private static float Axis(KeyCode plus,KeyCode minus){return (GameBindings.KeyHeld(plus)?1f:0f)-(GameBindings.KeyHeld(minus)?1f:0f);}
        private int Offset(WheelGripDraft d){if(_target==6)return 84;if(_target==7)return 87+_pose*3;return 12+_pose*24+(_target&1)*12+(_target/2==1?6:_target/2==2?9:_rotate?3:0);}
        private bool Rotation {get{return _target==7||_target/2==1||_target/2==0&&_rotate;}}
        private void Publish(WheelGripDraft d){_api.SetValues(d.Key,d.Values,_pose,_target&1);_dirty=true;}
        private void Copy(WheelGripDraft d){_api.Save();_copy=(float[])d.Values.Clone();_copyName=d.Name;}
        private void Paste(WheelGripDraft d){if(_copy==null)return;Array.Copy(_copy,d.Values,Math.Min(_copy.Length,d.Values.Length));if(d.Handlebars&&_copy.Length<84)for(int pose=1;pose<3;pose++)Array.Copy(d.Values,12,d.Values,12+pose*24,24);if(d.Handlebars&&_copy.Length<=84)Array.Clear(d.Values,84,12);Publish(d);_api.ResetMotion();_api.Save();_dirty=false;}
        private void Input()
        {
            if(_api==null)return;var d=_api.Capture();Context(d);if(!d.Available){if(_editing)End();return;}
            bool ready=Plugin.EnableDebug.Value&&Plugin.DeveloperMode.Value;
            if(!ready){if(_editing)End();return;}
            if(Time.timeScale<.01f||ToolWindow.IsOpen||ApocasetterBridge.InputBlocked||!Application.isFocused){if(_dirty){_api.Save();_dirty=false;}return;}
            if(_editing&&GameBindings.KeyDown(KeyCode.KeypadPeriod)){End();return;}if(!_editing)return;
            if(d.Owner!="wheel"){if(_dirty){_api.Save();_dirty=false;}return;}
            if(GameBindings.KeyDown(KeyCode.Keypad0))_target=(_target+1)%(d.Handlebars?8:6);
            if(GameBindings.KeyDown(KeyCode.Keypad5))_rotate=!_rotate;
            if(GameBindings.KeyDown(KeyCode.Keypad9)){_pose=(_pose+1)%(d.Handlebars?3:2);_preview=_pose;}
            if(GameBindings.KeyDown(KeyCode.Keypad3)){int live=d.Handlebars?3:2;_preview=_preview==live?_pose:live;}
            if(GameBindings.KeyDown(KeyCode.KeypadEnter)){_api.Save();_dirty=false;}
            if(GameBindings.KeyDown(KeyCode.KeypadDivide))Copy(d);if(GameBindings.KeyDown(KeyCode.KeypadMultiply))Paste(d);
            float up=Axis(KeyCode.Keypad8,KeyCode.Keypad2),right=Axis(KeyCode.Keypad6,KeyCode.Keypad4),forward=Axis(KeyCode.Keypad7,KeyCode.Keypad1);
            if(up!=0||right!=0||forward!=0)
            {
                _preview=_pose;Vector3 delta=Rotation?new Vector3(up,right,forward):new Vector3(right,up,(d.Handlebars?1:d.TowardDriver)*forward);
                float rate=Rotation?20:5;if(GameBindings.KeyHeld(KeyCode.LeftShift)||GameBindings.KeyHeld(KeyCode.RightShift))rate*=.2f;delta*=rate*Mathf.Min(Time.unscaledDeltaTime,.05f);
                float limit=_target==6?50:_target==7?60:Rotation?90:d.Handlebars?150:50;int offset=Offset(d);
                for(int i=0;i<3;i++)d.Values[offset+i]=Mathf.Clamp(d.Values[offset+i]+delta[i],-limit,limit);Publish(d);
            }
            else if(_dirty){_api.Save();_dirty=false;}
            _api.Preview(true,_pose,_preview);UpdateHint(d);
        }
        private void UpdateHint(WheelGripDraft d){int o=Offset(d);_hint="WHEEL "+d.Name+" | "+Targets[_target]+" | "+(Rotation?"ROTATE":"MOVE")+" | pose "+_pose+" / "+(_preview==(d.Handlebars?3:2)?"LIVE":"PREVIEW")+"\n"+d.Values[o].ToString("F1")+" / "+d.Values[o+1].ToString("F1")+" / "+d.Values[o+2].ToString("F1")+(Rotation?" degrees":" cm")+" | owner "+d.Owner+((d.LeftLimited||d.RightLimited)?" | REACH LIMIT":"")+"\nNum0 target | Num5 mode | Num9 pose | Num3 live/preview | Num8/2 up | Num6/4 right | Num7/1 forward | Shift fine | NumEnter save | Num/ copy | Num* paste | Num decimal stop";}
        internal bool WantsGui(){return _editing&&!ToolWindow.IsOpen&&Application.isFocused&&Time.timeScale>.01f;}
        internal void DrawHint(){if(!WantsGui())return;if(_style==null)_style=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,alignment=TextAnchor.MiddleCenter};var r=new Rect(20,Screen.height-132,Screen.width-40,116);var old=GUI.color;GUI.color=Color.black;GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),_hint,_style);GUI.color=Color.white;GUI.Label(r,_hint,_style);GUI.color=old;}
        internal void DrawPage(ToolLayout l)
        {
            if(_api==null){l.Text("Enable Presentation to edit steering-wheel and handlebar grips.");return;}var d=_api.Capture();Context(d);l.Value("Wheel model / setup",d.Name+" / "+d.Key);l.Value("Arm owner",d.Owner??"");if(!d.Available)return;
            l.Text(d.Handlebars?"Independent left/right grips, shoulders and elbows for straight/left/right steering; sitting offset and torso rotation are included.":"SIDE/TOP poses use mirrored arm fits. Editing either side updates its mirrored pair.");
            if(l.Button(_editing?"Stop live wheel editor":"START live numpad editor",true,true)){if(_editing)End();else ToolWindow.CloseThen(()=>{_editing=true;_preview=_pose;_api.Preview(true,_pose,_preview);});}
            if(l.Button("SAVE + COPY wheel (Num /)"))Copy(d);if(l.Button("PASTE wheel (Num *)",_copy!=null))Paste(d);if(_copy!=null)l.Value("Copied wheel",_copyName);
            int b=d.Handlebars?l.Buttons("STRAIGHT","LEFT TURN","RIGHT TURN","LIVE steering"):l.Buttons("SIDE pose","TOP pose","LIVE steering");if(b>=0){_preview=b;if(b<(d.Handlebars?3:2))_pose=b;_editing=true;_api.Preview(true,_pose,_preview);}
            if(l.Button("Editing target: "+Targets[_target]+" (next)"))_target=(_target+1)%(d.Handlebars?8:6);
            if(_target/2==0&&l.Button(_rotate?"Hand mode: ROTATE":"Hand mode: MOVE"))_rotate=!_rotate;
            int offset=Offset(d);float limit=_target==6?50:_target==7?60:Rotation?90:d.Handlebars?150:50;bool changed=false;
            for(int i=0;i<3;i++){float before=d.Values[offset+i];d.Values[offset+i]=l.Slider((Rotation?new[]{"Pitch","Yaw","Roll"}:new[]{"Right (cm)","Up (cm)","Forward (cm)"})[i],d.Key+"/"+_pose+"/"+_target+"/"+i,before,-limit,limit);changed|=before!=d.Values[offset+i];}
            l.Value("Requested / available reach (cm)",(d.Requested[_target&1]*100).ToString("F1")+" / "+(d.Reach[_target&1]*100).ToString("F1"));
            if(!d.Handlebars&&l.Button("Copy SIDE to TOP for both arms")){Array.Copy(d.Values,12,d.Values,36,24);changed=true;}
            if(!d.Handlebars){l.Value("Measured rim diameter (cm)",d.RimDiameter.ToString("F1"));float old=d.Values[0];d.Values[0]=l.Slider("Rim radius (%)",d.Key+"/Radius",old*100,50,110)*.01f;changed|=old!=d.Values[0];}
            float[] min={-10,0,-35,-60,120},max={10,1.2f,35,60,180};string[] labels={"Palm lift (cm)","Palm reach","Grip elevation","Elbow swing","Cross-wheel pull"};
            for(int i=1;i<=5;i++){if(d.Handlebars&&(i==3||i==5))continue;float scale=i==1?100:i==5?2:1,old=d.Values[i];d.Values[i]=l.Slider(labels[i-1],d.Key+"/"+i,old*scale,min[i-1],max[i-1])/scale;changed|=old!=d.Values[i];}
            if(changed){Publish(d);_api.Save();_dirty=false;}
            b=l.Buttons("SAVE this wheel","RESET this wheel");if(b==0)_api.Save();else if(b==1)_api.Reset();l.Value("File",d.FilePath??"");
        }
    }
}
