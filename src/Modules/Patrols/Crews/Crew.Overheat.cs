using System.Collections;
using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Traffic decides the hold policy; Vehicles owns reversible native engine changes.</summary>
    internal partial class Crew
    {
        /// <summary>Updates the current native provider after a seat or tuning change.</summary>
        internal void ApplyOverheat() { ModApi.Get<IVehicleEngineHeat>()?.Hold(_car,_locked && !PatrolsModule.RaidersOverheat.Value); }
        /// <summary>Retries once after native parts finish initialising.</summary>
        private IEnumerator OverheatLater() { yield return new WaitForSeconds(2f); ApplyOverheat(); }
        /// <summary>Updates all live traffic actors after tuning or provider replacement.</summary>
        internal static void ApplyOverheatAll() { foreach(var c in FindObjectsOfType<Crew>()) c.ApplyOverheat(); }
        /// <summary>Reapplies held traffic policy when the native engine provider restarts.</summary>
        internal static void HeatApiChanged(System.Type type) { if(type==typeof(IVehicleEngineHeat) && PatrolsModule.Running) ApplyOverheatAll(); }
    }
}
