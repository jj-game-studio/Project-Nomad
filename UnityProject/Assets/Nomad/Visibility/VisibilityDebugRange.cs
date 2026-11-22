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
        private readonly Vector3[] circle = new Vector3[Segments + 1];
        private readonly Vector3[] nearbyBoundary = new Vector3[Segments + 1];
        private PlayerVisibility sight;
        private GameObject root;
        private Material material;
        private LineRenderer fullRange;
        private LineRenderer clearRange;
        private LineRenderer nearbyRange;
        private LineRenderer clearNearbyRange;

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
                float length = sight.ClearDistance(eye, direction, sight.RangeAtAngle(degrees));
                boundary[index + 1] = eye + direction * sight.Distance;
                // 최대 범위와 겹쳐도 둘 다 구별할 수 있도록 높이를 조금 띄웁니다.
                unblocked[index + 1] = eye + direction * length + Vector3.up * 0.025f;
            }

            fullRange.SetPositions(boundary);
            clearRange.SetPositions(unblocked);

            nearbyRange.enabled = clearNearbyRange.enabled = sight.NearbyRadius > 0f;
            if (sight.NearbyRadius <= 0f) return;
            for (int index = 0; index <= Segments; index++)
            {
                Vector3 direction = Quaternion.AngleAxis(360f * index / Segments, Vector3.up) * Vector3.forward;
                float length = sight.ClearDistance(eye, direction, sight.NearbyRadius);
                circle[index] = eye + direction * sight.NearbyRadius;
                nearbyBoundary[index] = eye + direction * length + Vector3.up * 0.025f;
            }
            nearbyRange.SetPositions(circle);
            clearNearbyRange.SetPositions(nearbyBoundary);
        }

        private void CreateLines()
        {
            root = new GameObject("Visibility Debug Range");
            root.transform.SetParent(transform, false);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            fullRange = CreateLine("Maximum Cone", new Color(0.1f, 0.9f, 1f), 0.06f);
            clearRange = CreateLine("Occluded Boundary", new Color(1f, 0.8f, 0.1f), 0.035f);
            nearbyRange = CreateLine("Nearby Circle", new Color(0.85f, 0.3f, 1f), 0.06f);
            clearNearbyRange = CreateLine("Nearby Boundary", new Color(1f, 0.8f, 0.1f), 0.035f);
            nearbyRange.positionCount = clearNearbyRange.positionCount = Segments + 1;
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
