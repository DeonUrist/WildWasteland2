using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Shared vehicle-owner registry; actual controllers register typed queries so optimization never guesses namespaces.</summary>
    internal sealed class VehicleOwnership : IVehicleOwnership
    {
        private static readonly VehicleOwnership Instance = new VehicleOwnership();
        private readonly List<Entry> _queries = new List<Entry>();
        /// <summary>One query and the module responsible for its lifetime.</summary>
        private sealed class Entry { internal IModule Owner; internal IVehicleOwnerQuery Query; }
        /// <summary>Publishes Core's registry before module startup so providers can register during Init.</summary>
        internal static void Install() { ModApi.Provide<IVehicleOwnership>(null, Instance); }
        /// <summary>Adds a query once; multiple independent queries may belong to the same module.</summary>
        public void Register(IModule owner, IVehicleOwnerQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            for (int i = 0; i < _queries.Count; i++) if (_queries[i].Owner == owner && ReferenceEquals(_queries[i].Query, query)) return;
            _queries.Add(new Entry { Owner = owner, Query = query });
        }
        /// <summary>Asks active controllers in registration order; no query allocations occur during simulation.</summary>
        public bool IsOwned(GameObject vehicle, out IModule owner)
        {
            owner = null;
            if (vehicle == null) return false;
            for (int i = 0; i < _queries.Count; i++)
            {
                var entry = _queries[i];
                try { if (entry.Query.Owns(vehicle)) { owner = entry.Owner; return true; } }
                catch (Exception error) { ModuleHost.ReportHookException(entry.Owner, "vehicle ownership", error); }
            }
            return false;
        }
        /// <summary>Removes registrations when their owning module shuts down or fails startup.</summary>
        internal static void RemoveAll(IModule owner) { Instance._queries.RemoveAll(entry => entry.Owner == owner); }
    }
}
