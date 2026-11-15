#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Nomad.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterEliteTests
    {
        private Scene previousScene;
        private Scene scene;
        private Health player;
        private Health elite;
        private MonsterMeleeAttack attack;
        private MonsterTracker tracker;
        private NavMeshAgent agent;
        private LineRenderer warning;
        private MeshFilter fill;
        private Renderer fillRenderer;
        private SegmentedHealthBar healthBar;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterElite.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            GameObject[] roots = scene.GetRootGameObjects();
            player = Array.Find(roots, root => root.name == "Player").GetComponent<Health>();
            elite = Array.Find(roots, root => root.name == "Monster").GetComponent<Health>();
            attack = elite.GetComponent<MonsterMeleeAttack>();
            tracker = elite.GetComponent<MonsterTracker>();
            agent = elite.GetComponent<NavMeshAgent>();
            warning = Array.Find(roots, root => root.name == "Heavy Warning").GetComponent<LineRenderer>();
            fill = Array.Find(roots, root => root.name == "Attack Fill").GetComponent<MeshFilter>();
            fillRenderer = fill.GetComponent<Renderer>();
            healthBar = elite.GetComponent<SegmentedHealthBar>();
            foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
            {
                if (component.GetType().FullName == "Nomad.Player.PlayerMovement")
                    component.enabled = false;
            }
            player.IsCombatImmune = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator TwoLightAttemptsThenHeavyDealsOneFortyDamageHit()
        {
            var sequence = new List<bool>();
            bool wasWinding = false;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (sequence.Count < 3 && Time.realtimeSinceStartup < deadline)
            {
                bool winding = attack.Phase == MeleePhase.Windup;
                if (winding && !wasWinding)
                    sequence.Add(attack.IsHeavyAttack);
                wasWinding = winding;
                yield return null;
            }
            CollectionAssert.AreEqual(new[] { false, false, true }, sequence);
            Assert.That(elite.MaxHealth, Is.EqualTo(250));
            Assert.That(warning.enabled, Is.True, "강공격 준비 중 바닥 경고");
            Assert.That(!tracker.enabled && agent.isStopped, Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(100), "준비 중 피해 없음");

            player.IsCombatImmune = false;
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 2f, "강공격 타격");
            Assert.That(player.CurrentHealth, Is.EqualTo(60));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Recovery), "강공격 후 긴 빈틈");
            Assert.That(player.CurrentHealth, Is.EqualTo(60), "강공격 한 번만 피해 적용");
            Assert.That(warning.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator SidesteppingLockedHeavyDirectionAvoidsDamage()
        {
            yield return EnterHeavyWindup();
            Vector3 locked = attack.AttackDirection;
            player.IsCombatImmune = false;
            Vector3 side = Quaternion.AngleAxis(90f, Vector3.up) * locked;
            MovePlayer(elite.transform.position + side * 2f + Vector3.up * 0.05f);
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 2f, "측면 회피");
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            Assert.That(attack.AttackDirection, Is.EqualTo(locked), "준비 중 조준 방향 고정");
        }

        [UnityTest]
        public IEnumerator LeavingHeavyRangeAvoidsDamage()
        {
            yield return EnterHeavyWindup();
            player.IsCombatImmune = false;
            MovePlayer(elite.transform.position + attack.AttackDirection * 4f + Vector3.up * 0.05f);
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 2f, "강공격 범위 이탈");
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator WallBlocksHeavyStrike()
        {
            yield return EnterHeavyWindup();
            player.IsCombatImmune = false;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (player.transform.position + elite.transform.position) * 0.5f + Vector3.up;
            wall.transform.localScale = new Vector3(3f, 2f, 0.15f);
            Physics.SyncTransforms();
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 2f, "강공격 벽 차단");
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator CombatImmunityBlocksHeavyStrike()
        {
            yield return EnterHeavyWindup();
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 2f, "강공격 무적 계약");
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator DeathCancelsHeavyStrikeAndWarning()
        {
            yield return EnterHeavyWindup();
            int deaths = 0;
            elite.Died += _ => deaths++;
            elite.TryApplyDamage(250);
            player.IsCombatImmune = false;
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Dead));
            Assert.That(!tracker.enabled && agent.isStopped && !agent.hasPath, Is.True);
            Assert.That(warning.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator HealthBarShowsWholeAndPartialSegments()
        {
            Assert.That(healthBar.SegmentCount, Is.EqualTo(10));
            Assert.That(healthBar.GetSegmentFill(9), Is.EqualTo(1f));
            elite.TryApplyDamage(13);
            Assert.That(healthBar.GetSegmentFill(9), Is.EqualTo(0.48f).Within(0.01f));
            elite.TryApplyDamage(12);
            Assert.That(healthBar.GetSegmentFill(8), Is.EqualTo(1f));
            Assert.That(healthBar.GetSegmentFill(9), Is.EqualTo(0f));
            elite.TryApplyDamage(225);
            yield return null;
            for (int i = 0; i < 10; i++)
                Assert.That(healthBar.GetSegmentFill(i), Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator FillReachesFullBeforeDamageAndMatchesAttackProgress()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "기본 공격 준비");
            player.IsCombatImmune = false;
            bool fullSeen = false;
            float previous = 0f;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (attack.Phase == MeleePhase.Windup && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(player.CurrentHealth, Is.EqualTo(100), "채움 완료 전 피해 없음");
                Assert.That(attack.WindupProgress, Is.GreaterThanOrEqualTo(previous));
                previous = attack.WindupProgress;
                Assert.That(fillRenderer.enabled, Is.True);
                Assert.That(fill.transform.localScale.x,
                    Is.EqualTo(attack.CurrentAttackRange * attack.WindupProgress).Within(0.01f));
                fullSeen |= attack.WindupProgress >= 1f;
                yield return null;
            }
            Assert.That(fullSeen, Is.True, "100% 채움 프레임 표시");
            Assert.That(player.CurrentHealth, Is.EqualTo(80), "채움 완료 후 한 번 타격");
            Assert.That(fillRenderer.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator DoubleAttackSpeedShortensWindup()
        {
            attack.AttackSpeed = 2f;
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "2배 공속 준비");
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 0.45f, "2배 공속 타격");
        }

        [UnityTest]
        public IEnumerator ChangingSpeedDuringWindupAcceleratesSameFill()
        {
            attack.AttackSpeed = 0.5f;
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "느린 준비");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(attack.WindupProgress, Is.InRange(0.1f, 0.5f));
            float before = attack.WindupProgress;
            attack.AttackSpeed = 2f;
            yield return Until(() => attack.WindupProgress >= 1f, 0.4f, "준비 중 공속 증가");
            Assert.That(attack.WindupProgress, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator PartialFillClearsOnDeath()
        {
            yield return EnterHeavyWindup();
            yield return Until(() => attack.WindupProgress > 0.15f, 1f, "강공격 부분 채움");
            Assert.That(fillRenderer.enabled, Is.True);
            elite.TryApplyDamage(250);
            yield return null;
            Assert.That(attack.WindupProgress, Is.EqualTo(0f));
            Assert.That(fillRenderer.enabled || warning.enabled, Is.False);
            Assert.That(fill.transform.localScale, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator BasicUsesCircleAndHeavyUsesSectorFill()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup && attack.WindupProgress > 0.1f,
                4f, "원형 기본 경고");
            Assert.That(warning.positionCount, Is.EqualTo(17));
            Assert.That(fill.sharedMesh.bounds.min.z, Is.LessThan(-0.9f));
            yield return EnterHeavyWindup();
            Assert.That(warning.positionCount, Is.EqualTo(19));
            Assert.That(fill.sharedMesh.bounds.min.z, Is.GreaterThanOrEqualTo(0f));
            Assert.That(fill.sharedMesh.vertexCount, Is.EqualTo(34));
        }

        private IEnumerator EnterHeavyWindup()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup && attack.IsHeavyAttack,
                10f, "강공격 준비 진입");
            yield return null;
        }

        private IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True,
                $"{label}: phase={attack.Phase}, heavy={attack.IsHeavyAttack}, playerHP={player.CurrentHealth}");
        }

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
#endif
