using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nomad.Tests
{
    public sealed class BossArrivalSceneTests
    {
        private readonly List<InputDevice> muted = new();
        private BossDebugBootstrap scene;
        private const string Key = "Nomad.ArrivalTestPreviousScene";
        private int capture;

        [UnitySetUp]
        public IEnumerator Enter()
        {
            SessionState.SetString(Key, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene("Assets/Nomad/Monsters/BossDebug.unity");
            yield return new EnterPlayMode();
            capture = Time.captureFramerate;
            Time.captureFramerate = 60;
            foreach (InputDevice device in InputSystem.devices.Where(device => device is Keyboard || device is Mouse).ToArray())
            { InputSystem.DisableDevice(device); muted.Add(device); }
            for (int frame = 0; frame < 120; frame++)
            {
                scene = Object.FindFirstObjectByType<BossDebugBootstrap>();
                if (scene != null && scene.Ready) break;
                yield return null;
            }
            Assert.That(scene != null && scene.Ready, Is.True);
            scene.Boss.AutomaticPatterns = false;
            scene.Player.GetComponent<Health>().IsCombatImmune = true;
        }

        [UnityTearDown]
        public IEnumerator Leave()
        {
            foreach (InputDevice device in muted) InputSystem.EnableDevice(device);
            muted.Clear();
            Time.captureFramerate = capture;
            yield return new ExitPlayMode();
            string path = SessionState.GetString(Key, "");
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) EditorSceneManager.OpenScene(path);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator BossIsHiddenUntilWarningThenFallsBeforeVitalsAppear()
        {
            var boss = scene.Boss;
            var arrival = boss.GetComponent<BossArrival>();
            var health = boss.GetComponent<Health>();
            var bar = boss.GetComponent<SegmentedHealthBar>();
            Assert.That(arrival.Landed, Is.False);
            Assert.That(arrival.BodyVisible, Is.False);
            Assert.That(bar.enabled, Is.False);
            Assert.That(health.TryApplyDamage(50), Is.False);
            Assert.That(boss.GetComponent<CombatFeedbackOverlay>().GetComponentsInChildren<Text>(true)
                .Where(text => text.name == "Armor status").All(text => text.text == ""), Is.True);
            Assert.That(scene.Arena.GetComponentsInChildren<Transform>().Any(item => item.name == "Altar inlay"), Is.False);

            while (!arrival.Started) yield return null;
            float until = Time.time + 2.5f;
            while (!arrival.BodyVisible && Time.time < until) yield return null;
            Assert.That(arrival.BodyVisible, Is.True);
            Assert.That(arrival.Landed, Is.False);
            Assert.That(bar.enabled, Is.False, "떨어지는 동안에도 보스 체력 숨김");
            Assert.That(boss.transform.Find("Body").localPosition.y, Is.GreaterThan(5));
            while (!arrival.Landed && Time.time < until) yield return null;
            Assert.That(arrival.Landed, Is.True);
            Assert.That(bar.enabled, Is.True);
            Assert.That(health.IsCombatImmune, Is.False);
            Assert.That(boss.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator DeathUsesItsOwnClipAndStaysDownInsteadOfGettingUp()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            Health health = boss.GetComponent<Health>();
            health.TryApplyDamage(health.MaxHealth * 2);
            yield return null;
            yield return null;
            var death = boss.GetComponent<BossDeathPresentation>();
            Animator animator = boss.GetComponentInChildren<Animator>();
            Assert.That(death.Playing, Is.True);
            Assert.That(animator.GetCurrentAnimatorClipInfo(0).First().clip.name, Is.EqualTo("BossDeath"));
            Assert.That(boss.GetComponent<MonsterAnimationDriver>().enabled, Is.False);
            float until = Time.time + 2.8f;
            while (Time.time < until) yield return null;
            Assert.That(death.Progress, Is.EqualTo(1));
            Assert.That(animator.GetCurrentAnimatorClipInfo(0).First().clip.name, Is.EqualTo("BossDeath"));
            Assert.That(boss.GetComponent<Collider>().enabled, Is.False);
            Assert.That(health.IsDead, Is.True);
        }
    }
}
