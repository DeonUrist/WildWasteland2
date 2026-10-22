using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Developer
{
    /// <summary>Owns all developer authoring input and views independently of gameplay module lifecycles.</summary>
    public sealed partial class DeveloperModule : ModuleBase
    {
        private CharacterPoseEditor _character;
        private WheelGripEditorUI _wheel;
        /// <summary>Identifies the independent developer module.</summary>
        public override string Id { get { return "Developer"; } }
        /// <summary>Names the developer section in the module menu.</summary>
        public override string DisplayName { get { return "Developer"; } }
        /// <summary>Binds owned pose and placement editor settings.</summary>
        public override void BindConfig(ModuleConfig cfg){_character=new CharacterPoseEditor(this,cfg);_wheel=new WheelGripEditorUI(this);BindPatrolEditors(cfg);}
        /// <summary>Registers owned pose callbacks, hints and placement editor jobs.</summary>
        public override void Init(){_character.BindProvider();_wheel.BindProvider();Scheduler.EveryFrame(this,"Pose editor provider state",TickPoses);Scheduler.Gui(this,"Weapon pose hints",_character.DrawHint,_character.WantsGui);Scheduler.Gui(this,"Wheel grip hints",_wheel.DrawHint,_wheel.WantsGui);InitPatrolEditors();}
        /// <summary>Reconciles current Presentation providers before editor input.</summary>
        private void TickPoses(){_character.Tick();_wheel.Tick();}
        /// <summary>Releases pose callbacks and placement editor components.</summary>
        public override void Shutdown(){try{_character.Stop();}finally{try{_wheel.Stop();}finally{ShutdownPatrolEditors();}}}
        /// <summary>Ends all live authoring sessions before save loading.</summary>
        public override void OnLoadBegin(string slot){ResetEditors();}
        /// <summary>Discards scene-bound editing targets before providers rebuild.</summary>
        public override void OnSceneLoaded(Scene scene,LoadSceneMode mode){ResetEditors();}
        /// <summary>Releases all scene editing owners even when one backend cannot persist its current draft.</summary>
        private void ResetEditors(){try{_character.Stop();}finally{try{_wheel.Stop();}finally{ScenePatrolEditors();}}}
        /// <summary>Contributes weapon, wheel and placement authoring pages.</summary>
        public override IEnumerable<ToolPage> ToolPages(){yield return new ActionPage(this,ToolGroups.Player,"Weapon pose editor",_character.DrawPage){IsTool=true};yield return new ActionPage(this,ToolGroups.Player,"Steering-wheel hands",_wheel.DrawPage){IsTool=true};foreach(var page in PatrolEditorPages())yield return page;}
        /// <summary>Binds placement settings in the Developer-owned implementation.</summary>
        partial void BindPatrolEditors(ModuleConfig cfg);
        /// <summary>Creates the Developer-owned placement editor host.</summary>
        partial void InitPatrolEditors();
        /// <summary>Disposes placement editor callbacks and host.</summary>
        partial void ShutdownPatrolEditors();
        /// <summary>Clears scene-bound placement editor targets.</summary>
        partial void ScenePatrolEditors();
    }
}
