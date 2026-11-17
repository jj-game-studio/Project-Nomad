using UnityEngine;

namespace Nomad.Terrain
{
    // 바닥 판정이 "이 조각의 바닥인가"를 묻는 곳에서 공통으로 사용합니다.
    // 조각 자신의 충돌체뿐 아니라, 같은 생성 루트에 있는 필드 Terrain이 그 조각의 칸을 덮는 경우도 바닥으로 인정합니다.
    // Terrain이 없는 필드에서는 기존 IsChildOf 판정과 결과가 같습니다.
    public static class FieldGround
    {
        public static bool BelongsTo(Collider collider, Vector3 point, TerrainPiece piece)
        {
            if (collider == null || piece == null) return false;
            if (collider.transform.IsChildOf(piece.transform)) return true;

            Transform root = piece.transform.parent;
            if (!(collider is TerrainCollider) || root == null || !collider.transform.IsChildOf(root)) return false;

            // 조각 점유 영역의 가로·세로 안에 든 지점만 그 조각의 바닥으로 봅니다.
            Bounds occupancy = piece.LocalOccupancy;
            Vector3 local = piece.transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x - occupancy.center.x) <= occupancy.extents.x + 0.001f
                && Mathf.Abs(local.z - occupancy.center.z) <= occupancy.extents.z + 0.001f;
        }

        // 조각들이 속한 생성 루트의 필드 Terrain입니다. 없으면 null입니다.
        public static UnityEngine.Terrain FindTerrain(TerrainPiece piece)
        {
            Transform root = piece != null ? piece.transform.parent : null;
            return root != null ? root.GetComponentInChildren<UnityEngine.Terrain>() : null;
        }

        // 필드 Terrain이 있으면 그 표면 높이를, 없으면 타일 기준 높이(fallback)를 돌려줍니다.
        public static float SurfaceHeight(UnityEngine.Terrain field, Vector3 point, float fallback) =>
            field == null ? fallback : field.transform.position.y + field.SampleHeight(point);
    }
}
