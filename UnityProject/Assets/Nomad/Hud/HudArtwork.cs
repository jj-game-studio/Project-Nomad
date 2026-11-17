using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Hud
{
    // 벡터 도형으로 그려 해상도에 관계없이 HUD 윤곽을 유지합니다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudArtwork : MaskableGraphic
    {
        public enum Shape { Panel, Ring, Heart, Crystal, Bolt, Weapon }

        [SerializeField] private Shape shape;
        [SerializeField, Range(0f, 1f)] private float fraction = 1f;
        [SerializeField] private float thickness = 4f;

        public float Fraction
        {
            get => fraction;
            set
            {
                float next = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
                if (Mathf.Approximately(next, fraction))
                    return;

                fraction = next;
                SetVerticesDirty();
            }
        }

        public void Configure(Shape kind, Color tint, float lineWidth = 4f)
        {
            shape = kind;
            color = tint;
            thickness = lineWidth;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0)
                return;

            if (shape == Shape.Panel)
            {
                RoundedPanel(mesh, rect);
                return;
            }

            if (shape == Shape.Ring)
            {
                Ring(mesh, rect);
                return;
            }

            Vector2[] points = shape switch
            {
                Shape.Heart => new[] { P(.5f,.16f), P(.1f,.55f), P(.1f,.78f), P(.28f,.91f),
                    P(.5f,.76f), P(.72f,.91f), P(.9f,.78f), P(.9f,.55f) },
                Shape.Crystal => new[] { P(.5f,.96f), P(.82f,.53f), P(.68f,.12f),
                    P(.32f,.12f), P(.18f,.53f) },
                Shape.Bolt => new[] { P(.56f,.96f), P(.19f,.44f), P(.46f,.44f),
                    P(.38f,.05f), P(.83f,.61f), P(.57f,.61f) },
                _ => new[] { P(.08f,.48f), P(.17f,.69f), P(.87f,.69f), P(.94f,.61f),
                    P(.61f,.54f), P(.57f,.36f), P(.45f,.36f), P(.48f,.54f), P(.3f,.54f), P(.2f,.35f) }
            };

            // 각 아이콘은 단순 외곽선이며 중앙 삼각형 팬으로 채웁니다.
            Vector2 center = shape == Shape.Weapon ? P(.5f,.58f) : P(.5f,.54f);
            Fan(mesh, rect, center, points);
        }

        private void RoundedPanel(VertexHelper mesh, Rect rect)
        {
            float radius = Mathf.Min(15f, rect.height * .5f, rect.width * .5f);
            Add(mesh, rect.center);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMin + radius : rect.yMax - radius);
                for (int step = 0; step <= 8; step++)
                {
                    float angle = (-90f + corner * 90f + step * 90f / 8f) * Mathf.Deg2Rad;
                    Add(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }

            for (int index = 1; index <= 36; index++)
                mesh.AddTriangle(0, index, index == 36 ? 1 : index + 1);
        }

        private void Ring(VertexHelper mesh, Rect rect)
        {
            if (fraction <= 0f)
                return;

            float outer = Mathf.Min(rect.width, rect.height) * .5f;
            float inner = Mathf.Max(0f, outer - thickness);
            int segments = Mathf.Max(1, Mathf.CeilToInt(96 * fraction));
            for (int step = 0; step <= segments; step++)
            {
                float angle = (90f - 360f * fraction * step / segments) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Add(mesh, rect.center + direction * outer);
                Add(mesh, rect.center + direction * inner);
                if (step == 0)
                    continue;

                int start = (step - 1) * 2;
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start + 1, start + 3, start + 2);
            }
        }

        private void Fan(VertexHelper mesh, Rect rect, Vector2 center, Vector2[] points)
        {
            Add(mesh, rect.min + Vector2.Scale(rect.size, center));
            foreach (Vector2 point in points)
                Add(mesh, rect.min + Vector2.Scale(rect.size, point));

            for (int index = 0; index < points.Length; index++)
                mesh.AddTriangle(0, index + 1, (index + 1) % points.Length + 1);
        }

        private void Add(VertexHelper mesh, Vector2 position)
        {
            mesh.AddVert(position, color, Vector2.zero);
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);
    }
}
