using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.Patrols;
using WarRig = WildWasteland.Modules.Patrols.WarRig;             // refs-ok: Vehicles-owned namespace until the Wave C rename
using Motorcycles = WildWasteland.Modules.Patrols.Motorcycles;   // refs-ok: Vehicles-owned namespace until the Wave C rename

namespace WildWasteland.Modules.Vehicles
{
    /// <summary>
    /// IVehicleFactory provider (README 6.6, P4): builder registry per vehicle kind, template-name to kind resolution, prepared-instance
    /// queries, and the record of factory-built vehicles. Built-in kinds: "WarRig" (War Rig tanker) and "Motorcycle" (motorcycle chassis).
    /// A template name that is not a registered kind goes to the "Template" kind (the template-car builder Patrols registers on top of the
    /// Vehicles assembly + build cache, because a template build also rolls crew, cargo and fluids).
    /// </summary>
    internal sealed class VehicleFactory : IVehicleFactory
    {
        /// <summary>Kind that builds named templates (registered by the template owner).</summary>
        internal const string TemplateKind = "Template";

        /// <summary>One registered builder and its owner module.</summary>
        private sealed class Entry { internal IModule Owner; internal IVehicleBuilder Builder; }

        private readonly Dictionary<string, Entry> _kinds = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _built = new Dictionary<int, string>();
        private readonly Dictionary<int, GameObject> _builtObjects = new Dictionary<int, GameObject>();
        private readonly List<int> _dead = new List<int>();
        private float _nextSweep;

        /// <summary>Registers the War Rig and motorcycle builders of the Vehicles module.</summary>
        internal void RegisterBuiltIns(IModule owner)
        {
            Register(owner, WarRig.Factory.Name, new WarRigBuilder());
            Register(owner, Motorcycles.Factory.Name, new MotorcycleBuilder());
        }

        /// <summary>Registers (or replaces) the builder of a kind.</summary>
        public void Register(IModule owner, string kind, IVehicleBuilder builder)
        {
            if (string.IsNullOrEmpty(kind) || builder == null) return;
            _kinds[kind.Trim()] = new Entry { Owner = owner, Builder = builder };
            if (Log.L1) Log.Main(VehiclesModule.Instance, "Vehicle factory: kind " + kind + " registered by " + (owner != null ? owner.Id : "?"));
        }

        /// <summary>Removes a kind.</summary>
        public void Unregister(string kind) { if (!string.IsNullOrEmpty(kind)) _kinds.Remove(kind.Trim()); }

        /// <summary>Removes every kind registered by a module (its shutdown).</summary>
        internal void UnregisterAll(IModule owner)
        {
            var gone = new List<string>();
            foreach (var pair in _kinds) if (pair.Value.Owner == owner) gone.Add(pair.Key);
            foreach (var kind in gone) _kinds.Remove(kind);
        }

        /// <summary>True when a builder exists for the kind.</summary>
        public bool CanBuild(string kind) { return !string.IsNullOrEmpty(kind) && _kinds.ContainsKey(kind.Trim()); }

        /// <summary>The builder that builds a template name: the kind of the same name, else the template-car kind (null when none).</summary>
        private IVehicleBuilder BuilderFor(string template, out string kind)
        {
            kind = null;
            if (string.IsNullOrEmpty(template)) return null;
            Entry e;
            if (_kinds.TryGetValue(template.Trim(), out e)) { kind = template.Trim(); return e.Builder; }
            if (_kinds.TryGetValue(TemplateKind, out e)) { kind = TemplateKind; return e.Builder; }
            return null;
        }

        /// <summary>Builds a vehicle from a template name at a scene pose, records it and publishes VehicleSpawned; null on failure.</summary>
        public GameObject Build(string template, Vector3 scenePosition, Quaternion rotation, VehicleBuildOptions options)
        {
            if (!VehiclesModule.Running) return null;
            string kind;
            var builder = BuilderFor(template, out kind);
            if (builder == null) { Log.Warn(VehiclesModule.Instance, "Vehicle factory: no builder for " + template); return null; }
            GameObject go;
            try { go = builder.Build(template, scenePosition, rotation, options); }
            catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Vehicle factory: " + template + ": " + e.Message); return null; }
            if (go == null) return null;
            Record(go, template);
            Spawned(go, kind, template, options);
            return go;
        }

        /// <summary>Records a vehicle as factory-built with its template (IsFactoryVehicle / TemplateOf / ownership).</summary>
        internal void Record(GameObject go, string template)
        {
            int id = go.GetInstanceID();
            _built[id] = template; _builtObjects[id] = go;
        }

        /// <summary>Publishes VehicleSpawned for a factory build (only when somebody listens).</summary>
        internal static void Spawned(GameObject go, string kind, string template, VehicleBuildOptions options)
        {
            if (!ModEvents.HasListeners<VehicleSpawned>()) return;
            var ev = new VehicleSpawned
            {
                Vehicle = Entities.Of(go), Kind = kind, Template = template, WorldPos = Origin.World(go.transform.position), Rotation = go.transform.rotation,
                Trailer = EntityId.None, CargoKey = options.CargoKey, PaintKey = options.Faction, JourneyId = -1
            };
            ModEvents.Publish(ref ev);
        }

        /// <summary>Queues preparation of a template (warm request to its builder).</summary>
        public void Prepare(string template)
        {
            string kind; var builder = BuilderFor(template, out kind);
            if (builder == null) return;
            try { builder.Prepare(template); } catch (Exception e) { Log.Warn(VehiclesModule.Instance, "Vehicle factory: prepare " + template + ": " + e.Message); }
        }

        /// <summary>True when a prepared instance of the template is ready (asks its builder).</summary>
        public bool Prepared(string template)
        {
            string kind; var builder = BuilderFor(template, out kind);
            if (builder == null) return false;
            try { return builder.Prepare(template); } catch (Exception) { return false; }
        }

        /// <summary>True for vehicles built by the factory (also War Rigs and motorcycle chassis loaded from a save).</summary>
        public bool IsFactoryVehicle(GameObject vehicle)
        {
            if (vehicle == null) return false;
            GameObject known;
            return _builtObjects.TryGetValue(vehicle.GetInstanceID(), out known) && known == vehicle
                || vehicle.GetComponent<WarRig.WarRigUnit>() != null || vehicle.GetComponent<Motorcycles.MotorcycleBalance>() != null;
        }

        /// <summary>Template a factory vehicle was built from (War Rig / motorcycle by component; null otherwise).</summary>
        public string TemplateOf(GameObject vehicle)
        {
            if (vehicle == null) return null;
            string t; GameObject known;
            if (_built.TryGetValue(vehicle.GetInstanceID(), out t) && _builtObjects.TryGetValue(vehicle.GetInstanceID(), out known) && known == vehicle) return t;
            if (vehicle.GetComponent<WarRig.WarRigUnit>() != null) return WarRig.Factory.Name;
            if (vehicle.GetComponent<Motorcycles.MotorcycleBalance>() != null) return Motorcycles.Factory.Name;
            return null;
        }

        /// <summary>Interval job "Vehicles.Factory warm" (0.5 s): builds a requested War Rig template in play and sweeps destroyed records every 30 s.</summary>
        internal void Warm()
        {
            if (Time.unscaledTime >= _nextSweep) { _nextSweep = Time.unscaledTime + 30f; Sweep(); }
            WarRigBuilder.WarmPending();
        }

        /// <summary>Drops the records of destroyed vehicles.</summary>
        private void Sweep()
        {
            _dead.Clear();
            foreach (var pair in _builtObjects) if (pair.Value == null) _dead.Add(pair.Key);
            foreach (int id in _dead) { _built.Remove(id); _builtObjects.Remove(id); }
            _dead.Clear();
        }

        /// <summary>Scene change: every recorded vehicle is gone.</summary>
        internal void ResetForScene() { _built.Clear(); _builtObjects.Clear(); WarRigBuilder.WarmRequested = false; }

        /// <summary>Builder of the "WarRig" kind: a registered (saved) War Rig at the pose; Prepare builds the inactive template in play.</summary>
        private sealed class WarRigBuilder : IVehicleBuilder
        {
            /// <summary>A Prepare call asked for the template; the warm job builds it in play.</summary>
            internal static bool WarmRequested;

            /// <summary>Builds and registers a War Rig (wheels fitted when the options ask for a trailer-ready rig: not used, bare like native trailers).</summary>
            public GameObject Build(string template, Vector3 scenePosition, Quaternion rotation, VehicleBuildOptions options)
            {
                return WarRig.WarRigFeature.SpawnAt(scenePosition, rotation, WarRig.S.EquipWheels);
            }

            /// <summary>True when the template exists; otherwise requests it for the warm job.</summary>
            public bool Prepare(string template)
            {
                if (WarRig.Factory.Cached != null) return true;
                WarmRequested = true; return false;
            }

            /// <summary>Builds the requested template once the world is in play (one heavy frame, as the first spawn always was).</summary>
            internal static void WarmPending()
            {
                if (!WarmRequested || !GameRefs.InGame) return;
                WarmRequested = false;
                try { WarRig.Factory.Template(); } catch (Exception e) { Log.Warn(VehiclesModule.Instance, "War Rig template: " + e.Message); }
            }
        }

        /// <summary>Builder of the "Motorcycle" kind: a fully equipped motorcycle chassis, numbered and added to the game's car list.</summary>
        private sealed class MotorcycleBuilder : IVehicleBuilder
        {
            /// <summary>Builds the motorcycle with the save-list numbering of native spawns; null when the game's registry is not ready.</summary>
            public GameObject Build(string template, Vector3 scenePosition, Quaternion rotation, VehicleBuildOptions options)
            {
                if (!ItemSpawner.Available(ItemSpawner.SaveList.Cars)) return null;
                var bike = Motorcycles.Factory.Create(scenePosition, rotation, true, NextId);
                if (bike != null && options.Saved) { var list = ItemSpawner.List(ItemSpawner.SaveList.Cars); if (list != null) list.arrayList.Add(bike); }
                return bike;
            }

            /// <summary>Next native item number (the game's itemNameID counter).</summary>
            private static int NextId() { return ItemSpawner.NextId(); }

            /// <summary>The chassis template is built on first use; ready once the Crossbreed prefab is loaded.</summary>
            public bool Prepare(string template)
            {
                try { return Motorcycles.Factory.Template() != null; } catch (Exception) { return false; }
            }
        }
    }
}
