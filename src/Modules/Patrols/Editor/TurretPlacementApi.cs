using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace WildWasteland.Modules.Patrols
{
    // The turret spot editor ([Debug] TurretSpotEditor, TurretSpotter.cs) and the old Apocaspotter plugin (slot 1 only) use this. Public on purpose.
    internal sealed class TurretPlacementApi : Core.ITurretPlacement
    {
        /// <summary>Returns the number of supported turret slots on the towing vehicle.</summary>
        public int VehicleSlots {get{return PatrolMarker.VehicleTurretSlots;}}
        /// <summary>Returns the complete towing-vehicle and trailer turret slot count.</summary>
        public int TotalSlots {get{return PatrolMarker.TotalTurretSlots;}}
        /// <summary>Reports whether the actual Patrols owner and native gameplay are active.</summary>
        public bool InGame { get { return PatrolsModule.Running && Core.GameRefs.InGame; } }

        // every live raider car that has a turret spot
        /// <summary>Copies actual live native vehicles eligible for this authoring operation.</summary>
        public GameObject[] Cars()
        {
            var list = new List<GameObject>();
            foreach (var m in PatrolMarker.All) if (m != null && m.RiderAnchor != null) list.Add(m.gameObject);
            return list.ToArray();
        }
        /// <summary>Resolves the actual current Patrols marker for the supplied native vehicle.</summary>
        private PatrolMarker M(GameObject car) { return car != null ? car.GetComponent<PatrolMarker>() : null; }
        /// <summary>Resolves the actual native turret anchor for the requested saved slot.</summary>
        public Transform Anchor(GameObject car) { var m = M(car); return m != null ? m.RiderAnchor : null; }
        /// <summary>Resolves the actual native turret anchor for the requested saved slot.</summary>
        public Transform Anchor(GameObject car, int slot) { var m = M(car); return m != null && slot >= 0 && slot < m.RiderAnchors.Length ? m.RiderAnchors[slot] : null; }
        /// <summary>Returns the currently selected turret or merchant slot.</summary>
        public int Slot(GameObject car, Transform anchor) { var m = M(car); return m != null ? Array.IndexOf(m.RiderAnchors, anchor) : -1; }
        /// <summary>Returns the native actor currently assigned to the selected turret slot.</summary>
        public GameObject Rider(GameObject car) { var m = M(car); return m != null ? m.Rider : null; }
        /// <summary>Returns the native actor currently assigned to the selected turret slot.</summary>
        public GameObject Rider(GameObject car, int slot) { var m = M(car); return m != null && slot >= 0 && slot < m.Riders.Length ? m.Riders[slot] : null; }
        /// <summary>Returns the actual rider-held lance without creating a replacement.</summary>
        public Transform Lance(GameObject car) { var r = Rider(car); var g = r != null ? r.GetComponent<RiderGuard>() : null; return g != null ? g.Lance : null; }
        /// <summary>Returns the actual rider-held lance without creating a replacement.</summary>
        public Transform Lance(GameObject car, int slot) { var r = Rider(car, slot); var g = r != null ? r.GetComponent<RiderGuard>() : null; return g != null ? g.Lance : null; }
        /// <summary>Reads the current authored template identity from the actual patrol marker.</summary>
        public string TemplateName(GameObject car) { var m = M(car); return m != null ? m.TemplateName : ""; }

        /// <summary>Writes the accepted frame-local turret anchor into the actual authored template file.</summary>
        public string SaveAnchor(GameObject car) { var a = Anchor(car); return SaveAnchor(car, Math.Max(0, Slot(car, a))); }

        // writes the spot's frame-local position into the car's template file: riderPos / riderPos2 / riderPos3
        /// <summary>Writes the accepted frame-local turret anchor into the actual authored template file.</summary>
        public string SaveAnchor(GameObject car, int slot)
        {
            var a = Anchor(car, slot); string name = TemplateName(car);
            if (a == null || string.IsNullOrEmpty(name)) return "no turret " + (slot + 1) + " spot / template on this car";
            var t = CarTemplate.Find(name);
            if (t == null || string.IsNullOrEmpty(t.SourcePath) || !System.IO.File.Exists(t.SourcePath)) return "template file of " + name + " not found";
            var file = TemplateFile.FromJson(System.IO.File.ReadAllText(t.SourcePath));
            var p = a.localPosition;
            var target = slot < PatrolMarker.VehicleTurretSlots ? file : file.trailer;
            if (target == null) return "template has no trailer";
            target.SetTurretPos(slot < PatrolMarker.VehicleTurretSlots ? slot : slot - PatrolMarker.VehicleTurretSlots, new[] { Round(p.x), Round(p.y), Round(p.z) });
            Core.AuthoringFiles.AtomicWrite(t.SourcePath, file.ToJson());
            CarTemplates.Refresh(true);
            return "turret " + (slot + 1) + " spot " + WildWasteland.Modules.Patrols.Rider.FormatV3(p) + " saved into " + System.IO.Path.GetFileName(t.SourcePath);
        }

        /// <summary>Rounds coordinates to the existing authored template precision.</summary>
        private float Round(float v) { return Mathf.Round(v * 1000f) / 1000f; }
    }
}
