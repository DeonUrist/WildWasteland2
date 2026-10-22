using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;
namespace WildWasteland.Modules.Developer
{
    /// <summary>Developer owns placement tool input, configuration, temporary markers and UI lifecycle.</summary>
    internal sealed partial class PatrolEditorRuntime
    {
        internal static PatrolEditorRuntime Instance;
        private readonly DeveloperModule _owner;
        private GameObject _host;
        private TurretSpotter _spotter;private BusPassengerPoseEditor _busPose;private MerchantEditor _merchantEditor;
        /// <summary>Returns the current turret placement component.</summary>
        internal TurretSpotter Spotter {get{return _spotter;}}
        /// <summary>Returns the current bus-pose placement component.</summary>
        internal BusPassengerPoseEditor BusPose {get{return _busPose;}}
        /// <summary>Returns the current merchant placement component.</summary>
        internal MerchantEditor MerchantSpot {get{return _merchantEditor;}}
        /// <summary>Binds current Developer configuration without creating native scene objects.</summary>
        internal PatrolEditorRuntime(DeveloperModule owner,ModuleConfig cfg) {_owner=owner;BindDeveloperKeys(cfg);BindDeveloperDebug(cfg);}
        /// <summary>Starts owner-scoped tool jobs and native-hotkey registrations.</summary>
        internal void Start()
        {
            Instance=this;Create();
            Scheduler.EveryFrame(_owner,"Developer.Turret placement",()=>{if(_spotter!=null)_spotter.Tick();});
            Scheduler.EveryFrame(_owner,"Developer.Bus pose placement",()=>{if(_busPose!=null)_busPose.Tick();});
            Scheduler.EveryFrame(_owner,"Developer.Merchant placement",()=>{if(_merchantEditor!=null)_merchantEditor.Tick();});
            Scheduler.Gui(_owner,"Developer.Turret placement GUI",()=>{if(_spotter!=null)_spotter.Gui();},()=>_spotter!=null&&_spotter.GuiWanted());
            Scheduler.Gui(_owner,"Developer.Bus pose placement GUI",()=>{if(_busPose!=null)_busPose.Gui();},()=>_busPose!=null&&BusPassengerPoseEditor.Active);
            Scheduler.Gui(_owner,"Developer.Merchant placement GUI",()=>{if(_merchantEditor!=null)_merchantEditor.Gui();},()=>_merchantEditor!=null&&MerchantEditor.Enabled);
            Hotkeys.Register(_owner,BusPassengerPoseEditorKey,()=>BusPassengerPoseEditorEnabled.Value=!BusPassengerPoseEditorEnabled.Value);
            Hotkeys.Register(_owner,MerchantEditorKey,()=>MerchantEditorEnabled.Value=!MerchantEditorEnabled.Value);
        }
        /// <summary>Creates temporary tool components under the current hidden owner host.</summary>
        private void Create() {_host=new GameObject("WW.Developer.Placement"){hideFlags=HideFlags.HideAndDontSave};_host.transform.SetParent(Runner.Host.transform,false);_spotter=_host.AddComponent<TurretSpotter>();_busPose=_host.AddComponent<BusPassengerPoseEditor>();_merchantEditor=_host.AddComponent<MerchantEditor>();}
        /// <summary>Releases scene targets and recreates temporary tool state at a scene/load boundary.</summary>
        internal void Scene() {Destroy();if(Instance==this)Create();}
        /// <summary>Releases actual visual edit ownership and all temporary tool objects.</summary>
        private void Destroy() {ModApi.Get<IBusPassengerAuthoring>()?.Select(null);BusPassengerPoseEditor.Target=null;if(_host!=null)UnityEngine.Object.Destroy(_host);_host=null;_spotter=null;_busPose=null;_merchantEditor=null;}
        /// <summary>Stops tool state before Core removes its jobs and hotkeys.</summary>
        internal void Stop() {Instance=null;Destroy();}
        /// <summary>Returns pages contributed by the actual Developer owner.</summary>
        internal IEnumerable<ToolPage> Pages() {return new ToolPage[]{new ActionPage(_owner,ToolGroups.Poses,"Turret spot editor",TurretSpotPage){IsTool=true},new ActionPage(_owner,ToolGroups.Poses,"Bus passenger pose editor",BusPosePage){IsTool=true},new ActionPage(_owner,ToolGroups.Merchants,"Merchant item placement",MerchantSpotPage){IsTool=true}};}
        /// <summary>Formats the current bound toggle key.</summary>
        private static string KeyName(ConfigEntry<Key> key) {return key==null||key.Value==Key.None?"not bound":key.Value.ToString();}
    }
}
