using UnityEngine;

namespace Nomad.Terrain
{
    // 연결된 높이 골격을 WFC의 전역 제약으로 사용합니다. 절벽 옆에는 계단 우회 구간을 남깁니다.
    public sealed class MicroTerrainPlan
    {
        private readonly int[][] corners;
        private readonly int[] cliffs;

        public MicroTerrainPlan(int width, int height, System.Random random)
        {
            var vertices = new int[(width + 1) * (height + 1)];
            int left = 2 + random.Next(2);
            int right = width - 2 - random.Next(2);
            int bottom = 2 + random.Next(2);
            int top = height - 2 - random.Next(2);
            int margin = Mathf.Min(right - left, top - bottom) >= 10 ? 3 : 2;
            int innerLeft = left + margin;
            int innerRight = right - margin;
            int innerBottom = bottom + margin;
            int innerTop = top - margin;
            int notchCenter = (innerLeft + innerRight) / 2;
            bool notch = innerRight - innerLeft >= 6 && innerTop - innerBottom >= 6;
            for (int z = 0; z <= height; z++)
            for (int x = 0; x <= width; x++)
            {
                int level = x >= left && x <= right && z >= bottom && z <= top ? 1 : 0;
                if (level == 1 && x >= innerLeft && x <= innerRight && z >= innerBottom && z <= innerTop) level = 2;
                if (notch && level == 2 && x >= notchCenter - 1 && x <= notchCenter + 1 && z >= innerTop - 2) level = 1;
                vertices[z * (width + 1) + x] = level;
            }

            int turn = width == height ? random.Next(4) : random.Next(2) * 2;
            int Read(int x, int z)
            {
                int u = turn == 0 ? x : turn == 1 ? z : turn == 2 ? width - x : width - z;
                int v = turn == 0 ? z : turn == 1 ? height - x : turn == 2 ? height - z : x;
                return vertices[v * (width + 1) + u];
            }
            corners = new int[width * height][];
            cliffs = new int[width * height];
            int phase = random.Next(4);
            for (int cell = 0; cell < corners.Length; cell++)
            {
                int x = cell % width;
                int z = cell / width;
                int[] values = { Read(x, z), Read(x + 1, z), Read(x + 1, z + 1), Read(x, z + 1) };
                corners[cell] = values;
                cliffs[cell] = -1;
                bool straight = values[0] == values[1] && values[2] == values[3] && values[0] != values[2]
                    || values[0] == values[3] && values[1] == values[2] && values[0] != values[1];
                if (straight && x != width / 2 && z != height / 2 && (x + z + phase) % 4 != 0)
                    cliffs[cell] = Mathf.Max(Mathf.Max(values[0], values[1]), Mathf.Max(values[2], values[3]));
            }
            // 2m 절벽이0m 바닥과도 직접 맞닿되 기존2m 구역과 연결되도록 한 줄을 연장합니다.
            for (int z = height / 2; z >= 1; z--)
            {
                int high = -1;
                for (int x = 1; x < width - 1; x++)
                    if (System.Array.TrueForAll(corners[z * width + x], value => value == 2)) { high = x; break; }
                if (high < 0) continue;
                int low = high - 1;
                while (low > 0 && !System.Array.TrueForAll(corners[z * width + low], value => value == 0)) low--;
                for (int x = low + 1; x <= high; x++) cliffs[z * width + x] = 2;
                break;
            }
        }

        public bool Allows(int cell, WfcTileVariant tile)
        {
            if (cliffs[cell] >= 0) return tile.cliff && tile.level == cliffs[cell];
            for (int corner = 0; corner < 4; corner++)
                if (tile.cornerLevels[corner] != corners[cell][corner]) return false;
            return true;
        }
    }
}
