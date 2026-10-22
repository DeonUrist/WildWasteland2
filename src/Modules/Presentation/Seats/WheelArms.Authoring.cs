using System;
using System.IO;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Actual wheel runtime preview state and storage; the editor owns all input and rendering.</summary>
    internal static partial class WheelArms
    {
        private static bool _editor, _dirty;
        private static int _editPose, _target, _preview=2;
        /// <summary>Ends a preview and saves the current setup if it was edited.</summary>
        private static void StopEditor(){if(_dirty && _setup!=null)Save();_editor=false;_preview=_handlebars?3:2;}
        /// <summary>Runs the registered editor at the original wheel update point.</summary>
        private static void TickEditor(){WheelGripAuthoring.Input();}
        /// <summary>Publishes the real complete setup with atomic file replacement.</summary>
        private static void Save()
        {
            if(_setup==null)return;
            if(!_handlebars)for(int pose=0;pose<2;pose++)MirrorPose(_setup.V,pose,1);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var fields=new string[_setup.V.Length];for(int i=0;i<fields.Length;i++)fields[i]=_setup.V[i].ToString("R",Inv);
            string temp=FilePath+".tmp";File.WriteAllText(temp,string.Join(" ",fields));AtomicFile.Replace(temp,FilePath);_dirty=false;
        }
        /// <summary>Creates a detached snapshot of the actual fitted-wheel and arm-reach state.</summary>
        internal static WheelGripDraft Draft()
        {
            return new WheelGripDraft{Key=_key,Name=_name,Owner=_owner,FilePath=_setup==null?null:FilePath,Available=_setup!=null && _mount!=null && _wheel!=null,Values=_setup==null?null:(float[])_setup.V.Clone(),Handlebars=_handlebars,Editing=_editor,Pose=_editPose,Preview=_preview,TowardDriver=_towardDriver,RimDiameter=_mount==null?0f:200f*_radius.x*Mathf.Abs(_mount.lossyScale.x),Requested=new Vector2(_arms[0]==null?0f:_arms[0].Requested,_arms[1]==null?0f:_arms[1].Requested),Reach=new Vector2(_arms[0]==null?0f:_arms[0].Reach,_arms[1]==null?0f:_arms[1].Reach),LeftLimited=_arms[0]!=null && _arms[0].BendLimited,RightLimited=_arms[1]!=null && _arms[1].BendLimited};
        }
        /// <summary>Changes only preview intent; native steering and IK remain runtime-owned.</summary>
        internal static void AuthorPreview(bool editing,int pose,int preview){if(!editing){StopEditor();return;}if(_setup==null||!Core.PlayerState.InCar||Core.PlayerState.Dead)return;_editor=true;_editPose=Mathf.Clamp(pose,0,_handlebars?2:1);_preview=Mathf.Clamp(preview,0,_handlebars?3:2);}
        /// <summary>Validates the exact current wheel and full buffer before publishing edits and mirrored poses.</summary>
        internal static void AuthorValues(string key,float[] values,int pose,int side){if(_setup==null||key!=_key)throw new InvalidOperationException("Fitted wheel changed");PoseEditorApi.Validate(values,_setup.V.Length);Array.Copy(values,_setup.V,values.Length);if(!_handlebars && pose>=0 && pose<2 && side>=0 && side<2)MirrorPose(_setup.V,pose,side);_dirty=true;}
        /// <summary>Restarts the native stroke interpolation after replacing a complete setup.</summary>
        internal static void AuthorResetMotion(){ResetStroke(_lastSpin);}
        /// <summary>Saves authored wheel data explicitly.</summary>
        internal static void AuthorSave(){Save();}
        /// <summary>Replaces the current setup with its native defaults and saves it.</summary>
        internal static void AuthorReset(){if(_setup==null)return;_setup=Load(true);_setups[_key]=_setup;ResetStroke(_lastSpin);Save();}
    }
}
