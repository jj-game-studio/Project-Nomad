using UnityEngine;

namespace Nomad.Terrain
{
    // seed별로 굽이치는 높이 경계를 WFC의 전역 제약으로 사용합니다.
    public sealed class MicroTerrainPlan
    {
        private readonly int[][] corners;
        private readonly int[] cliffs;

        public MicroTerrainPlan(int width, int height, System.Random random)
        {
            bool transpose = random.Next(2) == 0;
            int across = transpose ? height : width;
            int along = transpose ? width : height;
            bool inverted = random.Next(2) == 0;
            var vertices = new int[(width + 1) * (height + 1)];
            int first = random.Next(2, across - 6);
            int second = random.Next(first + 4, across - 1);
            int nextBend = random.Next(3, 6);

            for (int row = 0; row <= along; row++)
            {
                // 꺾임 사이에 평지를 남겨 반대 계단과 한 타일짜리 능선을 피합니다.
                if (row == nextBend && row < along - 2)
                {
                    first = Mathf.Clamp(first + (random.Next(2) == 0 ? -2 : 2), 2, across - 6);
                    second = Mathf.Clamp(second + (random.Next(2) == 0 ? -2 : 2), first + 4, across - 2);
                    nextBend += random.Next(3, 6);
                }
                for (int column = 0; column <= across; column++)
                {
                    int level = column < first ? 0 : column < second ? 1 : 2;
                    int x = transpose ? row : column;
                    int z = transpose ? column : row;
                    vertices[z * (width + 1) + x] = inverted ? 2 - level : level;
                }
            }

            corners = new int[width * height][];
            cliffs = new int[width * height];
            for (int cell = 0; cell < corners.Length; cell++)
            {
                int x = cell % width;
                int z = cell / width;
                int Read(int u, int v) => vertices[v * (width + 1) + u];
                int[] values = { Read(x, z), Read(x + 1, z), Read(x + 1, z + 1), Read(x, z + 1) };
                corners[cell] = values;
                cliffs[cell] = -1;
                bool straight = values[0] == values[1] && values[2] == values[3] && values[0] != values[2]
                    || values[0] == values[3] && values[1] == values[2] && values[0] != values[1];
                // 일정 간격의 계단 대신 불규칙한 절벽/계단 구간을 선택합니다.
                if (straight && random.NextDouble() < 0.65)
                    cliffs[cell] = Mathf.Max(Mathf.Max(values[0], values[1]), Mathf.Max(values[2], values[3]));
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
