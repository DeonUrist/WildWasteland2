using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Developer
{
    /// <summary>Developer-owned weapon/arm input, clipboard and display over Presentation's actual pose store.</summary>
    internal sealed class CharacterPoseEditor
    {
        private readonly IModule _owner;
        private ICharacterPoseAuthoring _api;
        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<string>[] _keys = new ConfigEntry<string>[8];
        private static readonly string[] Labels = { "Select weapon / arm", "Move / rotate", "Up / pitch up", "Down / pitch down", "Right / yaw right", "Left / yaw left", "Forward / roll right", "Back / roll left" };
        private bool _rotate, _moving, _aimDirty;
        private int _binding = -1;
        private float[] _weaponCopy, _aimCopy;
        private string _hint = "", _aimHint = "", _status = "";
        private int _hintFrame = -10;
        private GUIStyle _style;
        internal CharacterPoseEditor(IModule owner, ModuleConfig cfg)
        {
            _owner=owner;
            _enabled=cfg.Tool("Weapon poses","Enabled",false,"Enable live weapon, arm and aim pose authoring. Keys act only in third person with a drawn weapon.");
            string[] defaults={"Keypad0","Keypad5","Keypad8","Keypad2","Keypad6","Keypad4","Keypad7","Keypad1"};
            for(int i=0;i<_keys.Length;i++)_keys[i]=cfg.Tool("Weapon poses","Pose key: "+Labels[i],defaults[i],"Developer weapon pose key; None disables it.");
        }
        internal void BindProvider()
        {
            var next=ModApi.Get<ICharacterPoseAuthoring>();if(ReferenceEquals(next,_api))return;
            try { if(_api!=null)_api.Remove(_owner); } finally { _api=null; }
            _api=next;_moving=_aimDirty=false;_hint=_aimHint="";
            if(_api!=null)_api.Register(_owner,WeaponInput,AimInput);
        }
        internal void Stop(){try{if(_api!=null)_api.Remove(_owner);}finally{_api=null;_moving=_aimDirty=false;_hint=_aimHint="";}}
        internal void Tick(){BindProvider();if(_api!=null)_api.Editing=_enabled.Value && Plugin.EnableDebug.Value && Plugin.DeveloperMode.Value;}
        private bool InputAllowed { get { return _api!=null&&_api.Editing&&Application.isFocused&&!ApocasetterBridge.InputBlocked&&!ToolWindow.IsOpen&&Time.timeScale>.01f; } }
        private KeyCode Key(int index){KeyCode key;return Enum.TryParse(_keys[index].Value,true,out key)?key:KeyCode.None;}
        private bool Down(int index){return GameBindings.KeyDown(Key(index));}
        private float Axis(int plus,int minus){return (GameBindings.KeyHeld(Key(plus))?1f:0f)-(GameBindings.KeyHeld(Key(minus))?1f:0f);}
        private PoseEditMotion WeaponInput()
        {
            var draft=_api.Capture();
            if(!InputAllowed||!draft.ThirdPerson||string.IsNullOrEmpty(draft.Weapon)){FinishMotion();_hint="";return default(PoseEditMotion);}
            _binding=-1;
            if(Down(0)){FinishMotion();_api.Target=(_api.Target+1)%3;}
            if(Down(1))_rotate=!_rotate;
            if(GameBindings.KeyDown(KeyCode.Keypad9)){FinishMotion();_api.StepClip(1);}
            if(GameBindings.KeyDown(KeyCode.Keypad3)){FinishMotion();_api.StepClip(-1);}
            if(GameBindings.KeyDown(KeyCode.KeypadMinus)){FinishMotion();_api.DeleteWeapon();_api.Save();}
            if(GameBindings.KeyDown(KeyCode.KeypadDivide))Copy();
            if(GameBindings.KeyDown(KeyCode.KeypadMultiply))Paste();
            float forward=Axis(6,7),up=Axis(2,3),right=Axis(4,5);
            Vector3 move=_rotate?Vector3.zero:new Vector3(right,up,forward)*3f;
            Vector3 rotation=_rotate?new Vector3(up,right,forward)*45f:Vector3.zero;
            bool any=move!=Vector3.zero||rotation!=Vector3.zero;
            if(!any)FinishMotion();else _moving=true;
            _hintFrame=Time.frameCount;
            _hint=Target+" | "+(_rotate?"ROTATING":"MOVING")+" | "+draft.Weapon+" | "+draft.Clip+" | "+draft.Status+"\n"+
                  "Num0 target | Num5 move/rotate | Num9/3 clip | Num8/2 up/pitch | Num6/4 right/yaw | Num7/1 forward/roll | Num- delete | Num/ copy | Num* paste";
            if(_api.Target!=0){_api.ArmMotion(_api.Target==1,move,rotation,Time.unscaledDeltaTime);return default(PoseEditMotion);}
            return new PoseEditMotion{Apply=any,Move=move,Rotation=rotation};
        }
        private string Target { get { return _api==null?"WEAPON":_api.Target==1?"LEFT ARM":_api.Target==2?"RIGHT ARM":"WEAPON"; } }
        private void FinishMotion(){if(_moving&&_api!=null){_api.EndMotion();_moving=false;}}
        private void AimInput(string weapon,string clip,bool crouched,float dt,float gap)
        {
            if(!InputAllowed||string.IsNullOrEmpty(weapon)||string.IsNullOrEmpty(clip)){if(_aimDirty){_api.Save();_aimDirty=false;}_aimHint="";return;}
            float lift=(GameBindings.KeyHeld(KeyCode.PageUp)?1f:0f)-(GameBindings.KeyHeld(KeyCode.PageDown)?1f:0f);
            float head=(GameBindings.KeyHeld(KeyCode.End)?1f:0f)-(GameBindings.KeyHeld(KeyCode.Home)?1f:0f);
            float forward=(GameBindings.KeyHeld(KeyCode.Insert)?1f:0f)-(GameBindings.KeyHeld(KeyCode.Delete)?1f:0f);
            var values=_api.GetAim(weapon,clip,crouched);
            if(lift!=0f||head!=0f||forward!=0f){values[0]=Mathf.Clamp(values[0]+lift*5f*dt,-20,45);values[1]=Mathf.Clamp(values[1]+head*15f*dt,-30,40);values[2]=Mathf.Clamp(values[2]+forward*5f*dt,-20,30);_api.SetAim(weapon,clip,crouched,values);_aimDirty=true;}
            else if(_aimDirty){_api.Save();_aimDirty=false;}
            _aimHint="AIM LIFT "+weapon+" | "+clip+(crouched?" CROUCHED":" STANDING")+": lift "+values[0].ToString("0.0")+" cm, head "+values[1].ToString("0")+" degrees, forward "+values[2].ToString("0.0")+" cm | gun top "+gap.ToString("0")+" cm below shot origin\nPage Up/Down lift | Home/End head | Insert/Delete forward";
        }
        private bool Copy(){if(_api==null)return false;var d=_api.Capture();if(d.Shown==null||!d.CanEditArms)return false;_weaponCopy=(float[])d.Shown.Clone();_aimCopy=_api.GetAim(d.AimWeapon,d.AimClip,d.Crouched);return true;}
        private bool Paste(){if(_api==null||_weaponCopy==null||_aimCopy==null)return false;var d=_api.Capture();if(!d.CanEditArms)return false;FinishMotion();_api.SetWeapon(_weaponCopy);_api.SetAim(d.Weapon,d.Clip,d.Crouched,_aimCopy);_api.Save();return true;}
        internal bool WantsGui(){return _api!=null&&_api.Editing&&Time.frameCount-_hintFrame<=1&&!ToolWindow.IsOpen;}
        internal void DrawHint(){if(!WantsGui())return;if(_style==null)_style=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true};var r=new Rect(20,Screen.height-150,Screen.width-40,130);var old=GUI.color;GUI.color=Color.black;GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),_hint+"\n"+_aimHint,_style);GUI.color=Color.yellow;GUI.Label(r,_hint+"\n"+_aimHint,_style);GUI.color=old;}
        internal void DrawPage(ToolLayout l)
        {
            l.Toggle(_enabled,"Weapon pose editor on");
            if(_api==null){l.Text("Enable Presentation to author weapon and arm poses.");return;}
            var d=_api.Capture();l.Value("Weapon",d.Weapon??"none");l.Value("Clip",d.Clip??"none");l.Value("Mode",_rotate?"ROTATING":"MOVING");l.Value("Target",Target);l.Value("Tables",d.UserCount+" own / "+d.BuiltinCount+" built-in / "+d.AimCount+" aim entries");
            if(d.Shown!=null)l.Value("Pose (cm, degrees)",string.Join(", ",Array.ConvertAll(d.Shown,v=>v.ToString("0.0",CultureInfo.InvariantCulture))));
            int action=l.Buttons("Move / rotate","Previous clip","Next clip","Back to playing");if(action==0)_rotate=!_rotate;else if(action==1)_api.StepClip(-1);else if(action==2)_api.StepClip(1);else if(action==3)_api.ResetSelection();
            l.Text("Numpad directions move or rotate. Page Up/Down lift hands; Home/End tilt head; Insert/Delete adjust forward offset. Arm motion saves on release.");
            if(_binding>=0&&Event.current.type==EventType.KeyDown&&Event.current.keyCode!=KeyCode.None){if(Event.current.keyCode!=KeyCode.Escape)_keys[_binding].Value=Event.current.keyCode==KeyCode.Backspace?"None":Event.current.keyCode.ToString();_binding=-1;Event.current.Use();}
            for(int i=0;i<_keys.Length;i++)if(l.Button(Labels[i]+": "+(_binding==i?"press a key (Backspace clears)":_keys[i].Value)))_binding=_binding==i?-1:i;
            ArmPage(l,true,d);ArmPage(l,false,d);
            action=l.Buttons("Copy pose","Paste pose","Delete clip pose");if(action==0)_status=Copy()?"Copied weapon, arms and head.":"No live pose to copy.";else if(action==1)_status=Paste()?"Pasted and saved.":"No applicable copied pose.";else if(action==2){_api.DeleteWeapon();_api.Save();}
            action=l.Buttons("Save now","Reload files");if(action==0)_api.Save();else if(action==1)_api.Reload();
            l.Value("Clipboard",_weaponCopy!=null?"Weapon, arms and head":"Empty");l.Value("File",d.FilePath??"");if(_status.Length>0)l.Text(_status);
        }
        private void ArmPage(ToolLayout l,bool left,CharacterPoseDraft draft)
        {
            l.Heading(left?"Left arm":"Right arm");bool old=GUI.enabled;GUI.enabled=old&&draft.CanEditArms;
            if(draft.CanEditArms){var v=_api.GetAim(draft.AimWeapon,draft.AimClip,draft.Crouched);int offset=left?3:8;l.Value("Up / right / pitch / yaw / roll",string.Join(" / ",Array.ConvertAll(new[]{v[offset],v[offset+1],v[offset+2],v[offset+3],v[offset+4]},n=>n.ToString("0.0"))));}
            int b=l.Buttons("Raise 1 cm","Lower 1 cm","Left 1 cm","Right 1 cm");if(b>=0)_api.NudgeArm(left,b<2?0:1,b==0||b==3?1:-1);
            b=l.Buttons("Pitch -1","Pitch +1");if(b>=0)_api.NudgeArm(left,2,b==0?-1:1);b=l.Buttons("Yaw -1","Yaw +1");if(b>=0)_api.NudgeArm(left,3,b==0?-1:1);b=l.Buttons("Roll -1","Roll +1");if(b>=0)_api.NudgeArm(left,4,b==0?-1:1);if(l.Button("Reset this arm"))_api.ResetArm(left);GUI.enabled=old;
        }
    }
}
