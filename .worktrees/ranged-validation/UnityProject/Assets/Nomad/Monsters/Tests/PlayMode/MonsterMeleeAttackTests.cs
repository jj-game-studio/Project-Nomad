#if UNITY_EDITOR
using System;
using System.Collections;
using Nomad.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterMeleeAttackTests
    {
        private Scene previousScene;
        private Scene scene;
        private Health player;
        private Health monster;
        private MonsterMeleeAttack attack;
        private MonsterTracker tracker;
        private NavMeshAgent agent;
        private Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterCombat.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            GameObject[] roots = scene.GetRootGameObjects();
            player = Array.Find(roots, root => root.name == "Player").GetComponent<Health>();
            monster = Array.Find(roots, root => root.name == "Monster").GetComponent<Health>();
            camera = Array.Find(roots, root => root.name == "Combat Camera").GetComponent<Camera>();
            attack = monster.GetComponent<MonsterMeleeAttack>();
            tracker = monster.GetComponent<MonsterTracker>();
            agent = monster.GetComponent<NavMeshAgent>();
            foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
            {
                if (component.GetType().FullName == "Nomad.Player.PlayerMovement")
                    component.enabled = false;
            }
            Assert.That(agent.isOnNavMesh, Is.True);
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
        public IEnumerator WindupDealsOneHitThenRecovers()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "공격 준비");
            Assert.That(player.CurrentHealth, Is.EqualTo(100), "준비 중 피해 없음");
            Assert.That(agent.isStopped && !tracker.enabled, Is.True,
                $"준비 중 추적 정지: stopped={agent.isStopped}, tracker={tracker.enabled}");
            yield return Until(() => player.CurrentHealth < 100, 1f, "첫 타격");
            Assert.That(player.CurrentHealth, Is.EqualTo(80));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(player.CurrentHealth, Is.EqualTo(80), "재사용 대기 중 중복 피해 없음");
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Recovery));
        }

        [UnityTest]
        public IEnumerator LeavingRangeDuringWindupAvoidsDamage()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "공격 준비");
            MovePlayer(new Vector3(0f, 0.05f, 8f));
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator WallAppearingDuringWindupBlocksDamage()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "공격 준비");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (player.transform.position + monster.transform.position) * 0.5f + Vector3.up;
            wall.transform.localScale = new Vector3(3f, 2f, 0.15f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator CombatImmunityRejectsMeleeDamage()
        {
            player.IsCombatImmune = true;
            yield return Until(() => attack.Phase == MeleePhase.Recovery, 4f, "무적 타격 시도");
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            player.IsCombatImmune = false;
            yield return Until(() => player.CurrentHealth < 100, 3f, "무적 종료 후 피해");
            Assert.That(player.CurrentHealth, Is.EqualTo(80));
        }

        [UnityTest]
        public IEnumerator MonsterDeathCancelsPreparedStrikeAndFiresDeathOnce()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "공격 준비");
            int deaths = 0;
            monster.Died += _ => deaths++;
            Assert.That(monster.TryApplyDamage(100), Is.True);
            Assert.That(monster.TryApplyDamage(20), Is.False);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Dead));
            Assert.That(!tracker.enabled && agent.isStopped && !agent.hasPath, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerDeathCancelsPreparedStrikeAndTracking()
        {
            yield return Until(() => attack.Phase == MeleePhase.Windup, 4f, "공격 준비");
            int deaths = 0;
            player.Died += _ => deaths++;
            player.TryApplyDamage(100);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(player.CurrentHealth, Is.EqualTo(0));
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Idle));
            Assert.That(!tracker.enabled && agent.isStopped && !agent.hasPath, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerShotsKillMonsterThroughSharedProjectile()
        {
            agent.speed = 0f;
            int deaths = 0;
            monster.Died += _ => deaths++;
            MonoBehaviour shooter = Array.Find(player.GetComponents<MonoBehaviour>(),
                value => value.GetType().FullName == "Nomad.Player.PlayerCombat");
            // 기본 Assembly-CSharp를 재편하지 않고 기존 공개 사격 계약으로 통합을 확인합니다.
            var shoot = shooter.GetType().GetMethod("TryShoot");

            for (int shot = 1; shot <= 4; shot++)
            {
                Vector2 pointer = camera.WorldToScreenPoint(monster.transform.position + Vector3.up);
                Assert.That((bool)shoot.Invoke(shooter, new object[] { pointer }), Is.True,
                    $"기존 PlayerCombat.TryShoot 발사 {shot}");
                int expected = 100 - shot * 25;
                yield return Until(() => monster.CurrentHealth == expected, 2f, $"플레이어 사격 {shot}");
            }

            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Dead));
            Assert.That(!tracker.enabled && agent.isStopped, Is.True);
        }

        private IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(condition(), Is.True,
                $"{label}: phase={attack.Phase}, playerHP={player.CurrentHealth}, monsterHP={monster.CurrentHealth}");
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
