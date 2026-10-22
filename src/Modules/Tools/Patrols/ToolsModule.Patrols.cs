using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Tools
{
    /// <summary>Owns the patrol ledger and vehicle recorder without depending on a patrol runner object.</summary>
    public sealed partial class ToolsModule
    {
        internal static ConfigEntry<bool> AllowDebugSpawns;
        private ConfigEntry<Key> _patrolKey, _recorderKey;
        private GameObject _patrolHost;
        private TemplateMenu _patrolMenu;
        private TemplateExporter _recorder;
        /// <summary>Binds Tools-owned ledger/recorder hotkeys and explicit spawn visibility.</summary>
        private void BindPatrols(ModuleConfig cfg) { _patrolKey=cfg.Hotkey("Patrol editor",Key.None,"Open the patrol, convoy, merchant and cargo ledger.");_recorderKey=cfg.Hotkey("Template recorder",Key.None,"Record the supported vehicle you currently sit in.");AllowDebugSpawns=cfg.Tool("Patrols","AllowDebugSpawns",true,"Show explicit editor spawn actions."); }
        /// <summary>Creates the independent authoring host and registers its jobs and hotkeys.</summary>
        private void InitPatrols()
        {
            ModApi.Changed += PatrolProviderChanged;
            _patrolHost=new GameObject("WW Tools Patrol authoring");_patrolHost.transform.SetParent(Runner.Host.transform,false);
            _patrolMenu=_patrolHost.AddComponent<TemplateMenu>();_recorder=_patrolHost.AddComponent<TemplateExporter>();
            Scheduler.EveryFrame(this,"Patrol authoring input",TickPatrolEditors);
            Scheduler.EveryFrame(this,"Patrol authoring sessions",MaintainPatrolEditors,Phase.LateUpdate);
            Scheduler.Gui(this,"Patrol authoring ledger",_patrolMenu.Gui,()=>_patrolMenu!=null&&_patrolMenu.IsOpen);
            Scheduler.Gui(this,"Vehicle recorder",_recorder.Gui,()=>_recorder!=null&&_recorder.GuiWanted());
            Hotkeys.Register(this,_patrolKey,OpenPatrolEditor,()=>GameRefs.InGame);Hotkeys.Register(this,_recorderKey,OpenRecorder,()=>GameRefs.InGame);
        }
        /// <summary>Updates ledger and recorder input through their Tools-owned host.</summary>
        private void TickPatrolEditors(){if(_patrolMenu!=null)_patrolMenu.Tick();if(_recorder!=null)_recorder.Tick();}
        /// <summary>Maintains both authoring sessions after gameplay updates.</summary>
        private void MaintainPatrolEditors(){if(_patrolMenu!=null)_patrolMenu.LateTick();if(_recorder!=null)_recorder.LateTick();}
        /// <summary>Attempts to acquire and open the patrol ledger.</summary>
        private void OpenPatrolEditor(){if(_patrolMenu!=null)_patrolMenu.TryOpen();}
        /// <summary>Attempts to acquire the supported seated-vehicle recorder.</summary>
        private void OpenRecorder(){if(_recorder!=null)_recorder.TryOpen();}
        /// <summary>Closes authoring sessions when their actual gameplay providers change.</summary>
        private void PatrolProviderChanged(Type contract) { if(contract==typeof(IPatrolAuthoring)||contract==typeof(IVehicleAuthoring)||contract==typeof(ICargoLoot)) { ClosePatrolEditors(); PatrolEditor.Data=null; PatrolEditor.Park.Clear(); } }
        /// <summary>Releases both editor sessions and discards detached workspace state.</summary>
        private void ClosePatrolEditors(){if(_patrolMenu!=null)_patrolMenu.Close();if(_recorder!=null)_recorder.Close();}
        /// <summary>Releases both editor sessions and discards detached workspace state.</summary>
        private void ShutdownPatrols(){ModApi.Changed -= PatrolProviderChanged;ClosePatrolEditors();if(_patrolHost!=null)UnityEngine.Object.Destroy(_patrolHost);_patrolHost=null;_patrolMenu=null;_recorder=null;}
        /// <summary>Draws ledger availability, runtime catalog status and explicit open/reload actions.</summary>
        private void DrawPatrolEditor(ToolLayout l){l.Text("Edit patrol, convoy and merchant rosters, factions, crew, cargo and template metadata. The editor pauses the world and writes validated authoring files.");l.Toggle(AllowDebugSpawns,"Show spawn controls");var api=PatrolEditor.Source;if(api==null){l.Text("Enable Patrols to author its groups and templates.");return;}l.Value("Templates",api.Summary);if(l.Button("Open patrol editor",GameRefs.InPlay))ToolWindow.CloseThen(OpenPatrolEditor);if(l.Button("Reload templates from disk"))PatrolEditor.Refresh(true);}
        /// <summary>Draws supported seated recording availability and its current output directory.</summary>
        private void DrawRecorder(ToolLayout l){l.Text("Sit in a supported assembled vehicle to record its frame, attached parts, adjusted hinges, trailer, suspension and crew choices.");if(l.Button("Record seated vehicle",PatrolEditor.Source!=null&&TemplateExporter.Seated))ToolWindow.CloseThen(OpenRecorder);l.Entry(_recorderKey);l.Value("Folder",PatrolEditor.Source?.PlayerDirectory??"Patrols unavailable");}
    }
}
