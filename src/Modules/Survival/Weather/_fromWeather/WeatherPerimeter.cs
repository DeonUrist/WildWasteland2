using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Weather
{
    // Cached arc-length layout on the same 64-point boundary used by the map.
    internal sealed class WeatherPerimeter
    {
        private readonly Vector2[] _points = new Vector2[65];
        private readonly float[] _lengths = new float[65];
        internal readonly Vector3[] Centers = new Vector3[48];
        internal readonly Quaternion[] Rotations = new Quaternion[48];
        internal readonly float[] Spans = new float[48];
        private Vector2 _radii;
        internal int Count;
        internal void Update(WeatherRegion region)
        {
            if (Count > 0 && _radii == region.Radii) return;
            _radii = region.Radii; _lengths[0] = 0;
            for (int i=0;i<=64;i++)
            {
                _points[i] = region.Boundary(i%64);
                if (i>0) _lengths[i] = _lengths[i-1]+Vector2.Distance(_points[i-1],_points[i]);
            }
            Count = Mathf.Clamp(Mathf.CeilToInt(_lengths[64]/1500),8,48);
            float spacing = _lengths[64]/Count;
            for (int i=0;i<Count;i++)
            {
                Vector2 a = Point(i*spacing), b = Point((i+1)*spacing), delta = b-a;
                Centers[i] = new Vector3((a.x+b.x)*.5f,0,(a.y+b.y)*.5f);
                Rotations[i] = Quaternion.LookRotation(new Vector3(-delta.y,0,delta.x));
                Spans[i] = spacing; // Arc length overlaps the straight chord at rounded corners.
            }
        }
        private Vector2 Point(float distance)
        {
            for (int i=1;i<=64;i++) if (distance <= _lengths[i])
                return Vector2.Lerp(_points[i-1],_points[i],(distance-_lengths[i-1])/Mathf.Max(.001f,_lengths[i]-_lengths[i-1]));
            return _points[64];
        }
    }
}
