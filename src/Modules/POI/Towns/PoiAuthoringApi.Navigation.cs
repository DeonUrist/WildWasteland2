using System;
using UnityEngine;
using WildWasteland.Core;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.POI
{
    internal sealed partial class PoiAuthoringApi
    {
        private sealed class NavigationGeometry : IPoiNavigationGeometry
        {
            private readonly PoiDressing _owner;
            private readonly PoiDressLayout _layout;
            private int _next;
            public GameObject Root { get; private set; }
            internal NavigationGeometry(PoiDressing owner, string type, Transform parent)
            {
                if (parent == null || parent.gameObject.activeInHierarchy) throw new InvalidOperationException("Navigation copies require inactive staging.");
                _owner = owner; _layout = owner.StoredLayout(type);
                var custom = owner.Authored.Get(type);
                var prefab = custom == null ? owner.NativeTemplate(type) : custom.Prefab ?? (custom.Prefab = owner.Authored.Shell(custom, true));
                if (prefab == null) throw new InvalidOperationException("POI geometry unavailable: " + type);
                Root = Object.Instantiate(prefab, parent, false); Root.name = type; Root.SetActive(false);
                foreach (var behaviour in Root.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) Object.DestroyImmediate(behaviour);
                foreach (var body in Root.GetComponentsInChildren<Rigidbody>(true)) if (body != null) Object.DestroyImmediate(body);
                owner.HideTemplateScenery(Root.transform, _layout);
            }
            public bool Step()
            {
                if (Root == null) throw new InvalidOperationException("Navigation copy was removed.");
                if (_next < _layout.Props.Count)
                {
                    var prop = _layout.Props[_next++];
                    if (!prop.Marker)
                    {
                        var asset = _owner.Catalog.Resolve(prop);
                        if (asset == null) throw new InvalidOperationException("Missing native mesh: " + prop.Mesh);
                        _owner.Catalog.Create(asset, Root.transform, prop, prop.Collision);
                    }
                }
                return _next >= _layout.Props.Count;
            }
        }
        public IPoiNavigationGeometry CreateNavigationGeometry(string type, Transform parent) { return new NavigationGeometry(_runtime, type, parent); }
    }
}
