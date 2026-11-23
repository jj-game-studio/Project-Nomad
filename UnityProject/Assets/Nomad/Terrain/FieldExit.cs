using System;
using Nomad.Interaction;
using UnityEngine;

namespace Nomad.Terrain
{
    // 출구를 스치는 전투/회피로 지역이 바뀌지 않도록 명시적인 E 상호작용으로만 귀환합니다.
    [DisallowMultipleComponent]
    public sealed class FieldExit : MonoBehaviour, IInteractable, IInteractionAction
    {
        public FieldSpawnPoint Point { get; private set; }
        public event Action<FieldExit, Collider> Entered;
        public string InteractionId => "field-exit-" + Point.Id;
        public string InteractionAction => "마을로 귀환";
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.5f;

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

        public bool CanInteract(InteractionContext context) => context.Player != null &&
            Vector3.Distance(context.Player.position, transform.position) <= InteractionRange.Default;

        public void Interact(InteractionContext context)
        {
            if (!CanInteract(context)) return;
            Collider body = context.Player.GetComponent<Collider>();
            if (body != null) Entered?.Invoke(this, body);
        }
    }
}
