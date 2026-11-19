using UnityEngine;

namespace Nomad.WorldHud
{
    // 텍스처 없이 화면 공간에서 일정한 두께의 원호를 그립니다.
    public sealed class WorldHudGraphic : UnityEngine.UI.MaskableGraphic
    {
        public float StartAngle = -65f;
        public float Sweep = 130f;
        public float Thickness = 4f;
        private float fraction = 1f;

        public float Fraction
        {
            get => fraction;
            set { fraction = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            float outer = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            float inner = Mathf.Max(0, outer - Thickness);
            Vector2 center = rectTransform.rect.center;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(Sweep * fraction) / 5f));

            for (int i = 0; i <= steps; i++)
            {
                float angle = (StartAngle + Sweep * fraction * i / steps) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * outer, color, Vector2.zero);
                mesh.AddVert(center + direction * inner, color, Vector2.zero);
                if (i == 0) continue;

                int end = i * 2;
                mesh.AddTriangle(end - 2, end, end - 1);
                mesh.AddTriangle(end, end + 1, end - 1);
            }
        }
    }
}
