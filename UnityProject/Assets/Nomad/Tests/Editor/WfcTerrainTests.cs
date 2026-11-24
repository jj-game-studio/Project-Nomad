using Nomad.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nomad.Tests
{
    public sealed class WfcTerrainTests
    {
        [Test]
        public void BoundaryWallCollider_BlocksAirbornePlayer_WithoutRaisingInteriorWalls()
        {
            var owner = new GameObject("Boundary regression");
            var wall = new GameObject("Outer stone wall");
            var interior = new GameObject("Interior stone wall");
            var player = new GameObject("Jumping player");
            try
            {
                var dressing = owner.AddComponent<FieldDressing>();
                foreach (GameObject target in new[] { wall, interior })
                {
                    GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mesh.transform.SetParent(target.transform, false);
                    mesh.transform.localScale = new Vector3(3f, 0.7f, 0.3f);
                    mesh.transform.localPosition = Vector3.up * 0.35f;
                    Object.DestroyImmediate(mesh.GetComponent<Collider>());
                }

                var add = typeof(FieldDressing).GetMethod("AddBlocker",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                add.Invoke(dressing, new object[] { wall, false, false, false });
                add.Invoke(dressing, new object[] { interior, false, true, false });
                wall.transform.position = new Vector3(1000f, 0f, 1000f);
                interior.transform.position = new Vector3(1010f, 0f, 1000f);
                Physics.SyncTransforms();
                Assert.That(wall.GetComponent<BoxCollider>().bounds.max.y, Is.GreaterThanOrEqualTo(4f));
                Assert.That(interior.GetComponent<BoxCollider>().bounds.max.y, Is.LessThan(1f), "내부 돌담은 원래 높이 유지");

                var body = player.AddComponent<CharacterController>();
                body.height = 1.8f;
                body.radius = 0.35f;
                body.center = Vector3.up * 0.9f;
                body.enabled = false;
                player.transform.position = wall.transform.position + new Vector3(0, 1.6f, -2f);
                body.enabled = true;
                Physics.SyncTransforms();
                for (int step = 0; step < 50; step++) body.Move(Vector3.forward * 0.1f);
                Assert.That(player.transform.position.z, Is.LessThan(wall.transform.position.z), "점프 높이에서 경계 통과 차단");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(interior);
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(owner);
            }
        }

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
