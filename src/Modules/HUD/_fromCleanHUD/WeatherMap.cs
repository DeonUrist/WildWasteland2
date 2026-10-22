using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal sealed class WeatherMap
    {
        private RectTransform _map;
        private GameObject _root;
        private WeatherMapGraphic _graphic;
        private float _nextRefresh;

        internal void Tick(QuestMap questMap)
        {
            var weather = Services.Weather;
            Camera camera; RectTransform map;
            bool shown = questMap != null && weather != null && weather.Count > 0 && weather.MapAreas;
            if (!shown || !questMap.TryWeatherView(out camera, out map))
            { if (_root != null && _root.activeSelf) _root.SetActive(false); return; }
            if (_map != map || _root == null)
            {
                Clear(); _map = map;
                var root = DialogueWidgets.Rect(map, "CleanHUD.WeatherAreas", 0, 0, 0, 0);
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
                root.SetSiblingIndex(1); root.gameObject.AddComponent<RectMask2D>(); _root = root.gameObject;
                var surface = DialogueWidgets.Rect(root, "Areas", 0, 0, 0, 0);
                surface.anchorMin = Vector2.zero; surface.anchorMax = Vector2.one; surface.offsetMin = surface.offsetMax = Vector2.zero;
                _graphic = surface.gameObject.AddComponent<WeatherMapGraphic>(); _graphic.raycastTarget = false;
            }
            if (!_root.activeSelf) _root.SetActive(true);
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .2f;
            _graphic.Refresh(camera, weather);
        }

        internal void Clear()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null; _map = null; _graphic = null; _nextRefresh = 0;
        }
    }

    // Rebuilt at most 5 Hz while the map is open; no texture uploads or independent Unity update loop.
    internal sealed class WeatherMapGraphic : MaskableGraphic
    {
        private const int Segments = 64;
        private readonly WeatherRegion[] _regions = new WeatherRegion[4];
        private readonly Vector2[] _points = new Vector2[Segments];
        private Camera _camera;
        private int _count;
        private Matrix4x4 _projection;
        private Vector3 _anchor;

        internal void Refresh(Camera camera, IWeatherRegions weather)
        {
            bool changed = _camera != camera || _projection != camera.projectionMatrix || _anchor != Origin.Anchor;
            _camera = camera; _projection = camera.projectionMatrix; _anchor = Origin.Anchor;
            int count = Mathf.Min(_regions.Length, weather.Count);
            if (_count != count) changed = true;
            _count = count;
            for (int i = 0; i < count; i++)
            {
                WeatherRegion region;
                if (!weather.TryGet(i, out region)) { _count = i; changed = true; break; }
                if (_regions[i].WorldCenter != region.WorldCenter || _regions[i].Radii != region.Radii || _regions[i].Heading != region.Heading || _regions[i].Hurricane != region.Hurricane) changed = true;
                _regions[i] = region;
            }
            // Map camera follows the player while open; even stationary weather must follow that projection.
            if (_view != camera.worldToCameraMatrix) { _view = camera.worldToCameraMatrix; changed = true; }
            if (changed) SetVerticesDirty();
        }
        private Matrix4x4 _view;

        private Vector2 Point(Vector3 scene, Rect rect)
        {
            var p = _camera.WorldToViewportPoint(scene);
            return new Vector2(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_camera == null) return;
            Rect rect = rectTransform.rect;
            for (int index = 0; index < _count; index++)
            {
                var region = _regions[index];
                Vector3 scene = Origin.Scene(region.WorldCenter);
                if (_camera.WorldToViewportPoint(scene).z <= 0) continue;
                Vector2 center = Point(scene, rect);
                var rotation = Quaternion.Euler(0, region.Heading, 0);
                Color ink = region.Hurricane ? new Color(1f, .28f, .12f) : new Color(1f, .76f, .24f);
                int start = helper.currentVertCount;
                var fill = ink; fill.a = .11f;
                helper.AddVert(center, fill, Vector2.zero);
                for (int i = 0; i < Segments; i++)
                {
                    Vector2 edge = region.Boundary(i);
                    Vector2 point = Point(scene + rotation * new Vector3(edge.x,0,edge.y), rect);
                    _points[i] = point;
                    var fade = fill; fade.a = Mathf.Lerp(.2f,.025f,Mathf.Clamp01((edge.y/region.Radii.y+1)*.5f));
                    helper.AddVert(point, fade, Vector2.zero);
                }
                for (int i = 0; i < Segments; i++) helper.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
                ink.a = .8f;
                for (int i = 0; i < Segments; i++)
                {
                    Vector2 a = _points[i], b = _points[(i + 1) % Segments];
                    Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * .8f;
                    int v = helper.currentVertCount;
                    helper.AddVert(a + normal, ink, Vector2.zero); helper.AddVert(a - normal, ink, Vector2.zero);
                    helper.AddVert(b - normal, ink, Vector2.zero); helper.AddVert(b + normal, ink, Vector2.zero);
                    helper.AddTriangle(v, v + 1, v + 2); helper.AddTriangle(v, v + 2, v + 3);
                }
                Vector2 tip = Point(scene + region.Velocity.normalized * (region.Radii.y * .35f), rect);
                Vector2 direction = (tip - center).normalized, side = new Vector2(-direction.y, direction.x) * 4;
                int arrow = helper.currentVertCount;
                helper.AddVert(tip, ink, Vector2.zero); helper.AddVert(center + side, ink, Vector2.zero); helper.AddVert(center - side, ink, Vector2.zero);
                helper.AddTriangle(arrow, arrow + 1, arrow + 2);
            }
        }
    }
}
