using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public static class TerrainPlacement
    {
        public static bool HasPlanarBranch(IReadOnlyList<TerrainPiece> pieces, IReadOnlyList<TerrainLink> links)
        {
            if (pieces.Count < 4)
                return false;

            var extent = new Bounds(pieces[0].transform.position, Vector3.zero);
            var degrees = new Dictionary<TerrainPiece, int>();
            foreach (TerrainPiece piece in pieces)
            {
                extent.Encapsulate(piece.transform.position);
                degrees.Add(piece, 0);
            }

            bool branch = false;
            foreach (TerrainLink link in links)
            {
                TerrainPiece from = link.From.GetComponentInParent<TerrainPiece>(true);
                TerrainPiece to = link.To.GetComponentInParent<TerrainPiece>(true);
                degrees[from]++;
                degrees[to]++;
                branch |= degrees[from] >= 3 || degrees[to] >= 3;
            }

            return extent.size.x >= 9.99f && extent.size.z >= 9.99f && branch;
        }

        public static void Align(TerrainPiece piece, TerrainConnection incoming, TerrainConnection target)
        {
            Quaternion facing = Quaternion.LookRotation(-target.Outward, target.transform.up);
            piece.transform.rotation = facing * Quaternion.Inverse(incoming.transform.rotation)
                * piece.transform.rotation;
            piece.transform.position += target.Position - incoming.Position;
        }

        public static bool Overlaps(TerrainPiece a, TerrainPiece b,
            TerrainConnection seamA = null, TerrainConnection seamB = null)
        {
            Bounds left = a.WorldOccupancy;
            Bounds right = b.WorldOccupancy;
            Vector3 min = Vector3.Max(left.min, right.min);
            Vector3 max = Vector3.Min(left.max, right.max);

            // 회전의 부동소수점 잔차만 허용합니다. 1cm 위치 오차를 관통 허용량으로 쓰지 않습니다.
            Vector3 size = max - min;
            if (size.x <= 0.00001f || size.y <= 0.00001f || size.z <= 0.00001f)
                return false;

            if (seamA == null || seamB == null || !seamA.Matches(seamB))
                return true;

            // 겹침 전체가 양쪽에서 지정한 좁은 연결 영역에 포함되어야 합니다.
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                if (!seamA.LocalSeamBounds.Contains(seamA.transform.InverseTransformPoint(point))
                    || !seamB.LocalSeamBounds.Contains(seamB.transform.InverseTransformPoint(point)))
                    return true;
            }

            return false;
        }
    }
}
