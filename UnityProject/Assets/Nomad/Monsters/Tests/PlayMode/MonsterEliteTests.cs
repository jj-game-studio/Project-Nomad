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
