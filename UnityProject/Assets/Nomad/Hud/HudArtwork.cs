using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Hud
{
    // 벡터 도형으로 그려 해상도에 관계없이 HUD 윤곽을 유지합니다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudArtwork : MaskableGraphic
    {
        public enum Shape { Panel, Ring, Heart, Crystal, Bolt, Weapon, Leather, MetalRing, FacetedCrystal, Boot, Fittings, Star }

        [SerializeField] private Shape shape;
        [SerializeField, Range(0f, 1f)] private float fraction = 1f;
        [SerializeField] private float thickness = 4f;

        private Texture2D leatherTexture;
        private Rect drawingRect;

        public override Texture mainTexture
        {
            get
            {
                if (shape != Shape.Leather)
                    return Texture2D.whiteTexture;
                if (leatherTexture == null)
                    CreateLeather();
                return leatherTexture;
            }
        }

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
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            drawingRect = rect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            if (shape == Shape.Panel || shape == Shape.Leather)
            {
                RoundedPanel(mesh, rect);
                return;
            }

            if (shape == Shape.Ring || shape == Shape.MetalRing)
            {
                Ring(mesh, rect);
                return;
            }

            if (shape == Shape.Fittings)
            {
                foreach (Vector2 corner in new[] { P(.09f,.09f), P(.91f,.09f), P(.91f,.91f), P(.09f,.91f) })
                {
                    Vector2 fittingCenter = rect.min + Vector2.Scale(rect.size, corner);
                    Disc(mesh, fittingCenter + new Vector2(1, -1), 2.2f, color * .4f);
                    Disc(mesh, fittingCenter, 1.6f, color);
                }
                return;
            }

            if (shape == Shape.Heart)
            {
                HeartIcon(mesh, rect);
                return;
            }

            if (shape == Shape.Star)
            {
                StarIcon(mesh, rect);
                return;
            }

            if (shape == Shape.FacetedCrystal)
            {
                Vector2 tip = P(.5f,.96f), left = P(.16f,.53f), bottom = P(.5f,.12f), right = P(.84f,.53f);
                Facet(mesh, rect, tip, left, P(.5f,.53f), color * .65f);
                Facet(mesh, rect, tip, P(.5f,.53f), right, Color.Lerp(color, Color.white, .5f));
                Facet(mesh, rect, bottom, left, P(.5f,.53f), color * .8f);
                Facet(mesh, rect, bottom, P(.5f,.53f), right, color);
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
                Shape.Boot => new[] { P(.35f,.9f), P(.68f,.9f), P(.61f,.43f), P(.89f,.27f),
                    P(.88f,.13f), P(.17f,.13f), P(.17f,.3f), P(.32f,.45f) },
                _ => new[] { P(.08f,.48f), P(.17f,.69f), P(.87f,.69f), P(.94f,.61f),
                    P(.61f,.54f), P(.57f,.36f), P(.45f,.36f), P(.48f,.54f), P(.3f,.54f), P(.2f,.35f) }
            };

            // 각 아이콘은 단순 외곽선이며 중앙 삼각형 팬으로 채웁니다.
            Vector2 center = shape == Shape.Weapon ? P(.5f,.58f) : P(.5f,.54f);
            Fan(mesh, rect, center, points);
        }

        // 두 원과 아래 삼각형을 겹쳐 둥근 하트를 만들고, 왼쪽 위에 광택을 얹습니다.
        private void HeartIcon(VertexHelper mesh, Rect rect)
        {
            float radius = rect.width * .25f;
            Vector2 left = rect.min + Vector2.Scale(rect.size, P(.28f, .68f));
            Vector2 right = rect.min + Vector2.Scale(rect.size, P(.72f, .68f));
            Color shadow = color * .7f;
            shadow.a = color.a;

            Disc(mesh, left, radius, color);
            Disc(mesh, right, radius, color);

            int start = mesh.currentVertCount;
            Add(mesh, rect.min + Vector2.Scale(rect.size, P(.04f, .6f)), color);
            Add(mesh, rect.min + Vector2.Scale(rect.size, P(.96f, .6f)), color);
            Add(mesh, rect.min + Vector2.Scale(rect.size, P(.5f, .06f)), shadow);
            mesh.AddTriangle(start, start + 1, start + 2);

            Disc(mesh, left + new Vector2(-radius * .25f, radius * .3f), radius * .28f, Color.Lerp(color, Color.white, .55f));
        }

        // 다섯 꼭짓점 별을 중심 팬으로 채우고 중심을 밝혀 입체감을 줍니다.
        private void StarIcon(VertexHelper mesh, Rect rect)
        {
            Vector2 center = rect.center + new Vector2(0, -rect.height * .03f);
            float outer = Mathf.Min(rect.width, rect.height) * .5f;
            float inner = outer * .45f;
            int start = mesh.currentVertCount;
            Add(mesh, center, Color.Lerp(color, Color.white, .35f));

            for (int point = 0; point < 10; point++)
            {
                float angle = (90f + point * 36f) * Mathf.Deg2Rad;
                float distance = point % 2 == 0 ? outer : inner;
                Add(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, color);
            }

            for (int point = 0; point < 10; point++)
                mesh.AddTriangle(start, start + 1 + point, start + 1 + (point + 1) % 10);
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
                Color rim = shape == Shape.MetalRing
                    ? Color.Lerp(color * .45f, Color.Lerp(color, Color.white, .35f), (direction.y + 1) * .5f) : color;
                Add(mesh, rect.center + direction * outer, rim);
                Add(mesh, rect.center + direction * inner, shape == Shape.MetalRing ? color * .65f : color);
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
            Add(mesh, position, color);
        }

        private void Add(VertexHelper mesh, Vector2 position, Color tint)
        {
            Vector2 uv = new Vector2((position.x - drawingRect.xMin) / drawingRect.width,
                (position.y - drawingRect.yMin) / drawingRect.height);
            mesh.AddVert(position, tint, uv);
        }

        private void Facet(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            foreach (Vector2 point in new[] { a, b, c })
                Add(mesh, rect.min + Vector2.Scale(rect.size, point), tint);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            Add(mesh, center, tint);
            for (int step = 0; step < 24; step++)
            {
                float angle = step * Mathf.PI / 12;
                Add(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
            }
            for (int step = 0; step < 24; step++)
                mesh.AddTriangle(start, start + 1 + step, start + 1 + (step + 1) % 24);
        }

        private void CreateLeather()
        {
            leatherTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "HUD leather grain" };
            leatherTexture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    // 고정 해시 질감으로 비교 전환 시 무늬가 달라지지 않게 합니다.
                    int noise = ((x * 73856093) ^ (y * 19349663)) & 31;
                    byte value = (byte)(205 + noise);
                    pixels[y * 64 + x] = new Color32(value, value, value, 255);
                }
            leatherTexture.SetPixels32(pixels);
            leatherTexture.Apply(false, true);
        }

        protected override void OnDestroy()
        {
            if (leatherTexture != null)
                Destroy(leatherTexture);
            base.OnDestroy();
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);
    }
}
