using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    public sealed class VehicleStatusIcon : MaskableGraphic
    {
        internal int Kind;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Kind == 3)
            {
                Stroke(vh, new Vector2(7, 35), new Vector2(13, 45)); Stroke(vh, new Vector2(13, 45), new Vector2(25, 45));
                Stroke(vh, new Vector2(25, 45), new Vector2(31, 35)); Stroke(vh, new Vector2(31, 35), new Vector2(25, 25));
                Stroke(vh, new Vector2(25, 25), new Vector2(13, 25)); Stroke(vh, new Vector2(13, 25), new Vector2(7, 35));
                Ring(vh, new Vector2(19, 35), 4, 0, 360);
                Stroke(vh, new Vector2(25, 29), new Vector2(44, 10)); Stroke(vh, new Vector2(29, 33), new Vector2(48, 14));
                Stroke(vh, new Vector2(44, 10), new Vector2(48, 14));
                Stroke(vh, new Vector2(32, 22), new Vector2(38, 28)); Stroke(vh, new Vector2(36, 18), new Vector2(42, 24));
                Stroke(vh, new Vector2(40, 14), new Vector2(46, 20));
                return;
            }
            if (Kind == 0)
            {
                Ring(vh, new Vector2(16, 36), 10, 0, 360);
                Stroke(vh, new Vector2(23, 29), new Vector2(43, 9));
                Stroke(vh, new Vector2(33, 19), new Vector2(38, 24));
                Stroke(vh, new Vector2(39, 13), new Vector2(44, 18));
            }
            else if (Kind == 1)
            {
                Ring(vh, new Vector2(26, 26), 17, 0, 360);
                Ring(vh, new Vector2(26, 26), 24, 145, 215);
                Ring(vh, new Vector2(26, 26), 24, -35, 35);
                Stroke(vh, new Vector2(22, 15), new Vector2(22, 37));
                Stroke(vh, new Vector2(22, 37), new Vector2(31, 37));
                Stroke(vh, new Vector2(31, 37), new Vector2(34, 33));
                Stroke(vh, new Vector2(34, 33), new Vector2(34, 28));
                Stroke(vh, new Vector2(34, 28), new Vector2(31, 25));
                Stroke(vh, new Vector2(31, 25), new Vector2(22, 25));
            }
            else
            {
                Stroke(vh, new Vector2(5, 12), new Vector2(47, 12));
                Stroke(vh, new Vector2(47, 12), new Vector2(47, 40));
                Stroke(vh, new Vector2(47, 40), new Vector2(5, 40));
                Stroke(vh, new Vector2(5, 40), new Vector2(5, 12));
                Ring(vh, new Vector2(15, 30), 5, 0, 360);
                Ring(vh, new Vector2(37, 30), 5, 0, 360);
                Stroke(vh, new Vector2(20, 30), new Vector2(32, 30));
                Stroke(vh, new Vector2(12, 13), new Vector2(16, 21));
                Stroke(vh, new Vector2(16, 21), new Vector2(36, 21));
                Stroke(vh, new Vector2(36, 21), new Vector2(40, 13));
            }
        }

        private void Ring(VertexHelper vh, Vector2 centre, float radius, float from, float to)
        {
            int steps = Mathf.CeilToInt((to - from) / 12f);
            float angle = from * Mathf.Deg2Rad;
            var previous = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            for (int i = 1; i <= steps; i++)
            {
                angle = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                var next = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Stroke(vh, previous, next); previous = next;
            }
        }

        private void Stroke(VertexHelper vh, Vector2 from, Vector2 to)
        {
            var direction = (to - from).normalized;
            var side = new Vector2(-direction.y, direction.x) * 1.5f;
            var offset = rectTransform.rect.min;
            int start = vh.currentVertCount;
            Color32 tint = color;
            vh.AddVert(offset + from - side, tint, Vector2.zero);
            vh.AddVert(offset + from + side, tint, Vector2.zero);
            vh.AddVert(offset + to + side, tint, Vector2.zero);
            vh.AddVert(offset + to - side, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
