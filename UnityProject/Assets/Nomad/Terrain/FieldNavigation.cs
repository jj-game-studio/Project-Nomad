using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Terrain
{
    [DisallowMultipleComponent]
    public sealed class FieldNavigation : MonoBehaviour
    {
        private TerrainGenerator generator;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshPath path;
        private bool preparing;
        private string completedId;

        public FieldGenerationState State { get; private set; }
        public FieldGenerationResult LastResult { get; private set; }
        public bool HasNavigation => instance.valid;
        public event Action<string> GenerationStarted;
        public event Action<FieldGenerationResult> GenerationCompleted;

        public void Configure(TerrainGenerator source)
        {
            if (generator == source) return;
            if (generator != null) generator.GenerationStarted -= Begin;
            Invalidate();
            generator = source;
            if (isActiveAndEnabled && generator != null) generator.GenerationStarted += Begin;
        }

        private void Begin(string id)
        {
            Invalidate();
            State = FieldGenerationState.Generating;
            GenerationStarted?.Invoke(id);
        }

        public void Invalidate()
        {
            ReleaseMesh();
            LastResult = null;
            State = FieldGenerationState.Idle;
        }

        // 호출 측은 소품 배치와 경로 복구를 마친 뒤 최종 결과를 준비합니다.
        public FieldGenerationResult Prepare(TerrainGenerationResult terrain, GameObject obstacles)
        {
            if (preparing) throw new InvalidOperationException("경로 준비에 재진입할 수 없습니다.");
            if (!isActiveAndEnabled) throw new InvalidOperationException("비활성 경로 제공자는 준비할 수 없습니다.");
            if (terrain == null || generator == null || generator.LastResult != terrain)
                throw new ArgumentException("현재 생성기의 최신 지형 결과가 필요합니다.");
            if (LastResult?.GenerationId == terrain.GenerationId) return LastResult;
            if (completedId == terrain.GenerationId)
                throw new InvalidOperationException("무효화한 세대는 다시 준비할 수 없습니다. 지형을 새로 생성하십시오.");

            preparing = true;
            try
            {
                return PrepareCurrent(terrain, obstacles);
            }
            finally
            {
                preparing = false;
            }
        }

        private FieldGenerationResult PrepareCurrent(TerrainGenerationResult terrain, GameObject obstacles)
        {
            ReleaseMesh();
            var enemies = new List<FieldSpawnPoint>();
            var chests = new List<FieldSpawnPoint>();
            Vector3 start = default;
            string reason = terrain.FailureReason;
            bool success = terrain.Success && terrain.Pieces.Count > 0;
            Physics.SyncTransforms();

            if (success)
            {
                Bake(terrain, obstacles, chests);
                success = HasNavigation && TryStart(terrain.Pieces, out start);
                reason = success ? string.Empty : "안전한 플레이어 시작점 또는 NavMesh가 없습니다.";
            }
            if (success) success = ValidateRoutes(terrain, start, obstacles, chests, out reason);
            if (success)
            {
                SelectPoints(terrain, start, chests, true);
                while (chests.Count > 0)
                {
                    // 상자를 모두 놓아도 필수 경로가 유지되는 예약 영역만 공개합니다.
                    Bake(terrain, obstacles, chests);
                    if (ValidateRoutes(terrain, start, obstacles, chests, out _) && ReachAll(start, chests)) break;
                    int nearest = 0;
                    for (int index = 1; index < chests.Count; index++)
                        if (Vector3.Distance(chests[index].Position, MicroTerrainWalkability.BlockedPoint)
                            < Vector3.Distance(chests[nearest].Position, MicroTerrainWalkability.BlockedPoint)) nearest = index;
                    chests.RemoveAt(nearest);
                }
                if (chests.Count == 0) Bake(terrain, obstacles, chests);
                success = HasNavigation && ValidateRoutes(terrain, start, obstacles, chests, out reason);
                if (success) SelectPoints(terrain, start, enemies, false, chests);
            }
            if (!success)
            {
                ReleaseMesh();
                enemies.Clear();
                chests.Clear();
            }
            LastResult = new FieldGenerationResult(terrain, success, reason, start, enemies, chests);
            completedId = terrain.GenerationId;
            State = success ? FieldGenerationState.Ready : FieldGenerationState.Failed;
            GenerationCompleted?.Invoke(LastResult);
            return LastResult;
        }

        private void Bake(TerrainGenerationResult terrain, GameObject obstacles, List<FieldSpawnPoint> chests)
        {
            ReleaseMesh();
            data = MicroTerrainWalkability.Build(terrain.Pieces[0].transform.parent.gameObject,
                terrain.Pieces, obstacles, chests);
            if (data != null) instance = NavMesh.AddNavMeshData(data);
        }

        private bool TryStart(IReadOnlyList<TerrainPiece> pieces, out Vector3 start)
        {
            foreach (TerrainPiece piece in pieces)
            {
                if (piece.Kind == TerrainPieceKind.Stairs) continue;
                foreach (Vector3 candidate in Candidates(piece))
                {
                    if (TryFloor(piece, candidate, 0.35f, out Vector3 floor)
                        && Sample(floor, out start)) return true;
                }
            }
            start = default;
            return false;
        }

        private void SelectPoints(TerrainGenerationResult terrain, Vector3 start, List<FieldSpawnPoint> target,
            bool chest, List<FieldSpawnPoint> other = null)
        {
            float radius = chest ? 0.75f : 0.5f;
            for (int index = 0; index < terrain.Pieces.Count; index++)
            {
                TerrainPiece piece = terrain.Pieces[index];
                if (piece.Kind == TerrainPieceKind.Stairs) continue;
                foreach (Vector3 candidate in Candidates(piece))
                {
                    if (!TryFloor(piece, candidate, radius, out Vector3 floor)
                        || Vector3.Distance(floor, start) < (chest ? 2f : 5f)
                        || Overlaps(floor, radius, target) || Overlaps(floor, radius, other)) continue;
                    if (chest && !PreservesConnections(piece, floor, radius)) continue;
                    if (chest && (!NavMesh.FindClosestEdge(floor, out NavMeshHit edge, NavMesh.AllAreas)
                        || edge.distance < 2f)) continue;

                    Vector3 approach = floor;
                    bool valid = false;
                    if (chest)
                    {
                        foreach (Vector3 direction in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                        {
                            Vector3 desired = floor + direction * 1.3f;
                            if (Sample(desired, out approach) && Vector3.Distance(approach, floor) <= 2f
                                && Reachable(start, approach)) { valid = true; break; }
                        }
                    }
                    else valid = Sample(floor, out approach) && Reachable(start, approach)
                        && TryFloor(piece, approach + Vector3.up * 2f, radius, out _);
                    if (!valid) continue;

                    target.Add(new FieldSpawnPoint((chest ? "chest-" : "enemy-") + index,
                        chest ? floor : approach, piece.transform.rotation, radius, approach));
                    break;
                }
                // 후보·수량을 제한하여 큰 필드에서도 유한한 작업량을 유지합니다.
                if (target.Count >= (chest ? 8 : 64)) break;
            }
        }

        private static IEnumerable<Vector3> Candidates(TerrainPiece piece)
        {
            Bounds bounds = piece.LocalOccupancy;
            foreach (Vector2 offset in new[] { Vector2.zero, new Vector2(-0.6f, -0.6f),
                new Vector2(0.6f, -0.6f), new Vector2(-0.6f, 0.6f), new Vector2(0.6f, 0.6f) })
                yield return piece.transform.TransformPoint(new Vector3(bounds.center.x + offset.x,
                    bounds.max.y + 2f, bounds.center.z + offset.y));
        }

        private static bool TryFloor(TerrainPiece piece, Vector3 origin, float radius, out Vector3 floor)
        {
            floor = default;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                || !FieldGround.BelongsTo(hit.collider, hit.point, piece) || hit.normal.y < 0.99f) return false;
            floor = hit.point;
            foreach (Vector2 offset in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f),
                new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Vector3 sample = floor + new Vector3(offset.x * radius, 0.5f, offset.y * radius);
                if (!Physics.Raycast(sample, Vector3.down, out RaycastHit support, 0.55f, ~0, QueryTriggerInteraction.Ignore)
                    || !FieldGround.BelongsTo(support.collider, support.point, piece) || support.normal.y < 0.99f
                    || Mathf.Abs(support.point.y - floor.y) > 0.02f) return false;
            }
            return !Physics.CheckCapsule(floor + Vector3.up * (radius + 0.05f),
                floor + Vector3.up * (1.85f - radius), radius, ~0, QueryTriggerInteraction.Ignore);
        }

        private static bool PreservesConnections(TerrainPiece piece, Vector3 floor, float radius)
        {
            // 3m 타일은 통합 씬의 실제 표면 우회 검사를 최종 기준으로 사용합니다.
            if (piece.GetComponent<MicroTerrainTile>() != null) return true;
            // 연결부의 통로 폭을 예약하여 계단 출구에 상자를 놓지 않습니다.
            foreach (TerrainConnection connection in piece.Connections)
            {
                Vector3 delta = floor - connection.Position;
                if (Mathf.Abs(delta.y) > 0.3f) continue;
                if (new Vector2(delta.x, delta.z).magnitude < connection.Width * 0.5f + radius + 0.1f)
                    return false;
            }
            return true;
        }

        private static bool Overlaps(Vector3 point, float radius, List<FieldSpawnPoint> points)
        {
            if (points == null) return false;
            foreach (FieldSpawnPoint other in points)
            {
                Vector3 delta = point - other.Position;
                if (Mathf.Abs(delta.y) < 1.8f && new Vector2(delta.x, delta.z).magnitude < radius + other.Radius + 0.1f)
                    return true;
            }
            return false;
        }

        private bool ValidateRoutes(TerrainGenerationResult terrain, Vector3 start, GameObject obstacles,
            List<FieldSpawnPoint> chests, out string reason)
        {
            reason = string.Empty;
            if (!HasNavigation) { reason = "NavMesh 생성 실패"; return false; }
            if (terrain.Pieces[0].GetComponent<MicroTerrainTile>() != null
                && !MicroTerrainWalkability.ValidatePaths(terrain.Pieces, start, out _, out reason, obstacles, chests))
                return false;
            foreach (TerrainLink link in terrain.Links)
            {
                foreach (TerrainConnection connection in new[] { link.From, link.To })
                {
                    Vector3 inside = connection.Position - connection.transform.forward * 0.45f;
                    // 계단의 포트 바닥과 첫 단 높이는 다르므로 실제 표면에 투영합니다.
                    // 필드 Terrain 표면은 타일 연결점 높이와 2m까지 다를 수 있어 아래로 넉넉히 내려 쏩니다.
                    if (!Physics.Raycast(inside + Vector3.up * 3f, Vector3.down, out RaycastHit floor,
                        6f, ~0, QueryTriggerInteraction.Ignore)
                        || !FieldGround.BelongsTo(floor.collider, floor.point, connection.GetComponentInParent<TerrainPiece>())
                        || !Sample(floor.point, out Vector3 hit) || !Reachable(start, hit))
                    { reason = $"도달할 수 없는 필수 연결부: {connection.transform.parent.name}/{connection.name} inside={inside} floor={floor.point}"; return false; }
                }
            }
            return true;
        }

        private static bool Sample(Vector3 point, out Vector3 position)
        {
            bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - point.y) <= 0.3f;
            position = hit.position;
            return found;
        }

        private bool Reachable(Vector3 start, Vector3 point)
        {
            path ??= new NavMeshPath();
            return NavMesh.CalculatePath(start, point, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        private bool ReachAll(Vector3 start, List<FieldSpawnPoint> points)
        {
            foreach (FieldSpawnPoint point in points)
                if (!Sample(point.ApproachPosition, out Vector3 approach) || !Reachable(start, approach)) return false;
            return true;
        }

        private void ReleaseMesh()
        {
            if (instance.valid) instance.Remove();
            instance = default;
            if (data == null) return;
            if (Application.isPlaying) Destroy(data);
            else DestroyImmediate(data);
            data = null;
        }

        private void OnEnable()
        {
            if (generator != null) generator.GenerationStarted += Begin;
        }

        private void OnDisable()
        {
            if (generator != null) generator.GenerationStarted -= Begin;
            Invalidate();
        }
        private void OnDestroy()
        {
            if (generator != null) generator.GenerationStarted -= Begin;
            Invalidate();
        }
    }
}
