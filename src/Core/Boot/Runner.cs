using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// The single hidden GameObject that hosts every WW loop (AGENTS.md 5.2). Three MonoBehaviours with different
    /// script execution orders give the scheduler its Early / normal / Last phases. Recreated on scene load if the game destroyed it.
    /// </summary>
    public sealed class Runner : MonoBehaviour
    {
        public static Runner Instance { get; private set; }
        public static GameObject Host { get; private set; }

        public static void Ensure()
        {
            if (Instance != null) return;
            Host = new GameObject("WildWasteland.Runner");
            Host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(Host);
            Host.AddComponent<EarlyRunner>();
            Instance = Host.AddComponent<Runner>();
            Host.AddComponent<LateRunner>();
            Gui = Host.AddComponent<GuiRunner>();
        }
        internal static GuiRunner Gui;

        void Update() { Scheduler.Run(Phase.Update); }
        void LateUpdate() { Scheduler.Run(Phase.LateUpdate); }
        void FixedUpdate() { Scheduler.Run(Phase.FixedUpdate); }
        void OnApplicationQuit() { ModuleHost.Quit(); }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }

    [DefaultExecutionOrder(-1000)]
    internal sealed class EarlyRunner : MonoBehaviour
    {
        void Update() { Scheduler.Run(Phase.EarlyUpdate); }
        void FixedUpdate() { Scheduler.Run(Phase.EarlyFixedUpdate); }
    }

    [DefaultExecutionOrder(20000)]
    internal sealed class LateRunner : MonoBehaviour
    {
        void LateUpdate()
        {
            Scheduler.Run(Phase.LateUpdateLast);
            // (WW perf, D18) OnGUI lives on its own component, enabled only while the F7 window needs it or some Gui job wants to draw
            // (jobs registered without a predicate always do): with nothing to draw Unity skips the IMGUI Layout/Repaint/event passes
            var g = Runner.Gui;
            if (!ReferenceEquals(g, null))
            {
                bool on;
                try { on = UI.ToolWindow.NeedsGui || Scheduler.GuiWanted(); } catch (System.Exception) { on = true; }
                if (g.enabled != on && g != null) g.enabled = on;
            }
        }
    }

    /// <summary>The runner's OnGUI (Phase.Gui jobs, then the F7 window). Enabled by <see cref="LateRunner"/>.</summary>
    internal sealed class GuiRunner : MonoBehaviour
    {
        void OnGUI() { Scheduler.Run(Phase.Gui); UI.ToolWindow.Draw(); }
    }
}
