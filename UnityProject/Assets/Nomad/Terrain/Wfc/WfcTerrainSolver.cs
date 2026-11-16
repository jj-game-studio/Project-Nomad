using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    // 셀 후보를 전파하고 후보 수가 가장 적은 셀을 하나로 붕괴합니다. 모순은 호출자가 유한 재시도합니다.
    public sealed class WfcTerrainSolver
    {
        private readonly WfcTileVariant[] variants;
        private readonly bool[][] compatible = new bool[4][];

        public WfcTerrainSolver(WfcTileVariant[] choices)
        {
            variants = choices;
            for (int side = 0; side < 4; side++)
            {
                compatible[side] = new bool[choices.Length * choices.Length];
                for (int a = 0; a < choices.Length; a++)
                for (int b = 0; b < choices.Length; b++)
                    compatible[side][a * choices.Length + b] = WfcTileRules.Compatible(choices[a], choices[b], side);
            }
        }

        public bool Solve(int width, int height, System.Random random, out int[] result, ref int checks, Func<int, WfcTileVariant, bool> filter = null)
        {
            result = null;
            var domains = new List<int>[width * height];
            var queue = new Queue<int>();
            for (int cell = 0; cell < domains.Length; cell++)
            {
                domains[cell] = new List<int>();
                for (int option = 0; option < variants.Length; option++)
                {
                    WfcTileVariant tile = variants[option];
                    bool valid = filter != null ? filter(cell, tile) : cell != 0 || (!tile.stairs && tile.surfaces[1] == 0);
                    // 중앙 셀은 계단으로 제한해 평지만 생성되는 검토 결과를 방지합니다.
                    if (filter == null && width >= 3 && height >= 3 && cell == height / 2 * width + width / 2)
                        valid &= tile.stairs;
                    int boundary = 0;
                    for (int side = 0; side < 4; side++)
                    {
                        if (Neighbour(cell, side, width, height) >= 0) continue;
                        boundary |= 1 << side;
                        for (int lane = 0; lane < 3; lane++)
                            valid &= tile.required[side * 3 + lane] == -99;
                    }
                    foreach (int region in tile.voidBoundaries) valid &= (region & boundary) != 0;
                    if (valid) domains[cell].Add(option);
                }
                if (domains[cell].Count == 0) return false;
                queue.Enqueue(cell);
            }

            while (true)
            {
                if (!Propagate(domains, queue, width, height, ref checks)) return false;
                int selected = -1;
                int count = int.MaxValue;
                int ties = 0;
                for (int cell = 0; cell < domains.Length; cell++)
                {
                    int size = domains[cell].Count;
                    if (size <= 1 || size > count) continue;
                    if (size < count) { count = size; ties = 0; }
                    if (random.Next(++ties) == 0) selected = cell;
                }
                if (selected < 0) break;

                float total = 0f;
                foreach (int option in domains[selected]) total += variants[option].weight;
                double choice = random.NextDouble() * total;
                int picked = domains[selected][0];
                foreach (int option in domains[selected])
                {
                    choice -= variants[option].weight;
                    if (choice > 0f) continue;
                    picked = option;
                    break;
                }
                domains[selected].Clear();
                domains[selected].Add(picked);
                queue.Enqueue(selected);
            }

            result = new int[domains.Length];
            for (int cell = 0; cell < domains.Length; cell++) result[cell] = domains[cell][0];
            return true;
        }

        private bool Propagate(List<int>[] domains, Queue<int> queue, int width, int height, ref int checks)
        {
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int side = 0; side < 4; side++)
                {
                    int neighbour = Neighbour(cell, side, width, height);
                    if (neighbour < 0) continue;
                    List<int> target = domains[neighbour];
                    bool changed = false;
                    for (int index = target.Count - 1; index >= 0; index--)
                    {
                        bool supported = false;
                        foreach (int option in domains[cell])
                        {
                            if (++checks > 25000000) return false;
                            if (!compatible[side][option * variants.Length + target[index]]) continue;
                            supported = true;
                            break;
                        }
                        if (supported) continue;
                        target.RemoveAt(index);
                        changed = true;
                    }
                    if (target.Count == 0) return false;
                    if (changed) queue.Enqueue(neighbour);
                }
            }
            return true;
        }

        public static int Neighbour(int cell, int side, int width, int height)
        {
            Vector2Int point = new Vector2Int(cell % width, cell / width) + WfcTileRules.Directions[side];
            return point.x < 0 || point.y < 0 || point.x >= width || point.y >= height ? -1 : point.y * width + point.x;
        }
    }
}
