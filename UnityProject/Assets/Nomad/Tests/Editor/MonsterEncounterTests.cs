using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Tests
{
    public sealed class MonsterEncounterTests : PlayModeFixture
    {
        private NavMeshDataInstance ground;

        [SetUp]
        public void SetUp()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.35f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            var sources = new System.Collections.Generic.List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box, size = new Vector3(36, 0.6f, 36),
                    transform = Matrix4x4.Translate(Vector3.down * 0.3f)
                }
            };
            NavMeshData data = Track(NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(40, 8, 40)), Vector3.zero, Quaternion.identity));
            ground = NavMesh.AddNavMeshData(data);
        }

        [TearDown]
        public void RemoveGround()
        {
            if (ground.valid) ground.Remove();
        }

        [Test]
        public void ActualNavigationDistanceRejectsUnreachablePoints()
        {
            Assert.That(MonsterEncounterLayout.PathLength(Vector3.zero, Vector3.forward * 10), Is.InRange(9f, 11f));
            Assert.That(MonsterEncounterLayout.PathLength(Vector3.zero, Vector3.forward * 1000), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void LargeBodyRequiresMoreSpaceThanNormal()
        {
            GameObject obstacle = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obstacle.transform.position = new Vector3(1.2f, 1.5f, 0);
            obstacle.transform.localScale = new Vector3(0.3f, 2, 0.3f);
            Physics.SyncTransforms();
            Assert.That(MonsterEncounterLayout.Clear(Vector3.zero, 0.6f, 2.1f), Is.True);
            Assert.That(MonsterEncounterLayout.Clear(Vector3.zero, 1.5f, 4), Is.False);
        }

        [Test]
        public void ArenaIsSeparateReachableAndReleasesItsNavigation()
        {
            var arena = Track(new GameObject("Boss area test")).AddComponent<BossArena>();
            Assert.That(arena.Build(new Vector3(0, 0.15f, 15), Vector3.forward, 3), Is.True);
            Assert.That(arena.Center.z, Is.GreaterThan(28), "보스는 원래 필드 바깥 전용 공간에 있음");
            Assert.That(arena.Connected, Is.True);
            Assert.That(MonsterEncounterLayout.PathLength(Vector3.zero, arena.Center), Is.InRange(28f, 36f));
            Assert.That(arena.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThanOrEqualTo(2));
            arena.gameObject.SetActive(false);
            Assert.That(arena.Connected, Is.False, "지역 해제와 함께 보조 경로·링크 제거");
            Assert.That(MonsterEncounterLayout.PathLength(Vector3.zero, arena.Center), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void PlayerCanWalkRampOverExistingBorderWall()
        {
            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = Vector3.down * 0.3f;
            floor.transform.localScale = new Vector3(36, 0.6f, 36);
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0, 0.75f, 18);
            wall.transform.localScale = new Vector3(8, 1.5f, 0.5f);
            var arena = Track(new GameObject("Raised boss area")).AddComponent<BossArena>();
            Assert.That(arena.Build(new Vector3(0, 0.15f, 15), Vector3.forward, 3, 2), Is.True);
            var player = Track(new GameObject("Walking player")).AddComponent<CharacterController>();
            player.height = 1.8f;
            player.radius = 0.35f;
            player.center = Vector3.up * 0.9f;
            player.transform.position = new Vector3(0, 0.1f, 14);
            Physics.SyncTransforms();
            for (int step = 0; step < 180; step++) player.Move(new Vector3(0, -0.05f, 0.1f));
            Assert.That(player.transform.position.z, Is.GreaterThan(26), "실제 CharacterController로 외곽 벽을 넘어 구역에 진입");
            Assert.That(player.transform.position.y, Is.GreaterThan(1.8f), "보스 바닥 위에 서 있음");
            Assert.That(wall.activeSelf, Is.True, "기존 외곽 벽은 제거하지 않음");
        }
    }
}
