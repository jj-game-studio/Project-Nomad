using UnityEngine;

namespace Nomad.MainMenu
{
    // 배경과 분리된 그라데이션이므로 그림을 바꿔도 왼쪽 글자의 대비를 유지합니다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainMenuShade : UnityEngine.UI.Graphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float[] stops = { 0f, 0.26f, 0.37f, 0.49f, 0.65f, 1f };
            float[] alpha = { 0.98f, 0.94f, 0.76f, 0.32f, 0.04f, 0.04f };

            for (int i = 0; i < stops.Length; i++)
            {
                Color tint = new Color(0.025f, 0.055f, 0.037f, alpha[i]);
                float x = Mathf.Lerp(rect.xMin, rect.xMax, stops[i]);
                mesh.AddVert(new Vector3(x, rect.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(x, rect.yMax), tint, Vector2.one);

                if (i == 0)
                    continue;

                int start = (i - 1) * 2;
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start + 2, start + 1, start + 3);
            }
        }
    }
}
