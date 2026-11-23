using System;
using System.Collections.Generic;
using System.Linq;
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
        public FieldGenerationResult Prepare(TerrainGenerationResult terrain, GameObject obstacles,
            IReadOnlyList<Vector3> scatteredChestPositions = null)
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
                return PrepareCurrent(terrain, obstacles, scatteredChestPositions);
            }
            finally
            {
                preparing = false;
            }
        }

        private FieldGenerationResult PrepareCurrent(TerrainGenerationResult terrain, GameObject obstacles,
            IReadOnlyList<Vector3> scatteredChestPositions)
        {
            ReleaseMesh();
            var enemies = new List<FieldSpawnPoint>();
            var chests = new List<FieldSpawnPoint>();
            var exits = new List<FieldSpawnPoint>();
            Vector3 start = default;
            Quaternion facing = Quaternion.identity;
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
            if (success && scatteredChestPositions == null)
                success = ReserveChestPoints(terrain, obstacles, start, chests, out reason);
            else if (success)
                AddScatteredChestPoints(start, scatteredChestPositions, chests);

            if (success)
            {
                // 4면 출구를 먼저 정하고, 그중 하나의 안쪽을 실제 시작점으로 옮깁니다.
                // 앞선 검사의 시작점은 연결 확인용 임시 지점이며, 모든 지점이 서로 연결되어 있어 검사 결과는 그대로 유효합니다.
                SelectExits(terrain, start, exits, chests);
                // 출구 안쪽 자리가 모두 막힌 드문 경우(이전 10m 비교 모드 포함)에는 임시 시작점을 그대로 씁니다.
                if (!TryEntrance(terrain.Seed, exits, chests, ref start, out facing))
                    Debug.LogWarning($"출구 안쪽에 안전한 시작점이 없어 기본 시작점을 씁니다. seed={terrain.Seed} exits={exits.Count}", this);

                // 적 지점이 출구·상자 자리를 피하도록 합니다.
                SelectPoints(terrain, start, enemies, false, new List<FieldSpawnPoint>(chests.Concat(exits)));
            }
            if (!success)
            {
                ReleaseMesh();
                enemies.Clear();
                chests.Clear();
                exits.Clear();
            }
            LastResult = new FieldGenerationResult(terrain, success, reason, start, facing, enemies, chests, exits);
            completedId = terrain.GenerationId;
            State = success ? FieldGenerationState.Ready : FieldGenerationState.Failed;
            GenerationCompleted?.Invoke(LastResult);
            return LastResult;
        }

        private bool ReserveChestPoints(TerrainGenerationResult terrain, GameObject obstacles, Vector3 start,
            List<FieldSpawnPoint> chests, out string reason)
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
            return ValidateRoutes(terrain, start, obstacles, chests, out reason);
        }

        private void AddScatteredChestPoints(Vector3 start, IReadOnlyList<Vector3> positions, List<FieldSpawnPoint> chests)
        {
            // 경로에 포함해 검사한 큐브 자리를 사용합니다. 주위에서 상자를 열 수 있는 곳도 확인합니다.
            for (int index = 0; index < positions.Count; index++)
            {
                Vector3 position = positions[index];
                for (int side = 0; side < 8; side++)
                {
                    Vector3 offset = Quaternion.Euler(0f, 45f * side, 0f) * (Vector3.forward * 1.3f);
                    if (!Sample(position + offset, out Vector3 approach) ||
                        Vector3.Distance(approach, position) > 1.85f || !Reachable(start, approach))
                        continue;

                    chests.Add(new FieldSpawnPoint("chest-prop-" + index, position, Quaternion.identity, 0.6f, approach));
                    break;
                }
            }
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

            // 적 후보를 타일 순서대로 고르면 첫 줄 근처에만 몰리므로, seed로 섞은 순서로 필드 전체에서 고릅니다.
            int[] order = Enumerable.Range(0, terrain.Pieces.Count).ToArray();
            if (!chest)
            {
                var random = new System.Random(unchecked(terrain.Seed * 31 + 7));
                for (int index = order.Length - 1; index > 0; index--)
                {
                    int swap = random.Next(index + 1);
                    (order[index], order[swap]) = (order[swap], order[index]);
                }
            }

            foreach (int index in order)
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
                if (target.Count >= (chest ? 8 : MaxEnemyPoints)) break;
            }
        }

        private const int MaxEnemyPoints = 96;
        private const float ExitRadius = 0.75f;
        // 시작점은 출구 트리거(반경 0.75m) 밖, 출구에서 필드 안쪽으로 이 거리만큼 들어온 자리입니다.
        private static readonly float[] EntranceDistances = { 3f, 3.75f, 2.5f, 4.5f };
        // 이웃한 면의 출구가 같은 모서리에 몰리지 않도록 출구끼리 4타일 이상 띄웁니다.
        private const float ExitSpacing = 12f;
        private const int ExitDepth = 2;
        private static readonly string[] ExitSides = { "west", "east", "south", "north" };

        // 4면의 가장자리 근처 타일에 출구를 하나씩 랜덤 배치합니다. start는 도달성 확인에만 씁니다.
        private void SelectExits(TerrainGenerationResult terrain, Vector3 start, List<FieldSpawnPoint> exits,
            List<FieldSpawnPoint> reserved)
        {
            Transform frame = terrain.Pieces[0].transform.parent;
            Vector3[] centers = terrain.Pieces.Select(piece => frame.InverseTransformPoint(piece.WorldOccupancy.center)).ToArray();
            float[] edges = { centers.Min(c => c.x), centers.Max(c => c.x), centers.Min(c => c.z), centers.Max(c => c.z) };

            // 같은 seed·설정에서 같은 출구가 나오도록 지형 seed로 고릅니다.
            var random = new System.Random(terrain.Seed);
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = side switch { 0 => Vector3.left, 1 => Vector3.right, 2 => Vector3.back, _ => Vector3.forward };
                Quaternion rotation = Quaternion.LookRotation(frame.TransformDirection(outward));

                // 가장자리 줄에 유효한 자리가 없으면 한 줄 안쪽까지 대체 후보로 써서 출구 없는 면을 줄입니다.
                bool found = false;
                for (int depth = 0; depth < ExitDepth && !found; depth++)
                {
                    float line = edges[side] + (side % 2 == 0 ? depth : -depth) * MicroTerrainTile.Size;
                    List<int> candidates = Enumerable.Range(0, centers.Length)
                        .Where(index => Mathf.Abs((side < 2 ? centers[index].x : centers[index].z) - line) < 0.1f
                            && terrain.Pieces[index].Kind != TerrainPieceKind.Stairs)
                        .OrderBy(_ => random.Next()).ToList();

                    // Any는 첫 성공에서 멈추므로 섞인 순서의 첫 유효 타일이 출구가 됩니다.
                    found = candidates.Any(index => TryExit(terrain.Pieces[index], start, rotation, ExitSides[side], exits, reserved));
                }
            }
        }

        private bool TryExit(TerrainPiece piece, Vector3 start, Quaternion rotation, string side,
            List<FieldSpawnPoint> exits, List<FieldSpawnPoint> reserved)
        {
            foreach (Vector3 candidate in Candidates(piece))
            {
                if (!TryFloor(piece, candidate, ExitRadius, out Vector3 floor)
                    || Overlaps(floor, ExitRadius, reserved)
                    || exits.Any(other => Vector3.Distance(floor, other.Position) < ExitSpacing)
                    || !Sample(floor, out Vector3 approach) || !Reachable(start, approach)) continue;

                exits.Add(new FieldSpawnPoint("exit-" + side, floor, rotation, ExitRadius, approach));
                return true;
            }
            return false;
        }

        // seed로 고른 출구 하나의 안쪽에 플레이어 시작점을 둡니다. 그 자리가 막혔으면 다음 출구를 시도합니다.
        private bool TryEntrance(int seed, List<FieldSpawnPoint> exits, List<FieldSpawnPoint> reserved,
            ref Vector3 start, out Quaternion facing)
        {
            facing = Quaternion.identity;
            var random = new System.Random(unchecked(seed * 17 + 3));
            foreach (FieldSpawnPoint exit in exits.OrderBy(_ => random.Next()).ToList())
            {
                Vector3 inward = Vector3.ProjectOnPlane(exit.Rotation * Vector3.back, Vector3.up).normalized;
                foreach (float distance in EntranceDistances)
                {
                    Vector3 desired = exit.Position + inward * distance;
                    if (!Physics.Raycast(desired + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                        || hit.normal.y < 0.8f || !Sample(hit.point, out Vector3 candidate) || !Reachable(start, candidate)
                        || Overlaps(candidate, 0.5f, reserved)
                        || Physics.CheckCapsule(candidate + Vector3.up * 0.45f, candidate + Vector3.up * 1.45f, 0.35f, ~0,
                            QueryTriggerInteraction.Ignore))
                        continue;

                    start = candidate;
                    facing = Quaternion.LookRotation(inward);
                    return true;
                }
            }
            return false;
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
