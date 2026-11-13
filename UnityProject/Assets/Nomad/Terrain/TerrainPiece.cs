using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public enum TerrainPieceKind { Flat, Stairs, Raised }

    [DisallowMultipleComponent]
    public sealed class TerrainPiece : MonoBehaviour
    {
        [SerializeField] private TerrainPieceKind kind;
        [SerializeField] private Bounds occupancy = new Bounds(Vector3.up, new Vector3(10f, 2f, 10f));
        [SerializeField] private TerrainConnection[] connections;

        public TerrainPieceKind Kind => kind;
        public Bounds LocalOccupancy => occupancy;
        public IReadOnlyList<TerrainConnection> Connections => connections;

        // 규격의 90도 단위 회전에서는 월드 AABB로 점유 영역을 표현할 수 있습니다.
        public Bounds WorldOccupancy
        {
            get
            {
                Vector3 size = occupancy.size;
                Vector3 worldSize = Abs(transform.TransformVector(Vector3.right * size.x))
                    + Abs(transform.TransformVector(Vector3.up * size.y))
                    + Abs(transform.TransformVector(Vector3.forward * size.z));

                return new Bounds(transform.TransformPoint(occupancy.center), worldSize);
            }
        }

        public void Configure(TerrainPieceKind pieceKind, Bounds localOccupancy, TerrainConnection[] points)
        {
            kind = pieceKind;
            occupancy = localOccupancy;
            connections = points;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(occupancy.center, occupancy.size);
        }
    }
}
