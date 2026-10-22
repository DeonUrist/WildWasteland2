// Debug contracts (README 3.1, 5): log level policy and performance tracing control. Provider: Debug. Consumers: any module that
// wants to show or change the level from its own page. Core itself reads Log.Level/Trace.On directly. FROZEN after W0.
namespace WildWasteland.Core
{
    /// <summary>Log level policy owned by the Debug module ([Debug] VerboseLevel).</summary>
    public interface ILogPolicy
    {
        /// <summary>Configured level 0/1/2 (writes the config entry).</summary>
        int Level { get; set; }
        /// <summary>True when FSM "could not find" warnings are hidden (QuietFsm, level &lt; 2).</summary>
        bool QuietFsmWarnings { get; }
    }

    /// <summary>Performance tracing control (hook/event/job timing, captures).</summary>
    public interface ITraceControl
    {
        /// <summary>Per-handler timing of hooks/events/scopes (sets Trace.On).</summary>
        bool TraceHandlers { get; set; }
        /// <summary>A capture is running.</summary>
        bool Capturing { get; }
        /// <summary>Starts an A/B capture of <paramref name="seconds"/> with a label; false when one is running.</summary>
        bool StartCapture(string label, float seconds);
        /// <summary>Clears all counters (jobs, hooks, events, scopes, frame ring).</summary>
        void ResetCounters();
    }
}
