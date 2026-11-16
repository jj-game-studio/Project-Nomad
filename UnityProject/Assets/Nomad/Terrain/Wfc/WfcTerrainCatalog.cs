using System;
using UnityEngine;

namespace Nomad.Terrain
{
    [Serializable]
    public sealed class WfcTileVariant
    {
        public TerrainPiece prefab;
        public int combination;
        public int turn;
        public bool stairs;
        public bool wide;
        public bool micro;
        public bool cliff;
        public int level;
        public int[] cornerLevels = new int[4];
        public int[] edgeSlope = new int[80];
        public float weight = 1f;
        // 방향 N/E/S/W × 변의 -3.5/0/3.5m. 높이는 0.25m 단위, -99는 바닥 없음입니다.
        public int[] surfaces = new int[12];
        public int[] required = new int[12];
        public int[] edgeHeights = new int[80];
        // 비어 있는 각 연결 영역이 닿는 원형 외곽 변의 비트입니다. 실제 맵 외곽에 닿아야 합니다.
        public int[] voidBoundaries = Array.Empty<int>();
    }

    [CreateAssetMenu(menuName = "Nomad/Terrain/WFC Catalog")]
    public sealed class WfcTerrainCatalog : ScriptableObject
    {
        public float cellSize = 10f;
        public bool microTiles;
        public WfcTileVariant[] variants = Array.Empty<WfcTileVariant>();
    }

    public static class WfcTileRules
    {
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static readonly Vector3[] Outward = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public static int Slot(Vector3 position, Vector3 outward)
        {
            int side = outward.z > 0.5f ? 0 : outward.x > 0.5f ? 1 : outward.z < -0.5f ? 2 : 3;
            float coordinate = side % 2 == 0 ? position.x : position.z;
            return side * 3 + (coordinate < -1f ? 0 : coordinate > 1f ? 2 : 1);
        }

        public static Vector3 EdgePoint(int side, int lane)
        {
            float value = (lane - 1) * 3.5f;
            return side % 2 == 0 ? new Vector3(value, 0f, side == 0 ? 5f : -5f)
                : new Vector3(side == 1 ? 5f : -5f, 0f, value);
        }

        public static bool Compatible(WfcTileVariant a, WfcTileVariant b, int side)
        {
            if (a.micro && b.micro) return MicroTerrainRules.Compatible(a, b, side);
            if (a.stairs && b.stairs && !a.wide && !b.wide) return false;
            int opposite = (side + 2) % 4;
            if (!a.stairs && !b.stairs && a.surfaces[side * 3 + 1] != b.surfaces[opposite * 3 + 1]) return false;
            for (int lane = 0; lane < 3; lane++)
            {
                int from = side * 3 + lane;
                int to = opposite * 3 + lane;
                if (!Supported(a.required[from], b.surfaces[to]) || !Supported(b.required[to], a.surfaces[from]))
                    return false;
            }
            for (int sample = 0; sample < 20; sample++)
            {
                int from = a.edgeHeights[side * 20 + sample];
                int to = b.edgeHeights[opposite * 20 + sample];
                if (from != -99 && to != -99 && Mathf.Abs(from - to) > 1) return false;
            }
            return true;
        }

        private static bool Supported(int required, int surface) => required == -99
            || (surface != -99 && Mathf.Abs(surface - required) <= 1);
    }
}
