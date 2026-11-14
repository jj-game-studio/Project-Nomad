using System;
using System.Collections.Generic;
using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Props
{
    [DisallowMultipleComponent]
    public sealed class PropScatter : MonoBehaviour
    {
        public const int CandidatesPerPiece = 50;
        public const float PropRadius = 0.6f;
        public const float PropHeight = 1f;

        [SerializeField] private TerrainGenerator generator;
        [SerializeField] private GameObject propPrefab;
        [SerializeField, Range(0, 4)] private int propsPerPiece = 2;

        private GameObject generatedRoot;
        private readonly List<Vector3> positions = new();
        public IReadOnlyList<Vector3> Positions => positions.AsReadOnly();
        public string GenerationId { get; private set; }
        public int CandidateChecks { get; private set; }

        public void Configure(TerrainGenerator source, GameObject prefab, int count = 2)
        {
            if (isActiveAndEnabled && generator != null)
                Unsubscribe();

            Clear();
            generator = source;
            propPrefab = prefab;
            propsPerPiece = Mathf.Clamp(count, 0, 4);
            if (isActiveAndEnabled)
                Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Clear();

        private void Subscribe()
        {
            if (generator == null) return;
            generator.GenerationStarted += OnStarted;
            generator.GenerationCompleted += Scatter;
            if (generator.LastResult != null) Scatter(generator.LastResult);
        }

        private void Unsubscribe()
        {
            if (generator == null) return;
            generator.GenerationStarted -= OnStarted;
            generator.GenerationCompleted -= Scatter;
        }

        private void OnStarted(string id) => Clear();

        public void Scatter(TerrainGenerationResult result)
        {
            if (result == null || result.GenerationId == GenerationId) return;
            Clear();
            if (!result.Success || propPrefab == null) return;

            ValidatePrefab();
            GenerationId = result.GenerationId;
            generatedRoot = new GameObject("GeneratedProps_" + GenerationId);
            generatedRoot.transform.SetParent(transform, false);
            var random = new System.Random(result.Seed ^ 160016);
            Physics.SyncTransforms();

            foreach (TerrainPiece piece in result.Pieces)
            {
                // 계단 전역은 예약합니다. 평지에서도 모든 연결점-중앙 통로를 보존합니다.
                if (piece.Kind == TerrainPieceKind.Stairs) continue;
                int placed = 0;
                Bounds area = piece.LocalOccupancy;
                for (int attempt = 0; attempt < CandidatesPerPiece && placed < propsPerPiece; attempt++)
                {
                    CandidateChecks++;
                    Vector3 local = new Vector3(
                        Mathf.Lerp(area.min.x + 1f, area.max.x - 1f, (float)random.NextDouble()),
                        area.max.y + 1f,
                        Mathf.Lerp(area.min.z + 1f, area.max.z - 1f, (float)random.NextDouble()));
                    Vector3 point = piece.transform.TransformPoint(local);
                    if (!TryFloor(piece, point, out point) || !PreservesPassages(piece, point)
                        || !ClearSpace(point)) continue;

                    GameObject prop = Instantiate(propPrefab, generatedRoot.transform);
                    prop.transform.SetPositionAndRotation(point, Quaternion.identity);
                    prop.SetActive(true);
                    positions.Add(point);
                    placed++;
                    Physics.SyncTransforms();
                }
            }
        }

        public static bool PreservesPassages(TerrainPiece piece, Vector3 point)
        {
            Vector3 center = piece.transform.TransformPoint(piece.LocalOccupancy.center);
            center.y = point.y;
            foreach (TerrainConnection connection in piece.Connections)
            {
                Vector3 end = connection.Position;
                end.y = point.y;
                Vector3 delta = end - center;
                float t = delta.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector3.Dot(point - center, delta) / delta.sqrMagnitude);
                // 연결점 통로 폭에 소품 반경과 여유를 더해 예약합니다.
                if (Vector3.Distance(point, center + delta * t) < connection.Width * 0.5f + PropRadius + 0.1f)
                    return false;
            }
            return Vector3.Distance(point, center) >= 2f;
        }

        private static bool TryFloor(TerrainPiece piece, Vector3 origin, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)
                || !hit.collider.transform.IsChildOf(piece.transform) || hit.normal.y < 0.99f) return false;
            point = hit.point;

            // 네 모서리 바닥을 확인하여 낭떠러지·작은 받침·높이 차에 걸친 배치를 막습니다.
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 sample = point + new Vector3(x * PropRadius, 0.5f, z * PropRadius);
                if (!Physics.Raycast(sample, Vector3.down, out RaycastHit support, 0.55f, ~0, QueryTriggerInteraction.Ignore)
                    || !support.collider.transform.IsChildOf(piece.transform) || support.normal.y < 0.99f
                    || Mathf.Abs(support.point.y - point.y) > 0.02f) return false;
            }
            return true;
        }

        private void ValidatePrefab()
        {
            if (propPrefab.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("소품 루트 배율은 1이어야 합니다.");

            Collider[] colliders = propPrefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0 || propPrefab.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("소품은 정적 BoxCollider가 필요합니다.");

            foreach (Collider collider in colliders)
            {
                if (collider is not BoxCollider box || box.isTrigger || !box.enabled)
                    throw new InvalidOperationException("소품은 활성 비트리거 BoxCollider만 지원합니다.");
                ValidateBounds(box.transform, new Bounds(box.center, box.size));
            }
            foreach (MeshFilter filter in propPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) ValidateBounds(filter.transform, filter.sharedMesh.bounds);
            }
        }

        private void ValidateBounds(Transform source, Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 local = propPrefab.transform.InverseTransformPoint(source.TransformPoint(corner));
                if (Mathf.Abs(local.x) > PropRadius || Mathf.Abs(local.z) > PropRadius
                    || local.y < -0.001f || local.y > PropHeight + 0.001f)
                    throw new InvalidOperationException("소품 외형/충돌체는 반경 0.6m·높이 1m의 예약 영역 안에 있어야 합니다.");
            }
        }

        private static bool ClearSpace(Vector3 point)
        {
            return !Physics.CheckBox(point + Vector3.up * (PropHeight * 0.5f + 0.05f),
                new Vector3(PropRadius, PropHeight * 0.5f, PropRadius), Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore);
        }

        private void Clear()
        {
            GenerationId = null;
            CandidateChecks = 0;
            positions.Clear();
            if (generatedRoot == null) return;
            generatedRoot.SetActive(false);
            if (Application.isPlaying) Destroy(generatedRoot);
            else DestroyImmediate(generatedRoot);
            generatedRoot = null;
        }
    }
}


