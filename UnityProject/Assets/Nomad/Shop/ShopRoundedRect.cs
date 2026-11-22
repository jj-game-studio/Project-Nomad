using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Shop
{
    // 크기를 바꿔도 모서리·테두리 두께가 유지되는 uGUI 도형입니다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShopRoundedRect : MaskableGraphic
    {
        private const int CornerSteps = 10;
        private const int RingSize = 4 * (CornerSteps + 1);
        private const float Feather = 0.75f;

        [SerializeField] private float radius = 12;
        [SerializeField] private float strokeWidth = 1.5f;
        [SerializeField] private Color stroke = new Color(0.65f, 0.68f, 0.57f);
        [SerializeField, Range(0, 0.2f)] private float shading;

        public float Radius => radius;

        internal void SetShading(float amount)
        {
            shading = Mathf.Clamp(amount, 0, 0.2f);
            SetVerticesDirty();
        }

        public void Style(float cornerRadius, float borderWidth, Color borderColor)
        {
            radius = cornerRadius;
            strokeWidth = borderWidth;
            stroke = borderColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0)
                return;

            float corner = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) * 0.5f);
            float border = Mathf.Clamp(strokeWidth, 0, Mathf.Max(0, Mathf.Min(rect.width, rect.height) * 0.5f - Feather));
            Color edge = border > 0 ? stroke : color;
            Color transparent = new Color(edge.r, edge.g, edge.b, 0);
            AddRing(mesh, rect, corner, transparent);
            AddRing(mesh, Inset(rect, Feather), Mathf.Max(0, corner - Feather), edge);
            AddRing(mesh, Inset(rect, border + Feather), Mathf.Max(0, corner - border - Feather), color);

            for (int ring = 0; ring < 2; ring++)
            {
                int outer = ring * RingSize;
                int inner = outer + RingSize;
                for (int index = 0; index < RingSize; index++)
                {
                    int next = (index + 1) % RingSize;
                    mesh.AddTriangle(outer + index, outer + next, inner + index);
                    mesh.AddTriangle(outer + next, inner + next, inner + index);
                }
            }

            int center = mesh.currentVertCount;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int index = 0; index < RingSize; index++)
                mesh.AddTriangle(center, RingSize * 2 + index, RingSize * 2 + (index + 1) % RingSize);

            // 상점 테마에서만 얕은 명암을 사용하며, 테두리의 투명도는 유지합니다.
            if (shading > 0)
            {
                var vertex = new UIVertex();
                for (int index = 0; index < mesh.currentVertCount; index++)
                {
                    mesh.PopulateUIVertex(ref vertex, index);
                    float light = Mathf.Lerp(1 - shading, 1 + shading,
                        Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y));
                    vertex.color = (Color)vertex.color * new Color(light, light, light, 1);
                    mesh.SetUIVertex(vertex, index);
                }
            }
        }

        private static Rect Inset(Rect rect, float amount) => new Rect(
            rect.x + amount, rect.y + amount, Mathf.Max(0, rect.width - amount * 2), Mathf.Max(0, rect.height - amount * 2));

        private static void AddRing(VertexHelper mesh, Rect rect, float corner, Color tint)
        {
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                Vector2 center = new Vector2(
                    quadrant == 0 || quadrant == 3 ? rect.xMax - corner : rect.xMin + corner,
                    quadrant < 2 ? rect.yMax - corner : rect.yMin + corner);
                for (int step = 0; step <= CornerSteps; step++)
                {
                    float angle = (quadrant * 90f + step * 90f / CornerSteps) * Mathf.Deg2Rad;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * corner, tint, Vector2.zero);
                }
            }
        }
    }
}
