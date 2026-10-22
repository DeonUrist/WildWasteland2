using System;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>Scopes wheel editor input to its running Tools owner while retaining actual IK data in Presentation.</summary>
    internal sealed class WheelGripAuthoring : IWheelGripAuthoring
    {
        static IModule _owner;
        static Action _input;
        /// <summary>Releases editor callbacks when Presentation stops; wheel teardown ends and saves preview separately.</summary>
        internal static void Clear(){_owner=null;_input=null;}
        /// <summary>Invokes editor input at the existing runtime update point.</summary>
        internal static void Input(){if(_owner!=null && ModuleHost.IsRunning(_owner) && _input!=null)_input();}
        /// <summary>Reads the complete real fitted-wheel context.</summary>
        public WheelGripDraft Capture(){return WheelArms.Draft();}
        /// <summary>Registers a single editor owner and its input callback.</summary>
        public void Register(IModule owner,Action input){if(owner==null)throw new ArgumentNullException(nameof(owner));if(_owner!=null && ModuleHost.IsRunning(_owner) && !ReferenceEquals(owner,_owner))throw new InvalidOperationException("Wheel editor already owned");_owner=owner;_input=input;}
        /// <summary>Ends preview and releases callbacks belonging to the exact owner.</summary>
        public void Remove(IModule owner){if(!ReferenceEquals(owner,_owner))return;try{WheelArms.AuthorPreview(false,0,0);}finally{Clear();}}
        /// <summary>Sets runtime preview intent with bounds checked by the real wheel owner.</summary>
        public void Preview(bool editing,int pose,int preview){WheelArms.AuthorPreview(editing,pose,preview);}
        /// <summary>Publishes a validated full setup and mirrors the edited wheel side.</summary>
        public void SetValues(string key,float[] values,int editedPose,int editedSide){WheelArms.AuthorValues(key,values,editedPose,editedSide);}
        /// <summary>Restarts runtime stroke interpolation after a complete setup replacement.</summary>
        public void ResetMotion(){WheelArms.AuthorResetMotion();}
        /// <summary>Atomically saves the current authored wheel setup.</summary>
        public void Save(){WheelArms.AuthorSave();}
        /// <summary>Resets and saves the current wheel setup.</summary>
        public void Reset(){WheelArms.AuthorReset();}
    }
}
