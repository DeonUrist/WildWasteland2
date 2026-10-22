using UnityEngine;
using WildWasteland.Core.Events;
using NwhOrigin = NWH.Common.FloatingOrigin.FloatingOrigin;

namespace WildWasteland.Core
{
    /// <summary>
    /// Floating-origin tracking (AGENTS.md 5.5). The game (NWH FloatingOrigin) shifts the whole world by ~1000 m; after a
    /// jump every cached world position is stale. Core raises <see cref="IModule.OnOriginShift"/> with
    /// delta = oldTotalOffset - newTotalOffset (add it to cached scene positions). Long-lived data should instead be stored
    /// relative to the MapMagic root (<see cref="World"/>/<see cref="Scene"/>), which the game saves and moves with every jump.
    /// </summary>
    public static class Origin
    {
        static Vector3 _before;
        static Transform _anchor;
        static float _nextFind;

        /// <summary>MapMagic root position (scene space); Vector3.zero until found (1 s retry while missing).</summary>
        public static Vector3 Anchor
        {
            get
            {
                if (_anchor == null && Time.unscaledTime >= _nextFind && GameRefs.WorldActive)
                {
                    _nextFind = Time.unscaledTime + 1f;
                    var map = GameObject.Find("MapMagic");
                    if (map != null) _anchor = map.transform;
                }
                return _anchor != null ? _anchor.position : Vector3.zero;
            }
        }
        /// <summary>MapMagic root transform (looked up like <see cref="Anchor"/>), or null.</summary>
        public static Transform AnchorTransform { get { var a = Anchor; return _anchor; } }
        /// <summary>Scene position -> stable world position.</summary>
        public static Vector3 World(Vector3 scene) { return scene - Anchor; }
        /// <summary>Stable world position -> current scene position.</summary>
        public static Vector3 Scene(Vector3 world) { return world + Anchor; }

        /// <summary>Forgets the anchor on scene load (looked up again on next use).</summary>
        internal static void Reset() { _anchor = null; _nextFind = 0f; }

        /// <summary>FloatingOrigin.Jump prefix: remembers the offset before the jump.</summary>
        internal static void BeforeJump(NwhOrigin o) { _before = o.TotalOffset; }

        /// <summary>FloatingOrigin.Jump postfix: raises IModule.OnOriginShift and publishes <see cref="OriginShifted"/> when the offset changed.</summary>
        internal static void AfterJump(NwhOrigin o)
        {
            var delta = _before - o.TotalOffset;
            if (delta == Vector3.zero) return;
            ModuleHost.OriginShift(delta);
            if (ModEvents.HasListeners<OriginShifted>())
            {
                var e = new OriginShifted { Delta = delta, TotalOffset = o.TotalOffset };
                ModEvents.Publish(ref e);
            }
        }
    }
}
