using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Patrols entry part owned by Tools: patrol editor (TemplateMenu) and template recorder (TemplateExporter) components, jobs and hotkeys (Wave A split of PatrolsModule.cs).</summary>
    public sealed partial class PatrolsModule
    {
        TemplateMenu _menu; TemplateExporter _exporter;

        /// <summary>The patrol editor component on the module runner object (null while the module is off).</summary>
        internal TemplateMenu Menu { get { return _menu; } }

        /// <summary>Adds the patrol editor and template recorder components to the runner object (after Patrol, old component order).</summary>
        partial void AddToolsComponents()
        {
            _menu = _host.AddComponent<TemplateMenu>();
            _exporter = _host.AddComponent<TemplateExporter>();
        }

        /// <summary>Drops the editor/recorder component references on Shutdown (the runner object owning them is destroyed).</summary>
        partial void ShutdownTools() { _menu = null; _exporter = null; }

        /// <summary>Registers the editor/recorder Update jobs right after Patrol.Tick (old job order).</summary>
        partial void RegisterToolsTicks()
        {
            Scheduler.EveryFrame(this, "TemplateMenu.Tick", () => { if (_menu != null) _menu.Tick(); });
            Scheduler.EveryFrame(this, "TemplateExporter.Tick", () => { if (_exporter != null) _exporter.Tick(); });
        }

        /// <summary>Registers the editor/recorder LateUpdate jobs.</summary>
        partial void RegisterToolsLateTicks()
        {
            Scheduler.EveryFrame(this, "TemplateMenu.LateTick", () => { if (_menu != null) _menu.LateTick(); }, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "TemplateExporter.LateTick", () => { if (_exporter != null) _exporter.LateTick(); }, Phase.LateUpdate);
        }

        /// <summary>Registers the editor/recorder Gui jobs after Patrol.Gui (old order).</summary>
        partial void RegisterToolsGui()
        {
            Scheduler.Gui(this, "TemplateMenu.Gui", () => { if (_menu != null) _menu.Gui(); }, () => _menu != null && _menu.IsOpen);
            Scheduler.Gui(this, "TemplateExporter.Gui", () => { if (_exporter != null) _exporter.Gui(); }, () => _exporter != null && _exporter.GuiWanted());
        }

        /// <summary>Registers the patrol editor and template recorder hotkeys.</summary>
        partial void RegisterToolsHotkeys()
        {
            Hotkeys.Register(this, MenuKey, () => OpenEditor(), () => GameRefs.InGame);
            Hotkeys.Register(this, TemplateDumpKey, () => OpenRecorder(), () => GameRefs.InGame);
        }

        /// <summary>Opens the patrol editor (hotkey, F7 page); false when the module runner is gone or the editor refused.</summary>
        internal bool OpenEditor() { return _menu != null && _menu.TryOpen(); }
        /// <summary>Opens the template recorder for the vehicle the player sits in; false when not possible.</summary>
        internal bool OpenRecorder() { return _exporter != null && _exporter.TryOpen(); }
    }
}
