#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Nomad.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterHitFeedbackTests
    {
        private Scene previous;
        private Scene scene;
        private Health monster;
        private MonsterHitFeedback feedback;
        private MonoBehaviour panel;
        private Renderer body;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterElite.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Monster") monster = root.GetComponent<Health>();
                if (root.name == "Test Controls")
                    panel = (MonoBehaviour)root.GetComponent(Type.GetType("Nomad.Monsters.Demo.MonsterTestPanel, Assembly-CSharp"));
                if (root.name == "Player") root.GetComponent<Health>().IsCombatImmune = true;
            }
            yield return null;
            feedback = monster.GetComponent<MonsterHitFeedback>();
            body = monster.transform.Find("Body").GetComponent<Renderer>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ActualHpLossFlashesWhiteAndEmitsNumbersAndShards()
        {
            monster.TryApplyDamage(25);
            yield return null;
            Assert.That(feedback.LastDamage, Is.EqualTo(25));
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(1));
            Assert.That(feedback.ActiveShardCount, Is.GreaterThan(0));
            Assert.That(feedback.IsFlashing, Is.True);
            Assert.That(BodyColor(), Is.EqualTo(Color.white));
            yield return new WaitForSeconds(0.2f);
            Assert.That(feedback.IsFlashing, Is.False);
            Assert.That(BodyColor(), Is.Not.EqualTo(Color.white), "공격 예고/원래 색으로 복원");
            yield return new WaitForSeconds(0.7f);
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(feedback.ActiveShardCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PanelTogglesAreIndependentAndClearExistingEffects()
        {
            SetToggle("hitFlash", false);
            SetToggle("damageNumbers", false);
            monster.TryApplyDamage(25);
            yield return null;
            Assert.That(feedback.IsFlashing, Is.False);
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(feedback.ActiveShardCount, Is.GreaterThan(0));
            SetToggle("hitShards", false);
            SetToggle("damageNumbers", true);
            SetToggle("hitFlash", true);
            monster.TryApplyDamage(25);
            yield return null;
            Assert.That(feedback.IsFlashing, Is.True);
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(1));
            Assert.That(feedback.ActiveShardCount, Is.EqualTo(0));
            SetToggle("hitFlash", false);
            SetToggle("damageNumbers", false);
            yield return null;
            Assert.That(feedback.IsFlashing, Is.False);
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(BodyColor(), Is.Not.EqualTo(Color.white));
        }

        [UnityTest]
        public IEnumerator ImmuneDamageAndRecoveryDoNotProduceHits()
        {
            monster.IsCombatImmune = true;
            Assert.That(monster.TryApplyDamage(25), Is.False);
            yield return null;
            Assert.That(feedback.LastDamage, Is.EqualTo(0));
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(feedback.ActiveShardCount, Is.EqualTo(0));
            monster.IsCombatImmune = false;
            monster.TryApplyDamage(30);
            monster.TryApplyDamage(20);
            yield return null;
            Assert.That(feedback.LastDamage, Is.EqualTo(20), "실제 피해 이벤트별 감소량을 표시");
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(2));
            feedback.SetEffects(false, false, false);
            typeof(Health).GetProperty(nameof(Health.CurrentHealth)).SetValue(monster, monster.MaxHealth);
            yield return null;
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(feedback.ActiveShardCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LethalHitShowsRemainingHpThenReturnsToDeathColor()
        {
            monster.TryApplyDamage(1000);
            yield return null;
            Assert.That(feedback.LastDamage, Is.EqualTo(250));
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(1));
            Assert.That(feedback.ActiveShardCount, Is.GreaterThan(0));
            yield return new WaitForSeconds(0.2f);
            Assert.That(BodyColor(), Is.EqualTo(Color.gray));
            monster.gameObject.SetActive(false);
            Assert.That(feedback.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(feedback.ActiveShardCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator NewSpawnsInheritDisabledTogglesWithoutReplayingOldDamage()
        {
            SetToggle("hitFlash", false);
            SetToggle("damageNumbers", false);
            SetToggle("hitShards", false);
            panel.GetType().GetMethod("SetKind").Invoke(panel, new object[] { 0 });
            Health spawned = (Health)panel.GetType().GetMethod("SpawnAt").Invoke(panel,
                new object[] { new Vector3(3f, 0f, -2f) });
            Assert.That(spawned, Is.Not.Null);
            spawned.TryApplyDamage(25);
            yield return null;
            MonsterHitFeedback other = spawned.GetComponent<MonsterHitFeedback>();
            Assert.That(other.IsFlashing, Is.False);
            Assert.That(other.ActiveNumberCount, Is.EqualTo(0));
            Assert.That(other.ActiveShardCount, Is.EqualTo(0));
            SetToggle("hitFlash", true);
            SetToggle("damageNumbers", true);
            SetToggle("hitShards", true);
            yield return null;
            Assert.That(other.ActiveNumberCount, Is.EqualTo(0));
            spawned.TryApplyDamage(25);
            yield return null;
            Assert.That(other.ActiveNumberCount, Is.EqualTo(1));
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(other.IsFlashing, Is.True);
            Assert.That(other.ActiveNumberCount, Is.EqualTo(1));
        }

        private Color BodyColor()
        {
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        private void SetToggle(string name, bool value)
        {
            bool Read(string field) => field == name ? value : (bool)panel.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            panel.GetType().GetMethod("SetHitEffects").Invoke(panel,
                new object[] { Read("hitFlash"), Read("damageNumbers"), Read("hitShards") });
        }
    }
}
#endif
