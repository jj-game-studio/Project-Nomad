using Nomad.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nomad.Tests
{
    public sealed class WfcTerrainTests
    {
        private const string CatalogPath = "Assets/Nomad/Terrain/Wfc/TerrainCatalog.asset";
        private const string MicroCatalogPath = "Assets/Nomad/Terrain/Micro/MicroCatalog.asset";

        private static WfcTerrainCatalog Load(string path)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(path);
            Assert.That(catalog, Is.Not.Null, path);
            Assert.That(catalog.variants, Is.Not.Empty, path);
            return catalog;
        }

        private static bool Solve(WfcTileVariant[] variants, int width, int height, int seed, out int[] result,
            System.Func<int, WfcTileVariant, bool> filter = null)
        {
            int checks = 0;
            return new WfcTerrainSolver(variants).Solve(width, height, new System.Random(seed), out result, ref checks, filter);
        }

        // 모든 인접 셀 쌍이 타일 규칙상 호환되는지 확인합니다.
        private static void AssertNeighboursCompatible(WfcTileVariant[] variants, int[] result, int width, int height)
        {
            for (int cell = 0; cell < result.Length; cell++)
            {
                for (int side = 0; side < 4; side++)
                {
                    int neighbour = WfcTerrainSolver.Neighbour(cell, side, width, height);
                    if (neighbour < 0)
                        continue;

                    Assert.That(WfcTileRules.Compatible(variants[result[cell]], variants[result[neighbour]], side), Is.True,
                        $"셀 {cell}과 {neighbour}(방향 {side})가 호환되지 않습니다.");
                }
            }
        }

        // 방향은 N(+z)/E(+x)/S/W 순서이고 셀 번호는 z * width + x입니다.
        [TestCase(0, 0, 3)]
        [TestCase(0, 1, 1)]
        [TestCase(0, 2, -1)]
        [TestCase(0, 3, -1)]
        [TestCase(4, 0, 7)]
        [TestCase(4, 1, 5)]
        [TestCase(4, 2, 1)]
        [TestCase(4, 3, 3)]
        [TestCase(8, 0, -1)]
        [TestCase(8, 1, -1)]
        public void NeighbourOnThreeByThreeGrid(int cell, int side, int expected)
        {
            Assert.That(WfcTerrainSolver.Neighbour(cell, side, 3, 3), Is.EqualTo(expected));
        }

        [Test]
        public void EdgePointMapsBackToSameSlot()
        {
            for (int side = 0; side < 4; side++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    Vector3 point = WfcTileRules.EdgePoint(side, lane);
                    Assert.That(WfcTileRules.Slot(point, WfcTileRules.Outward[side]), Is.EqualTo(side * 3 + lane));
                }
            }
        }

        [Test]
        public void FootprintsOverlapIgnoresTouchingEdges()
        {
            var a = new Bounds(Vector3.zero, new Vector3(10, 1, 10));

            Assert.That(TerrainGenerator.FootprintsOverlap(a, new Bounds(new Vector3(5, 0, 0), new Vector3(10, 1, 10))), Is.True);
            Assert.That(TerrainGenerator.FootprintsOverlap(a, new Bounds(new Vector3(10, 0, 0), new Vector3(10, 1, 10))), Is.False);
            Assert.That(TerrainGenerator.FootprintsOverlap(a, new Bounds(new Vector3(0, 50, 0), new Vector3(10, 1, 10))), Is.True);
        }

        [Test]
        public void CatalogSolveIsDeterministicAndCompatible()
        {
            WfcTileVariant[] variants = Load(CatalogPath).variants;
            const int width = 6;
            const int height = 6;
            int solved = 0;

            for (int seed = 1; seed <= 10; seed++)
            {
                bool first = Solve(variants, width, height, seed, out int[] result);
                bool second = Solve(variants, width, height, seed, out int[] again);
                Assert.That(second, Is.EqualTo(first), $"seed {seed} 재현성");
                if (!first)
                    continue;

                solved++;
                Assert.That(again, Is.EqualTo(result), $"seed {seed} 재현성");
                AssertNeighboursCompatible(variants, result, width, height);

                // 기본 필터는 중앙 셀을 계단으로 고정하고 0번 셀은 평지 계단 없음으로 제한합니다.
                Assert.That(variants[result[height / 2 * width + width / 2]].stairs, Is.True, $"seed {seed} 중앙 계단");
                Assert.That(variants[result[0]].stairs, Is.False, $"seed {seed} 시작 셀");
            }

            // 모순은 생성기가 재시도하지만 대부분의 seed는 한 번에 풀려야 합니다.
            Assert.That(solved, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void MicroPlanSolveRespectsPlanAndRules()
        {
            WfcTileVariant[] variants = Load(MicroCatalogPath).variants;
            const int width = 12;
            const int height = 12;
            int solved = 0;

            for (int seed = 1; seed <= 10; seed++)
            {
                // 생성기와 같이 하나의 난수열로 계획과 풀이를 이어서 만듭니다.
                var random = new System.Random(seed);
                var plan = new MicroTerrainPlan(width, height, random);
                int checks = 0;
                if (!new WfcTerrainSolver(variants).Solve(width, height, random, out int[] result, ref checks, plan.Allows))
                    continue;

                solved++;
                AssertNeighboursCompatible(variants, result, width, height);
                for (int cell = 0; cell < result.Length; cell++)
                    Assert.That(plan.Allows(cell, variants[result[cell]]), Is.True, $"seed {seed} 셀 {cell} 계획 위반");
            }

            Assert.That(solved, Is.GreaterThanOrEqualTo(5));
        }
    }
}
