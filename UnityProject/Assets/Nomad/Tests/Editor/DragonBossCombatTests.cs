using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed class DragonBossCombatTests
    {
        private readonly List<Object> created = new();
        private BossArena arena;
        private Health player, boss;
        private DragonBossCombat combat;
        private NavMeshDataInstance ground;
        private int previousCapture;

        [UnitySetUp]
        public IEnumerator EnterEncounter()
        {
            yield return new EnterPlayMode();
            BuildEncounter();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        public void BuildEncounter()
        {
            previousCapture = Time.captureFramerate;
            Time.captureFramerate = 60;
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.35f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            NavMeshData data = Track(NavMeshBuilder.BuildNavMeshData(settings,
                new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(36, 0.6f, 36), transform = Matrix4x4.Translate(Vector3.down * 0.3f) } },
                new Bounds(Vector3.zero, new Vector3(40, 8, 40)), Vector3.zero, Quaternion.identity));
            ground = NavMesh.AddNavMeshData(data);
            arena = Track(new GameObject("Boss test arena")).AddComponent<BossArena>();
            Assert.That(arena.Build(new Vector3(0, 0.15f, 15), Vector3.forward, 3), Is.True);
            GameObject target = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            target.name = "Boss test player";
            target.transform.position = arena.Center + Vector3.right * 3;
            target.GetComponent<CapsuleCollider>().center = Vector3.up;
            target.SetActive(false);
            player = target.AddComponent<Health>();
            JsonUtility.FromJsonOverwrite("{\"maxHealth\":2000}", player);
            target.SetActive(true);
            var factory = Track(new GameObject("Boss test factory")).AddComponent<MonsterActorFactory>();
            var material = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            factory.ConfigureMaterials(material, material, material, material);
            boss = factory.CreateRanked(player, MonsterTestSettings.Defaults(MonsterRank.Boss), MonsterRank.Boss,
                arena.Center, Quaternion.identity, "Test boss", arena.transform);
            arena.PrepareCombat(boss, player);
            combat = boss.GetComponent<DragonBossCombat>();
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator ClearNavigation()
        {
            foreach (Object item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            if (ground.valid) ground.Remove();
            Time.captureFramerate = previousCapture;
            yield return new ExitPlayMode();
        }

        [Test]
        public void RocksLeaveCenterAndEntranceClear()
        {
            BossBreakableRock[] rocks = arena.GetComponentsInChildren<BossBreakableRock>();
            Assert.That(rocks.Length, Is.EqualTo(6));
            foreach (BossBreakableRock rock in rocks)
            {
                Vector3 offset = rock.transform.position - arena.Center;
                Assert.That(Vector3.ProjectOnPlane(offset, Vector3.up).magnitude, Is.GreaterThan(6));
                if (Vector3.Dot(offset, arena.Forward) < 0)
                    Assert.That(Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, arena.Forward))), Is.GreaterThan(4));
                Assert.That(rock.GetComponentInChildren<NavMeshObstacle>().carving, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CircleIsSlowAndCanBeEscapedThenDamagesOnlyOnce()
        {
            combat.BeginPattern(DragonPattern.Circle);
            int hp = player.CurrentHealth;
            yield return Wait(1.5f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp), "전조 중 피해 없음");
            player.transform.position = arena.Center + Vector3.right * 8;
            Physics.SyncTransforms();
            yield return Wait(1);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp), "걸어서 범위를 빠져나가 회피");
            player.transform.position = arena.Center + Vector3.right * 3;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.Circle);
            yield return Wait(2.5f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 32), "강타는 한 번만 피해");
        }

        [UnityTest]
        public IEnumerator TripleHasNoGroundRangeAndBreathDirectionIsLocked()
        {
            player.transform.position = arena.Center + Vector3.right * 7;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.TripleFireball);
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.Zero, "3갈래 바닥 표시 없음");
            yield return Wait(1);
            Assert.That(combat.Effects.GetComponentsInChildren<BossFireball>().Length, Is.EqualTo(3));
            combat.Stun(0.01f);
            yield return Wait(0.03f);
            player.transform.position = arena.Center + Vector3.forward * 6;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.Breath);
            Quaternion facing = boss.transform.rotation;
            player.transform.position = arena.Center - Vector3.forward * 3;
            Physics.SyncTransforms();
            int hp = player.CurrentHealth;
            yield return Wait(2);
            Assert.That(Quaternion.Angle(boss.transform.rotation, facing), Is.LessThan(0.1f));
            Assert.That(player.CurrentHealth, Is.EqualTo(hp), "브레스 뒤로 회피 가능");
        }

        [UnityTest]
        public IEnumerator ThresholdStunsCancelAttacksAndOnlyTriggerOnce()
        {
            combat.BeginPattern(DragonPattern.Circle);
            boss.TryApplyDamage(Mathf.CeilToInt(boss.MaxHealth * 0.26f));
            yield return null;
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Stunned));
            Assert.That(combat.ThresholdStuns, Is.EqualTo(1));
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.Zero);
            float until = combat.Remaining;
            boss.TryApplyDamage(1);
            yield return Wait(0.2f);
            Assert.That(combat.Remaining, Is.LessThan(until), "피격 때마다 기절 연장하지 않음");
            boss.TryApplyDamage(Mathf.CeilToInt(boss.MaxHealth * 0.25f));
            yield return null;
            Assert.That(combat.ThresholdStuns, Is.EqualTo(2));
            boss.TryApplyDamage(boss.MaxHealth);
            yield return null;
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Dead));
            Assert.That(combat.Effects.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ChargingIntoRockBreaksItAndStunsBoss()
        {
            yield return Wait(0.2f);
            BossBreakableRock rock = arena.GetComponentsInChildren<BossBreakableRock>().First();
            Vector3 point = rock.transform.position;
            Assert.That(boss.GetComponent<NavMeshAgent>().Warp(point + Vector3.right * 4), Is.True);
            player.transform.position = point - Vector3.right * 3;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.Charge);
            yield return Wait(2);
            Assert.That(rock == null || rock.Broken, Is.True, $"phase={combat.Phase} position={boss.transform.position} rock={point}");
            Assert.That(combat.RockStuns, Is.EqualTo(1));
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Stunned));
        }

        [UnityTest]
        public IEnumerator FirePoolTicksAndStopsAfterLeavingAndDeathCleansIt()
        {
            combat.BeginPattern(DragonPattern.FirePool);
            int hp = player.CurrentHealth;
            yield return Wait(2.4f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 10));
            player.transform.position = arena.Center - Vector3.right * 6;
            Physics.SyncTransforms();
            yield return Wait(1);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 10));
            boss.TryApplyDamage(boss.MaxHealth);
            yield return null;
            Assert.That(combat.Effects.childCount, Is.Zero);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator BasicAttackIsSlowAndRainWarningsStayAtTheirOriginalPosition()
        {
            combat.BeginPattern(DragonPattern.Basic);
            int hp = player.CurrentHealth;
            yield return Wait(0.5f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp));
            yield return Wait(0.4f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 18));
            combat.BeginPattern(DragonPattern.Rain);
            yield return Wait(1.7f);
            BossHazard first = combat.Effects.GetComponentsInChildren<BossHazard>().First();
            Vector3 warningPosition = first.transform.position;
            Assert.That(first.Active, Is.False, "작은 불덩이에도 회피 전조 있음");
            Assert.That(first.Radius, Is.EqualTo(1.15f));
            player.transform.position = arena.Center - Vector3.right * 6;
            Physics.SyncTransforms();
            yield return Wait(0.5f);
            Assert.That(first.transform.position, Is.EqualTo(warningPosition), "생성한 표식은 플레이어를 따라가지 않음");
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.GreaterThanOrEqualTo(2));
            boss.TryApplyDamage(boss.MaxHealth);
            yield return null;
            Assert.That(combat.Effects.childCount, Is.Zero);
        }
    }
}
