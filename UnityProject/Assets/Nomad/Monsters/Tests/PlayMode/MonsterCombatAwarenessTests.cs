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
    public sealed class MonsterCombatAwarenessTests
    {
        private Scene previous;
        private Scene scene;
        private Health player;
        private Health monster;
        private MonsterTracker tracker;
        private MonsterMeleeAttack attack;
        private NavMeshAgent agent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterCombat.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            GameObject[] roots = scene.GetRootGameObjects();
            player = Array.Find(roots, root => root.name == "Player").GetComponent<Health>();
            monster = Array.Find(roots, root => root.name == "Monster").GetComponent<Health>();
            tracker = monster.GetComponent<MonsterTracker>();
            attack = monster.GetComponent<MonsterMeleeAttack>();
            agent = monster.GetComponent<NavMeshAgent>();
            foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
                if (component.GetType().FullName == "Nomad.Player.PlayerMovement") component.enabled = false;
            player.IsCombatImmune = true;
            yield return null;
            agent.speed = 0f;
            Place(new Vector3(2f, 0.05f, 2f), Vector3.back);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator RearIdleTargetNeedsRealDamageToStartCombat()
        {
            yield return new WaitForSeconds(0.2f);
            Assert.That(tracker.IsTracking, Is.False, "전투 전에는 기존 시야각 유지");
            monster.IsCombatImmune = true;
            Assert.That(monster.TryApplyDamage(10), Is.False);
            yield return null;
            Assert.That(tracker.IsTracking, Is.False, "막힌 피해로 어그로를 얻지 않음");
            monster.IsCombatImmune = false;
            monster.TryApplyDamage(10);
            yield return Until(() => tracker.IsTracking && tracker.HasLineOfSight, "후방 피격 반응");
        }

        [UnityTest]
        public IEnumerator EngagedTargetRemainsVisibleWhenMonsterFacesAway()
        {
            Place(new Vector3(2f, 0.05f, 2f), Vector3.forward);
            yield return Until(() => tracker.IsTracking, "정면 최초 탐지");
            monster.transform.rotation = Quaternion.LookRotation(Vector3.back);
            yield return null;
            Assert.That(tracker.IsTracking && tracker.HasLineOfSight, Is.True,
                "전투 중에는 시야각만으로 목표를 잊지 않음");
        }

        [UnityTest]
        public IEnumerator RearTargetOutsideProximityIsRememberedAfterAttackRecovery()
        {
            Place(new Vector3(2f, 0.05f, -0.6f), Vector3.forward);
            yield return Until(() => attack.Phase == MeleePhase.Recovery, "기본 공격 회복");
            MovePlayer(new Vector3(2f, 0.05f, -6f));
            yield return Until(() => tracker.enabled && tracker.IsTracking, "회복 후 후방 목표 유지");
            yield return null;
            Assert.That(tracker.HasLineOfSight, Is.True);
        }

        [UnityTest]
        public IEnumerator CloseRearTargetIsFacedBeforeWindupStarts()
        {
            Place(new Vector3(2f, 0.05f, -3f), Vector3.forward);
            yield return Until(() => attack.Phase == MeleePhase.Windup, "제자리 뒤돌기 후 공격");
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - monster.transform.position, Vector3.up);
            Assert.That(Vector3.Angle(attack.AttackDirection, direction), Is.LessThanOrEqualTo(5.1f));
        }

        [UnityTest]
        public IEnumerator RememberedCombatStillRespectsWallAndLastSeenPosition()
        {
            Place(new Vector3(2f, 0.05f, 2f), Vector3.forward);
            yield return Until(() => tracker.IsTracking && agent.hasPath, "마지막 목격 경로");
            Vector3 remembered = agent.destination;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(2f, 1f, 0f);
            wall.transform.localScale = new Vector3(6f, 2f, 0.2f);
            tracker.enabled = false;
            tracker.enabled = true;
            tracker.ResumeCombatTracking();
            MovePlayer(new Vector3(4f, 0.05f, 2f));
            yield return new WaitForSeconds(0.3f);
            Assert.That(tracker.HasLineOfSight, Is.False);
            Assert.That(Vector3.Distance(agent.destination, remembered), Is.LessThan(0.1f),
                "벽 뒤 실시간 위치로 경로를 갱신하지 않음");
            yield return Until(() => tracker.IsReturning, "시야 상실 후 복귀", 3f);
        }

        [UnityTest]
        public IEnumerator DamageDuringReturnOrOutsideAbandonRangeCannotReengage()
        {
            Place(new Vector3(2f, 0.05f, 2f), Vector3.forward);
            yield return Until(() => tracker.IsTracking, "전투 시작");
            MovePlayer(new Vector3(2f, 0.05f, 20f));
            yield return Until(() => tracker.IsReturning, "거리 초과 복귀");
            monster.TryApplyDamage(10);
            yield return null;
            Assert.That(tracker.IsTracking, Is.False);
            MovePlayer(new Vector3(2f, 0.05f, -6f));
            monster.TryApplyDamage(10);
            yield return null;
            Assert.That(tracker.IsReturning && !tracker.IsTracking, Is.True,
                "복귀 중 피격으로 다시 끌려오지 않음");
        }

        private void Place(Vector3 playerPosition, Vector3 facing)
        {
            attack.enabled = false;
            agent.Warp(new Vector3(2f, 0f, -2f));
            monster.transform.rotation = Quaternion.LookRotation(facing);
            MovePlayer(playerPosition);
            tracker.BindTarget(player.transform);
            attack.enabled = true;
        }

        private void MovePlayer(Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private IEnumerator Until(Func<bool> condition, string label, float seconds = 2.5f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, label);
        }
    }
}
#endif
