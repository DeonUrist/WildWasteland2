using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Owns actual character pose storage and safely scopes editor callbacks to a running module.</summary>
    internal sealed class PoseEditorApi : ICharacterPoseAuthoring
    {
        static IModule _owner;
        static Func<PoseEditMotion> _weapon;
        static Action<string,string,bool,float,float> _aim;
        internal static bool Enabled;
        /// <summary>Clears callbacks and preview state when Presentation stops, even if Tools remains enabled.</summary>
        internal static void Clear(){Enabled=false;GunPose.ResetSelection();GunPose.CurrentWeapon=GunPose.CurrentPose="";GunPose.CurrentShown=null;GunPose.CurrentFrame=-1;_owner=null;_weapon=null;_aim=null;}
        /// <summary>True only while the registered editor module runs.</summary>
        internal static bool Active { get { return _owner!=null && ModuleHost.IsRunning(_owner); } }
        /// <summary>Calls Tools at the original weapon evaluation point.</summary>
        internal static PoseEditMotion WeaponInput(){ return Active && _weapon!=null ? _weapon() : default(PoseEditMotion); }
        /// <summary>Calls Tools at the original aim-lift evaluation point.</summary>
        internal static void AimInput(string weapon,string clip,bool crouched,float dt,float gap){if(Active && _aim!=null)_aim(weapon,clip,crouched,dt,gap);}
        /// <summary>Captures detached current values only when explicitly requested by the editor.</summary>
        public CharacterPoseDraft Capture(){return GunPose.Draft();}
        /// <summary>Replaces callbacks only for the same owner or after the previous owner stops.</summary>
        public void Register(IModule owner,Func<PoseEditMotion> weaponInput,Action<string,string,bool,float,float> aimInput){if(owner==null)throw new ArgumentNullException(nameof(owner));if(Active && !ReferenceEquals(owner,_owner))throw new InvalidOperationException("Pose editor already owned");_owner=owner;_weapon=weaponInput;_aim=aimInput;}
        /// <summary>Flushes changes and releases only the matching owner's callbacks.</summary>
        public void Remove(IModule owner){if(!ReferenceEquals(owner,_owner))return;try{EndMotion();}finally{Clear();}}
        /// <summary>Controls live authoring while a valid editor owner is registered.</summary>
        public bool Editing {get{return Active && Enabled;}set{if(!value)EndMotion();Enabled=value;}}
        /// <summary>Routes runtime arm/weapon selection through the actual pose runtime.</summary>
        public int Target {get{return GunPose.EditorTarget;}set{GunPose.EditorTarget=Mathf.Clamp(value,0,2);}}
        /// <summary>Cycles the actual playable animation preview.</summary>
        public void StepClip(int delta){GunPose.StepClip(delta);}
        /// <summary>Releases forced preview playback.</summary>
        public void ResetSelection(){GunPose.ResetSelection();}
        /// <summary>Stores a complete current weapon pose without retaining the caller's buffer.</summary>
        public void SetWeapon(float[] values){Validate(values,6);var d=Capture();if(string.IsNullOrEmpty(d.Weapon)||string.IsNullOrEmpty(d.Clip))return;GunPose.PutUser(d.Weapon,d.Clip,values);}
        /// <summary>Removes the current own weapon pose, allowing normal fallback.</summary>
        public void DeleteWeapon(){GunPose.DeleteCurrent();}
        /// <summary>Validates and stores all aim, head and arm dimensions.</summary>
        public void SetAim(string weapon,string clip,bool crouched,float[] values){Validate(values,13);if(string.IsNullOrEmpty(weapon)||string.IsNullOrEmpty(clip))return;AimLift.Set(weapon,clip,crouched,(float[])values.Clone());}
        /// <summary>Returns a complete detached aim/head/arm record.</summary>
        public float[] GetAim(string weapon,string clip,bool crouched){return (float[])AimLift.Get(weapon,clip,crouched).Clone();}
        /// <summary>Applies the existing bounded arm motion algorithm.</summary>
        public void ArmMotion(bool left,Vector3 move,Vector3 rotation,float seconds){AimLift.ArmKeys(left,move,rotation,seconds);}
        /// <summary>Commits the preceding runtime weapon and arm motion.</summary>
        public void EndMotion(){GunPose.Flush();AimLift.EndArmKeys();AimLift.SaveIfDirty();}
        /// <summary>Applies a bounded immediate arm adjustment.</summary>
        public void NudgeArm(bool left,int axis,float delta){if(axis<0||axis>4)throw new ArgumentOutOfRangeException(nameof(axis));AimLift.NudgeArm(left,axis,delta);}
        /// <summary>Resets one arm's offsets using the current eligible animation context.</summary>
        public void ResetArm(bool left){AimLift.ResetArm(left);}
        /// <summary>Saves complete weapon and aim tables.</summary>
        public void Save(){GunPose.SaveNow();AimLift.Save();}
        /// <summary>Drops unsaved edits and reloads actual Presentation files.</summary>
        public void Reload(){GunPose.ReloadFiles();AimLift.Load(PresentationModule.DataDir);}
        /// <summary>Rejects malformed buffers before publishing any live state.</summary>
        internal static void Validate(float[] values,int count){if(values==null||values.Length!=count)throw new ArgumentException("Unexpected pose dimensions");foreach(float v in values)if(float.IsNaN(v)||float.IsInfinity(v))throw new ArgumentException("Nonfinite pose value");}
    }
}
