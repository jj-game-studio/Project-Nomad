using UnityEngine;

namespace Nomad.Terrain
{
    public enum MicroTerrainShape { Flat, Stair, ConvexCorner, ConcaveCorner, Cliff }

    public sealed class MicroTerrainTile : MonoBehaviour
    {
        public const float Size = 3f;
        [SerializeField] private MicroTerrainShape shape;
        [SerializeField] private int level;
        public MicroTerrainShape Shape => shape;
        public int Level => level;
        public bool IsStair => shape == MicroTerrainShape.Stair || shape == MicroTerrainShape.ConvexCorner || shape == MicroTerrainShape.ConcaveCorner;
        public bool IsCliff => shape == MicroTerrainShape.Cliff;

        private void Awake() => KingdomTerrainVisuals.Apply(this);

        public void Configure(MicroTerrainShape kind, int baseLevel)
        {
            shape = kind;
            level = baseLevel;
        }

        public float ContinuousHeight(float x, float z)
        {
            if (!IsStair) return level;
            float a = Mathf.Clamp01((x + 1.5f) / Size);
            float b = Mathf.Clamp01((z + 1.5f) / Size);
            return level + (shape == MicroTerrainShape.Stair ? b
                : shape == MicroTerrainShape.ConvexCorner ? Mathf.Min(a, b) : Mathf.Max(a, b));
        }

        public float HeightAt(float x, float z)
        {
            if (!IsStair) return level;
            float value = ContinuousHeight(x, z) - level;
            return level + Mathf.Clamp(Mathf.FloorToInt(value * 4f + 0.0001f) + 1, 1, 4) * 0.25f;
        }
    }

    public static class MicroTerrainRules
    {
        public static bool Compatible(WfcTileVariant a, WfcTileVariant b, int side)
        {
            int opposite = (side + 2) % 4;
            bool cliff = a.cliff || b.cliff;
            for (int sample = 0; sample < 20; sample++)
            {
                int from = side * 20 + sample;
                int to = opposite * 20 + sample;
                if (a.required[side * 3 + 1] != -99 && Mathf.Abs(a.required[side * 3 + 1] - b.edgeHeights[to]) > 1) return false;
                if (b.required[opposite * 3 + 1] != -99 && Mathf.Abs(b.required[opposite * 3 + 1] - a.edgeHeights[from]) > 1) return false;
                if (!cliff && Mathf.Abs(a.edgeHeights[from] - b.edgeHeights[to]) > 1) return false;
                // 공유 변 양쪽이 모두 변을 향해 오르거나 내려가면 ^ 또는 V가 됩니다.
                if (a.stairs && b.stairs && a.edgeSlope[from] * b.edgeSlope[to] > 0) return false;
            }
            return true;
        }
    }
}
