using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Compatibility
{
    /// <summary><see cref="IWorldGenFacts"/> provider: publishes Vistas generation facts without exposing the reflective bridge state.</summary>
    internal sealed class VistasFacts : IWorldGenFacts
    {
        /// <summary>True while the detected World Amp bridge is installed.</summary>
        public bool Present { get { return VistasWorldGen.Present; } }
        /// <summary>Last place name reported by the native entry callback.</summary>
        public string CurrentPlace { get { return VistasWorldGen.CurrentPlace; } }
        /// <summary>Native place classification or landmark caption accompanying the entry callback.</summary>
        public string CurrentPlaceKind { get { return VistasWorldGen.CurrentPlaceKind; } }
        /// <summary>Revision changed whenever captured roads are replaced or cleared.</summary>
        public int RoadRevision { get { return VistasWorldGen.RoadRevision; } }
        /// <summary>Third-party assembly identity and terrain/road settings used by route-cache consumers.</summary>
        public string RoadIdentity { get { return VistasWorldGen.RoadIdentity; } }
        /// <summary>Configured banner height, with the native fallback when the setting is unavailable.</summary>
        public float BannerHeight { get { return VistasWorldGen.BannerHeight; } }
        /// <summary>Copies the retained road polylines into the consumer's reusable list.</summary>
        public void RoadLines(List<Vector3[]> into) { into.Clear(); foreach (var line in VistasWorldGen.RoadLines) into.Add(line); }
    }
}
