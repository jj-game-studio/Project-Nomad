using System;
using UnityEngine;

namespace Nomad.Terrain
{
    // 생성 필드 출구의 임시 표식과 진입 트리거입니다. 실제 장면 전환은 소비 측(#89)이 Entered에 연결합니다.
    [DisallowMultipleComponent]
    public sealed class FieldExit : MonoBehaviour
    {
        public FieldSpawnPoint Point { get; private set; }
        public event Action<FieldExit, Collider> Entered;

        public static FieldExit Create(FieldSpawnPoint point, Transform parent)
        {
            var root = new GameObject("FieldExit_" + point.Id);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(point.Position, point.Rotation);

            // 바닥 원판과 멀리서도 보이는 기둥만 두고, 표식 Collider는 제거해 이동을 막지 않습니다.
            CreateMarker(root.transform, new Vector3(point.Radius * 2f, 0.03f, point.Radius * 2f), 0.03f);
            CreateMarker(root.transform, new Vector3(0.25f, 1.5f, 0.25f), 1.5f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up;
            trigger.size = new Vector3(point.Radius * 2f, 2f, point.Radius * 2f);

            FieldExit exit = root.AddComponent<FieldExit>();
            exit.Point = point;
            return exit;
        }

        private static void CreateMarker(Transform parent, Vector3 scale, float height)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(marker.GetComponent<Collider>());
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = Vector3.up * height;
            marker.transform.localScale = scale;

            // 편집 모드 검증에서도 재질이 누적되지 않도록 표식 재질 하나를 공유합니다.
            var renderer = marker.GetComponent<Renderer>();
            if (markerMaterial == null)
                markerMaterial = new Material(renderer.sharedMaterial) { name = "FieldExitMarker", color = new Color(0.2f, 0.9f, 0.5f) };
            renderer.sharedMaterial = markerMaterial;
        }

        private static Material markerMaterial;

        private static void DestroyCollider(Collider value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnTriggerEnter(Collider other) => Entered?.Invoke(this, other);
    }
}
