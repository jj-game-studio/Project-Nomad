using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Visibility
{
    // Game 뷰에서도 보이는 선택적 선 표시입니다. 실제 시야 판정에는 영향을 주지 않습니다.
    [DefaultExecutionOrder(700)]
    public sealed class VisibilityDebugRange : MonoBehaviour
    {
        private const int Segments = 128;
        private readonly Vector3[] boundary = new Vector3[Segments + 3];
        private readonly Vector3[] unblocked = new Vector3[Segments + 3];
        private PlayerVisibility sight;
        private GameObject root;
        private Material material;
        private LineRenderer fullRange;
        private LineRenderer clearRange;

        public void Configure(PlayerVisibility source)
        {
            sight = source;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (sight == null || !sight.Available || !sight.ShowDebugRange)
            {
                Hide();
                return;
            }

            if (root == null) CreateLines();
            root.SetActive(true);
            Vector3 eye = sight.Eye;
            boundary[0] = unblocked[0] = eye;
            boundary[^1] = unblocked[^1] = eye;

            for (int index = 0; index <= Segments; index++)
            {
                float degrees = -sight.Angle * 0.5f + sight.Angle * index / Segments;
                Vector3 direction = Quaternion.AngleAxis(degrees, Vector3.up) * sight.Forward;
                float length = sight.ClearDistance(eye, direction, sight.Distance);
                boundary[index + 1] = eye + direction * sight.Distance;
                // 최대 범위와 겹쳐도 둘 다 구별할 수 있도록 높이를 조금 띄웁니다.
                unblocked[index + 1] = eye + direction * length + Vector3.up * 0.025f;
            }

            fullRange.SetPositions(boundary);
            clearRange.SetPositions(unblocked);
        }

        private void CreateLines()
        {
            root = new GameObject("Visibility Debug Range");
            root.transform.SetParent(transform, false);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            fullRange = CreateLine("Maximum Cone", new Color(0.1f, 0.9f, 1f), 0.06f);
            clearRange = CreateLine("Occluded Boundary", new Color(1f, 0.8f, 0.1f), 0.035f);
        }

        private LineRenderer CreateLine(string label, Color color, float width)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            line.useWorldSpace = true;
            line.positionCount = Segments + 3;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            line.SetPropertyBlock(tint);
            return line;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            Hide();
            if (root != null) Destroy(root);
            if (material != null) Destroy(material);
        }
    }
}
