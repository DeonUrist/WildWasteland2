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
        public override string Id { get { return "Developer"; } }
        public override string DisplayName { get { return "Developer"; } }
        public override void BindConfig(ModuleConfig cfg){_character=new CharacterPoseEditor(this,cfg);_wheel=new WheelGripEditorUI(this);BindPatrolEditors(cfg);}
        public override void Init(){_character.BindProvider();_wheel.BindProvider();Scheduler.EveryFrame(this,"Pose editor provider state",TickPoses);Scheduler.Gui(this,"Weapon pose hints",_character.DrawHint,_character.WantsGui);Scheduler.Gui(this,"Wheel grip hints",_wheel.DrawHint,_wheel.WantsGui);InitPatrolEditors();}
        private void TickPoses(){_character.Tick();_wheel.Tick();}
        public override void Shutdown(){_character.Stop();_wheel.Stop();ShutdownPatrolEditors();}
        public override void OnLoadBegin(string slot){_character.Stop();_wheel.Stop();ScenePatrolEditors();}
        public override void OnSceneLoaded(Scene scene,LoadSceneMode mode){_character.Stop();_wheel.Stop();ScenePatrolEditors();}
        public override IEnumerable<ToolPage> ToolPages(){yield return new ActionPage(this,ToolGroups.Player,"Weapon pose editor",_character.DrawPage){IsTool=true};yield return new ActionPage(this,ToolGroups.Player,"Steering-wheel hands",_wheel.DrawPage){IsTool=true};foreach(var page in PatrolEditorPages())yield return page;}
        partial void BindPatrolEditors(ModuleConfig cfg);
        partial void InitPatrolEditors();
        partial void ShutdownPatrolEditors();
        partial void ScenePatrolEditors();
    }
}
