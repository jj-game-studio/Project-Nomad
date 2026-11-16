using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Terrain
{
    public static class MicroTerrainWalkability
    {
        public static Vector3 BlockedPoint { get; private set; }
        public static bool Validate(GameObject root, IReadOnlyList<TerrainPiece> pieces, out int samples, out string reason, GameObject extraObstacles = null)
        {
            samples = 0;
            BlockedPoint = pieces[0].transform.position;
            reason = string.Empty;
            var sources = new List<NavMeshBuildSource>();
            Bounds bounds = new Bounds(pieces[0].transform.position, Vector3.one);
            var boxes = new List<BoxCollider>(root.GetComponentsInChildren<BoxCollider>(true));
            if (extraObstacles != null) boxes.AddRange(extraObstacles.GetComponentsInChildren<BoxCollider>());
            foreach (BoxCollider box in boxes)
            {
                Matrix4x4 matrix = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = box.size,
                    transform = matrix, area = (box.GetComponentInParent<MicroTerrainWalls>(true) != null || !box.transform.IsChildOf(root.transform)) ? 1 : 0 });
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(Vector3.Scale(box.size, new Vector3(x, y, z)) * 0.5f));
            }
            bounds.Expand(new Vector3(2f, 4f, 2f));
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) { reason = "NavMesh 생성 실패"; return false; }
            NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
            try
            {
                Vector3 start = pieces[0].transform.position;
                if (!NavMesh.SamplePosition(start, out NavMeshHit origin, 0.4f, NavMesh.AllAreas))
                { reason = "시작점의 보행 영역 없음"; return false; }
                var path = new NavMeshPath();
                foreach (TerrainPiece piece in pieces)
                {
                    var profile = piece.GetComponent<MicroTerrainTile>();
                    foreach (float x in new[] { -0.75f, 0.03f, 0.75f })
                    foreach (float z in new[] { -0.75f, 0.07f, 0.75f })
                    {
                        Vector3 point = piece.transform.TransformPoint(new Vector3(x, profile.HeightAt(x, z), z));
                        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)
                            || Mathf.Abs(hit.position.y - point.y) > 0.3f
                            || !NavMesh.CalculatePath(origin.position, hit.position, NavMesh.AllAreas, path)
                            || path.status != NavMeshPathStatus.PathComplete)
                        { BlockedPoint = point; reason = "도달할 수 없는 표면: " + piece.name + " / " + point; return false; }
                        samples++;
                    }
                }
                return true;
            }
            finally
            {
                instance.Remove();
                if (Application.isPlaying) Object.Destroy(data);
                else Object.DestroyImmediate(data);
            }
        }
    }
}
