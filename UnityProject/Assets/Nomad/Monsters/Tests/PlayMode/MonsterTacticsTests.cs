#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Nomad.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterTacticsTests
    {
        private Scene previous;
        private Scene scene;
        private MonsterIdentity actor;
        private Health player;
        private MonsterMeleeAttack attack;
        private MonsterTactics tactics;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterAnimationLab.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return null;
            player = scene.GetRootGameObjects().Single(root => root.name == "Player").GetComponent<Health>();
            ((Behaviour)player.GetComponent(Type.GetType("Nomad.Player.PlayerMovement, Assembly-CSharp"))).enabled = false;
            var panel = scene.GetRootGameObjects().Single(root => root.name == "Test Controls").GetComponent(
                Type.GetType("Nomad.Monsters.Demo.MonsterTestPanel, Assembly-CSharp"));
            panel.GetType().GetMethod("SetInvincible").Invoke(panel, new object[] { false });
            foreach (MonsterIdentity value in scene.GetRootGameObjects().Select(root => root.GetComponent<MonsterIdentity>()).Where(a => a != null))
                value.gameObject.SetActive(false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private void Place(MonsterRank rank, float distance)
        {
            actor = scene.GetRootGameObjects().Select(root => root.GetComponent<MonsterIdentity>()).Single(a => a != null && a.Rank == rank);
            actor.gameObject.SetActive(true);
            actor.transform.rotation = Quaternion.identity;
            actor.GetComponent<NavMeshAgent>().Warp(new Vector3(4f, 0.05f, 0f));
            actor.GetComponent<NavMeshAgent>().speed = 0f;
            actor.GetComponent<MonsterTracker>().Configure(25f, 30f, 0f);
            player.transform.position = new Vector3(4f, 0.05f, distance);
            Physics.SyncTransforms();
            attack = actor.GetComponent<MonsterMeleeAttack>();
            tactics = actor.GetComponent<MonsterTactics>();
        }

        [UnityTest]
        public IEnumerator ChargeLocksDirectionAndCanBeSidestepped()
        {
            Place(MonsterRank.Elite, 8f);
            yield return Until(() => attack.Phase == MeleePhase.Windup, 3f);
            Assert.That(attack.Skill, Is.EqualTo(MonsterSkill.Charge));
            // 돌진 외곽선은 지형을 따라 잘게 나눠지므로 닫힌 사각형(시작=끝)인지 봅니다.
            LineRenderer outline = actor.GetComponentsInChildren<LineRenderer>().Single(line => line.name == "Attack Outline");
            Assert.That(outline.positionCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(Vector3.Distance(outline.GetPosition(0), outline.GetPosition(outline.positionCount - 1)), Is.LessThan(0.01f));
            player.transform.position += Vector3.right * 3f;
            Physics.SyncTransforms();
            yield return Until(() => tactics.ChargesStarted == 1, 2f);
            yield return Until(() => !tactics.IsDashing, 2f);
            Assert.That(actor.transform.position.z, Is.GreaterThan(5f));
            Assert.That(Mathf.Abs(actor.transform.position.x - 4f), Is.LessThan(0.1f));
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            Assert.That(tactics.Choose(8f, 2.4f, 0), Is.EqualTo(MonsterSkill.None), "charge cooldown prevents immediate repeat");
        }

        [UnityTest]
        public IEnumerator ChargeHitsPlayerOnlyOnce()
        {
            Place(MonsterRank.Elite, 8f);
            yield return Until(() => player.CurrentHealth < 100, 4f);
            attack.enabled = false;
            Assert.That(player.CurrentHealth, Is.EqualTo(65));
            Assert.That(actor.transform.position.z, Is.LessThan(8f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(player.CurrentHealth, Is.EqualTo(65));
        }

        [UnityTest]
        public IEnumerator ChargeStopsAtNewWallWithoutDamagingPlayer()
        {
            Place(MonsterRank.Elite, 8f);
            yield return Until(() => attack.Phase == MeleePhase.Windup, 3f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(4f, 1.5f, 4f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.3f);
            Physics.SyncTransforms();
            yield return Until(() => tactics.ChargesStarted == 1, 2f);
            yield return Until(() => !tactics.IsDashing, 2f);
            Assert.That(actor.transform.position.z, Is.LessThan(3.8f));
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            UnityEngine.Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator DragonMidRangeAreaCanBeEscapedDuringWindup()
        {
            Place(MonsterRank.Boss, 5f);
            yield return Until(() => attack.Phase == MeleePhase.Windup, 3f);
            Assert.That(attack.Skill, Is.EqualTo(MonsterSkill.Area));
            Assert.That(attack.CurrentAttackAngle, Is.EqualTo(360f));
            player.transform.position = new Vector3(4f, 0.05f, 9f);
            Physics.SyncTransforms();
            yield return Until(() => tactics.AreasReleased == 1, 2f);
            attack.enabled = false;
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator DragonAreaHitsOnce()
        {
            Place(MonsterRank.Boss, 5f);
            yield return Until(() => tactics.AreasReleased == 1, 3f);
            attack.enabled = false;
            Assert.That(player.CurrentHealth, Is.EqualTo(60));
            yield return new WaitForSeconds(0.3f);
            Assert.That(player.CurrentHealth, Is.EqualTo(60));
        }

        [UnityTest]
        public IEnumerator CoverBlocksDragonAreaDamage()
        {
            Place(MonsterRank.Boss, 5f);
            yield return Until(() => attack.Phase == MeleePhase.Windup, 3f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(4f, 1.5f, 2.5f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.3f);
            Physics.SyncTransforms();
            yield return Until(() => tactics.AreasReleased == 1, 2f);
            attack.enabled = false;
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            UnityEngine.Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator DeathDuringChargePreparationCancelsSkill()
        {
            Place(MonsterRank.Elite, 8f);
            yield return Until(() => attack.Phase == MeleePhase.Windup, 3f);
            Vector3 before = actor.transform.position;
            actor.GetComponent<Health>().TryApplyDamage(10000);
            yield return new WaitForSeconds(1f);
            Assert.That(tactics.ChargesStarted, Is.Zero);
            Assert.That(Vector3.Distance(before, actor.transform.position), Is.LessThan(0.1f));
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.That(condition(), Is.True, "skill condition before deadline");
        }
    }
}
#endif
