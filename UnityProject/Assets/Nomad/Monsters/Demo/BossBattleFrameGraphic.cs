using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossBattleFrameGraphic : MaskableGraphic
    {
        public bool Ring;
        public float Fraction = 1;
        public Color GaugeColor = new Color(0.2f, 0.65f, 1);
        private static readonly Color Gold = new Color(0.62f, 0.43f, 0.22f);

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect r = rectTransform.rect;
            if (Ring)
            {
                Arc(helper, r.width * 0.5f - 4, 1, new Color(0.08f, 0.065f, 0.06f));
                Arc(helper, r.width * 0.5f - 4, Fraction, GaugeColor);
                return;
            }
            float x = r.width * 0.5f, y = r.height * 0.5f;
            Panel(helper, x, y, Gold);
            Panel(helper, x - 3, y - 3, new Color(0.055f, 0.04f, 0.045f, 0.96f));
            foreach (float side in new[] { -1f, 1f })
            {
                Triangle(helper, new Vector2(side * (x - 15), y * 0.15f), new Vector2(side * (x + 38), y * 0.6f), new Vector2(side * (x - 4), -y * 0.2f), Gold);
                Triangle(helper, new Vector2(side * (x - 12), y * 0.1f), new Vector2(side * (x + 23), y * 0.4f), new Vector2(side * (x - 3), -y * 0.08f), new Color(0.2f, 0.12f, 0.07f));
            }
            Triangle(helper, new Vector2(-16, y - 2), new Vector2(0, y + 25), new Vector2(16, y - 2), Gold);
            Triangle(helper, new Vector2(-6, y + 2), new Vector2(0, y + 18), new Vector2(6, y + 2), new Color(2, 0.4f, 0.05f));
        }

        private static void Panel(VertexHelper h, float x, float y, Color color)
        {
            Vector2[] points = { new(-x + 18, -y), new(x - 18, -y), new(x, -y + 18), new(x, y - 18),
                new(x - 18, y), new(-x + 18, y), new(-x, y - 18), new(-x, -y + 18) };
            for (int index = 0; index < points.Length; index++) Triangle(h, Vector2.zero, points[index], points[(index + 1) % points.Length], color);
        }
        private static void Triangle(VertexHelper h, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = h.currentVertCount;
            h.AddVert(a, color, Vector2.zero); h.AddVert(b, color, Vector2.zero); h.AddVert(c, color, Vector2.zero);
            h.AddTriangle(start, start + 1, start + 2);
        }
        private void Arc(VertexHelper h, float radius, float fraction, Color color)
        {
            for (int index = 0; index < 64; index++)
            {
                if (index / 64f >= fraction) break;
                float a = (90 - index * 360f / 64) * Mathf.Deg2Rad, b = (90 - Mathf.Min(index + 1, fraction * 64) * 360f / 64) * Mathf.Deg2Rad;
                Vector2 first = new(Mathf.Cos(a), Mathf.Sin(a)), next = new(Mathf.Cos(b), Mathf.Sin(b));
                Triangle(h, first * radius, next * radius, first * (radius - 6), color);
                Triangle(h, first * (radius - 6), next * radius, next * (radius - 6), color);
            }
        }
    }
}
