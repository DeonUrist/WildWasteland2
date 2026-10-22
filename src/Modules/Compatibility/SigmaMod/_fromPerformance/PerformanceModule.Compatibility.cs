namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// Compatibility part of the Performance entry (Wave A split): installs the SigmaMod FSM guard first in Init, as before.
    /// B17 takes this over (install only when SigmaMod is loaded).
    /// </summary>
    public sealed partial class PerformanceModule
    {
        /// <summary>Installs <see cref="SigmaFsmStates"/> (postfix on PlayMakerFSM.FsmStates) at the start of Init.</summary>
        partial void InitCompatibility() { SigmaFsmStates.Install(this); }
    }
}
