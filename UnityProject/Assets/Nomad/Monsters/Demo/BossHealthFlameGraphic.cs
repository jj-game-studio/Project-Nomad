using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossHealthFlameGraphic : Graphic
    {
        public float Intensity;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (Intensity <= 0.01f) return;
            float width = rectTransform.rect.width;
            for (int index = 0; index < 44; index++)
            {
                float wave = Mathf.Repeat(index * 0.618f + Time.time * 0.08f, 1);
                float x = -width * 0.5f + wave * width;
                float height = (4 + 13 * Mathf.Abs(Mathf.Sin(index * 3.7f + Time.time * 5))) * Intensity;
                float y = 12;
                int first = helper.currentVertCount;
                helper.AddVert(new Vector2(x - 3, y), new Color(1, 0.12f, 0.01f, 0.7f * Intensity), Vector2.zero);
                helper.AddVert(new Vector2(x + 3, y), new Color(1, 0.62f, 0.1f, 0.8f * Intensity), Vector2.zero);
                helper.AddVert(new Vector2(x + Mathf.Sin(Time.time * 4 + index) * 5, y + height), new Color(1, 0.25f, 0, 0), Vector2.zero);
                helper.AddTriangle(first, first + 1, first + 2);
            }
        }
    }
}
