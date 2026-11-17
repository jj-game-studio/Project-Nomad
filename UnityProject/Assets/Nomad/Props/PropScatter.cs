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

        [SerializeField] private float placementRadius = PropRadius;
        [SerializeField] private float placementHeight = PropHeight;

        [SerializeField, HideInInspector] private GameObject generatedRoot;
        private readonly List<Vector3> positions = new();
        public IReadOnlyList<Vector3> Positions => positions.AsReadOnly();
        public string GenerationId { get; private set; }
        public int CandidateChecks { get; private set; }

        public void Configure(TerrainGenerator source, GameObject prefab, int count = 2)
        {
            Configure(source, prefab, count, PropRadius, PropHeight);
        }

        public void Configure(TerrainGenerator source, GameObject prefab, int count, float radius, float height)
        {
            if (isActiveAndEnabled && generator != null)
                Unsubscribe();

            Clear();
            generator = source;
            propPrefab = prefab;
            if (radius <= 0f || height <= 0f || !float.IsFinite(radius) || !float.IsFinite(height))
                throw new ArgumentOutOfRangeException(nameof(radius));
            propsPerPiece = Mathf.Clamp(count, 0, 4);
            placementRadius = radius;
            placementHeight = height;
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
                // 계단 전역은 예약하고, 기존 큰 타일은 연결점-중앙 통로도 보존합니다.
                if (piece.Kind == TerrainPieceKind.Stairs) continue;
                var micro = piece.GetComponent<MicroTerrainTile>();
                if (micro != null)
                {
                    // 3m 평지/절벽에는 중앙 상자를 최대 하나만 확률적으로 배치합니다.
                    if (propsPerPiece == 0 || random.NextDouble() >= 0.07) continue;
                    CandidateChecks++;
                    // 필드 Terrain 표면이 타일 높이보다 높을 수 있으므로 최고 높이(2m) 위에서 내려 쏩니다.
                    Vector3 center = piece.transform.TransformPoint(new Vector3(0f, Mathf.Max(micro.Level, 2) + 1f, 0f));
                    if (!TryFloor(piece, center, out center) || !ClearSpace(center)) continue;

                    GameObject crate = Instantiate(propPrefab, generatedRoot.transform);
                    crate.transform.SetPositionAndRotation(center, piece.transform.rotation);
                    crate.SetActive(true);
                    positions.Add(center);
                    Physics.SyncTransforms();
                    continue;
                }

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
                    if (!TryFloor(piece, point, out point) || !PreservesPassages(piece, point, placementRadius, area.size.x <= 3.01f ? 1f : 2f)
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
            return PreservesPassages(piece, point, PropRadius, 2f);
        }

        private static bool PreservesPassages(TerrainPiece piece, Vector3 point, float radius, float centerClearance)
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
                if (Vector3.Distance(point, center + delta * t) < connection.Width * 0.5f + radius + 0.1f)
                    return false;
            }
            return Vector3.Distance(point, center) >= centerClearance;
        }

        private bool TryFloor(TerrainPiece piece, Vector3 origin, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)
                || !FieldGround.BelongsTo(hit.collider, hit.point, piece) || hit.normal.y < 0.99f) return false;
            point = hit.point;

            // 네 모서리 바닥을 확인하여 낭떠러지·작은 받침·높이 차에 걸친 배치를 막습니다.
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 sample = point + new Vector3(x * placementRadius, 0.5f, z * placementRadius);
                if (!Physics.Raycast(sample, Vector3.down, out RaycastHit support, 0.55f, ~0, QueryTriggerInteraction.Ignore)
                    || !FieldGround.BelongsTo(support.collider, support.point, piece) || support.normal.y < 0.99f
                    || Mathf.Abs(support.point.y - point.y) > 0.02f) return false;
            }
            return true;
        }

        private void ValidatePrefab()
        {
            if (transform.lossyScale != Vector3.one || propPrefab.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("소품과 배치기 루트 배율은 1이어야 합니다.");

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
                if (Mathf.Abs(local.x) > placementRadius || Mathf.Abs(local.z) > placementRadius
                    || local.y < -0.001f || local.y > placementHeight + 0.001f)
                    throw new InvalidOperationException("소품 외형/충돌체는 지정한 반경·높이의 예약 영역 안에 있어야 합니다.");
            }
        }

        private bool ClearSpace(Vector3 point)
        {
            return !Physics.CheckBox(point + Vector3.up * (placementHeight * 0.5f + 0.05f),
                new Vector3(placementRadius, placementHeight * 0.5f, placementRadius), Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore);
        }

        public int RemoveNear(Vector3 point, float distance, int maximum = int.MaxValue)
        {
            if (generatedRoot == null) return 0;
            int removed = 0;
            for (int index = generatedRoot.transform.childCount - 1; index >= 0; index--)
            {
                GameObject prop = generatedRoot.transform.GetChild(index).gameObject;
                if (!prop.activeSelf || Vector3.Distance(prop.transform.position, point) > distance) continue;
                positions.Remove(prop.transform.position);
                prop.SetActive(false);
                if (Application.isPlaying) Destroy(prop);
                else DestroyImmediate(prop);
                removed++;
                if (removed >= maximum) break;
            }
            return removed;
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
