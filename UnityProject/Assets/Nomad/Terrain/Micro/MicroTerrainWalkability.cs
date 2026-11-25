using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Terrain
{
    public static class MicroTerrainWalkability
    {
        public static Vector3 BlockedPoint { get; private set; }

        // 필드 생성 시간 측정용 누계입니다. 측정 측이 ResetStats로 초기화합니다.
        public static int BuildCount { get; private set; }
        public static int UpdateCount { get; private set; }
        public static double BuildSeconds { get; private set; }
        public static int PathChecks { get; private set; }
        public static double PathSeconds { get; private set; }

        public static void ResetStats()
        {
            BuildCount = 0;
            UpdateCount = 0;
            BuildSeconds = 0;
            PathChecks = 0;
            PathSeconds = 0;
        }

        // cache를 넘기면 반복 검사 사이에 NavMesh를 유지하고, 바뀐 장애물이 걸친 타일만 다시 굽습니다.
        public static bool Validate(GameObject root, IReadOnlyList<TerrainPiece> pieces, out int samples, out string reason,
            GameObject extraObstacles = null, WalkabilityCache cache = null)
        {
            samples = 0;
            BlockedPoint = pieces[0].transform.position;
            reason = string.Empty;
            NavMeshData data = Build(root, pieces, extraObstacles, null, cache);
            if (data == null) { reason = "NavMesh 생성 실패"; return false; }
            NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
            try
            {
                var first = pieces[0].GetComponent<MicroTerrainTile>();
                Vector3 start = pieces[0].transform.TransformPoint(new Vector3(-1.1f, first.HeightAt(-1.1f, -1.1f), -1.1f));
                start.y = FieldGround.SurfaceHeight(FieldGround.FindTerrain(pieces[0]), start, start.y);
                if (cache == null) return ValidatePaths(pieces, start, out samples, out reason, extraObstacles);

                // 반복 검사에서는 장애물이 줄기만 하므로 앞서 통과한 표본은 그대로 통과합니다.
                // 지난번에 막힌 표본부터 이어서 검사합니다. 최종 경로 보장은 이후 단계의 전체 검사가 맡습니다.
                bool valid = ValidatePaths(pieces, start, out samples, out reason, extraObstacles, null,
                    cache.ResumeSample, out int failed);
                cache.ResumeSample = failed;
                return valid;
            }
            finally
            {
                instance.Remove();
                if (cache == null) Release(data);
            }
        }

        public static NavMeshData Build(GameObject root, IReadOnlyList<TerrainPiece> pieces,
            GameObject extraObstacles = null, IReadOnlyList<FieldSpawnPoint> reservations = null,
            WalkabilityCache cache = null)
        {
            var sources = new List<NavMeshBuildSource>();
            Bounds bounds = new Bounds(pieces[0].transform.position, Vector3.one);
            var boxes = new List<BoxCollider>(root.GetComponentsInChildren<BoxCollider>(true));
            if (extraObstacles != null) boxes.AddRange(extraObstacles.GetComponentsInChildren<BoxCollider>());
            foreach (BoxCollider box in boxes)
            {
                // 필드 Terrain으로 대체해 끈 타일 충돌체는 보행 면에서 제외합니다.
                if (!box.enabled) continue;

                Matrix4x4 matrix = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = box.size,
                    transform = matrix, area = (box.GetComponentInParent<MicroTerrainWalls>(true) != null || !box.transform.IsChildOf(root.transform)) ? 1 : 0 });
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(Vector3.Scale(box.size, new Vector3(x, y, z)) * 0.5f));
            }
            // 생성 루트 아래의 필드 Terrain(경사로 포함)을 보행 면으로 굽습니다. 없으면 기존과 같습니다.
            foreach (UnityEngine.Terrain field in root.GetComponentsInChildren<UnityEngine.Terrain>())
            {
                Vector3 origin = field.transform.position;
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Terrain,
                    sourceObject = field.terrainData, transform = Matrix4x4.Translate(origin), area = 0 });
                bounds.Encapsulate(origin);
                bounds.Encapsulate(origin + field.terrainData.size);
            }

            bounds.Expand(new Vector3(2f, 4f, 2f));
            if (reservations != null)
            {
                foreach (FieldSpawnPoint point in reservations)
                    sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                        size = new Vector3(point.Radius * 2f, 1.5f, point.Radius * 2f),
                        transform = Matrix4x4.TRS(point.Position + Vector3.up * 0.75f, point.Rotation, Vector3.one), area = 1 });
            }
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            NavMeshData data;

            // 같은 영역을 다시 굽는 경우 기존 데이터를 갱신합니다. 원본이 그대로인 타일은 다시 굽지 않습니다.
            if (cache != null && cache.Data != null && cache.Bounds == bounds)
            {
                data = NavMeshBuilder.UpdateNavMeshData(cache.Data, settings, sources, bounds) ? cache.Data : null;
                UpdateCount++;
            }
            else
            {
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                BuildCount++;
                if (cache != null)
                {
                    cache.Dispose();
                    cache.Data = data;
                    cache.Bounds = bounds;
                }
            }

            BuildSeconds += watch.Elapsed.TotalSeconds;
            return data;
        }

        internal static void Release(NavMeshData data)
        {
            if (data == null) return;
            if (Application.isPlaying) Object.Destroy(data);
            else Object.DestroyImmediate(data);
        }

        public static bool ValidatePaths(IReadOnlyList<TerrainPiece> pieces, Vector3 start,
            out int samples, out string reason, GameObject extraObstacles = null,
            IReadOnlyList<FieldSpawnPoint> reservations = null) =>
            ValidatePaths(pieces, start, out samples, out reason, extraObstacles, reservations, 0, out _);

        // resumeFrom 앞의 표본은 경로 계산 없이 통과로 셉니다. failed는 막힌 표본 순번입니다.
        private static bool ValidatePaths(IReadOnlyList<TerrainPiece> pieces, Vector3 start,
            out int samples, out string reason, GameObject extraObstacles,
            IReadOnlyList<FieldSpawnPoint> reservations, int resumeFrom, out int failed)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return ValidatePathsCore(pieces, start, out samples, out reason, extraObstacles, reservations,
                    resumeFrom, out failed);
            }
            finally
            {
                PathChecks++;
                PathSeconds += watch.Elapsed.TotalSeconds;
            }
        }

        private static bool ValidatePathsCore(IReadOnlyList<TerrainPiece> pieces, Vector3 start,
            out int samples, out string reason, GameObject extraObstacles,
            IReadOnlyList<FieldSpawnPoint> reservations, int resumeFrom, out int failed)
        {
            samples = 0;
            failed = 0;
            int index = 0;
            reason = string.Empty;
            var boxes = extraObstacles == null ? new BoxCollider[0] : extraObstacles.GetComponentsInChildren<BoxCollider>();
            if (!NavMesh.SamplePosition(start, out NavMeshHit origin, 0.4f, NavMesh.AllAreas))
            { reason = "시작점의 보행 영역 없음"; return false; }
            var path = new NavMeshPath();
            UnityEngine.Terrain field = pieces.Count > 0 ? FieldGround.FindTerrain(pieces[0]) : null;
            foreach (TerrainPiece piece in pieces)
            {
                var profile = piece.GetComponent<MicroTerrainTile>();
                // 중앙 상자가 있는 평지는 상자 주변 보행 면을 검사합니다.
                float offset = extraObstacles != null && !profile.IsStair ? 1.1f : 0.75f;
                foreach (float x in new[] { -offset, 0.03f, offset })
                foreach (float z in new[] { -offset, 0.07f, offset })
                {
                    if (index++ < resumeFrom) { samples++; continue; }

                    Vector3 point = piece.transform.TransformPoint(new Vector3(x, profile.HeightAt(x, z), z));
                    // 필드 Terrain이 지면이면 표본 높이를 그 표면에서 읽습니다. 없으면 타일 높이 그대로입니다.
                    point.y = FieldGround.SurfaceHeight(field, point, point.y);
                    // 상자 자체가 차지한 중앙은 보행 면이 아닙니다. 주변 8개 지점은 계속 검사합니다.
                    if (extraObstacles != null && !profile.IsStair && Mathf.Abs(x) < 0.1f && Mathf.Abs(z) < 0.1f
                        && System.Array.Exists(boxes, box => box.bounds.Contains(point + Vector3.up * 0.1f))) continue;
                    if (StructureOccupies(point, boxes) || Reserved(point, reservations)) continue;
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)
                        || Mathf.Abs(hit.position.y - point.y) > 0.3f
                        || !NavMesh.CalculatePath(origin.position, hit.position, NavMesh.AllAreas, path)
                        || path.status != NavMeshPathStatus.PathComplete)
                    { BlockedPoint = point; failed = index - 1; reason = "도달할 수 없는 표면: " + piece.name + " / " + point; return false; }
                    samples++;
                }
            }
            return true;
        }

        private static bool StructureOccupies(Vector3 point, BoxCollider[] boxes)
        {
            foreach (BoxCollider box in boxes)
            {
                if (!box.enabled || (box.name != "StructureWall" && box.name != "StructurePillar")) continue;
                Vector3 local = box.transform.InverseTransformPoint(point) - box.center;
                // 벽·기둥과 플레이어 반경이 겹치는 곳은 원래 걸을 수 없는 공간입니다.
                if (Mathf.Abs(local.x) < box.size.x * 0.5f + 0.35f &&
                    Mathf.Abs(local.z) < box.size.z * 0.5f + 0.35f &&
                    Mathf.Abs(local.y) < box.size.y * 0.5f + 0.3f) return true;
            }
            return false;
        }

        private static bool Reserved(Vector3 point, IReadOnlyList<FieldSpawnPoint> reservations)
        {
            if (reservations == null) return false;
            foreach (FieldSpawnPoint slot in reservations)
            {
                Vector3 local = Quaternion.Inverse(slot.Rotation) * (point - slot.Position);
                if (Mathf.Abs(local.y) < 0.3f && Mathf.Abs(local.x) <= slot.Radius && Mathf.Abs(local.z) <= slot.Radius)
                    return true;
            }
            return false;
        }
    }

    // 반복 경로 검사가 같은 NavMesh를 증분 갱신하도록 붙잡아 둡니다. 다 쓰면 Dispose로 정리합니다.
    public sealed class WalkabilityCache : System.IDisposable
    {
        internal NavMeshData Data;
        internal Bounds Bounds;

        // 장애물만 줄어드는 반복 검사에서 이어서 검사할 표본 순번입니다.
        internal int ResumeSample;

        public void Dispose()
        {
            MicroTerrainWalkability.Release(Data);
            Data = null;
        }
    }
}
