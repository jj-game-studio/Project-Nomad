using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.CombatFeedback.Editor;
using Nomad.Monsters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed class CombatFeedbackTests
    {
        private readonly List<GameObject> created = new();
        private int previousCapture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            previousCapture = Time.captureFramerate;
            Time.captureFramerate = 60;
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.tag = "Untagged";
            CombatFeedbackSettings.Reset();
            CombatFeedbackSettings.Current.MonsterSound = false;
            CombatFeedbackSettings.Current.PlayerSound = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            Time.captureFramerate = previousCapture;
            foreach (GameObject item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            CombatFeedbackSettings.Reset();
            yield return new ExitPlayMode();
        }

        private GameObject New(string name)
        {
            var item = new GameObject(name);
            created.Add(item);
            return item;
        }

        [UnityTest]
        public IEnumerator SameFrameHitsKeepCriticalIdentityAndReportActualLossImmediately()
        {
            var monster = New("Feedback target").AddComponent<MonsterHitFeedback>();
            Health health = monster.GetComponent<Health>();
            health.TryApplyCombatDamage(20, true);
            health.TryApplyCombatDamage(10, false);
            Assert.That(monster.ActiveNumberCount, Is.EqualTo(2));
            Assert.That(monster.ActiveCriticalNumberCount, Is.EqualTo(1));
            Assert.That(monster.LastDamage, Is.EqualTo(10));
            Assert.That(monster.LastCritical, Is.False);

            health.IsCombatImmune = true;
            Assert.That(health.TryApplyDamage(5), Is.False);
            health.Heal(5);
            Assert.That(monster.ActiveNumberCount, Is.EqualTo(2));
            health.IsCombatImmune = false;
            health.TryApplyDamage(1000);
            Assert.That(monster.LastDamage, Is.EqualTo(75), "과잉 피해는 남은 HP만 표시");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewDoesNotDamageTargetsAndEffectsCanBeCleared()
        {
            var monster = New("Preview monster").AddComponent<MonsterHitFeedback>();
            var player = New("Preview player").AddComponent<PlayerDamageFeedback>();
            monster.PreviewHit(30, true);
            player.PreviewDamage(40);
            Assert.That(monster.GetComponent<Health>().CurrentHealth, Is.EqualTo(100));
            Assert.That(player.GetComponent<Health>().CurrentHealth, Is.EqualTo(100));
            Assert.That(monster.ActiveNumberCount, Is.EqualTo(1));
            Assert.That(monster.LastCritical, Is.True);
            yield return new WaitForSeconds(0.06f);
            var overlay = player.GetComponent<CombatFeedbackOverlay>();
            Assert.That(overlay.VignetteAlpha, Is.GreaterThan(0));

            monster.ClearFeedback();
            player.ClearFeedback();
            Assert.That(monster.ActiveNumberCount, Is.Zero);
            Assert.That(monster.ActiveShardCount, Is.Zero);
            Assert.That(overlay.VignetteAlpha, Is.Zero);
            Assert.That(player.Reacting, Is.False);
        }

        [UnityTest]
        public IEnumerator FlashToggleDoesNotDisableCameraAndNumbersAreBounded()
        {
            var camera = New("Feedback camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            var monster = New("Feedback target").AddComponent<MonsterHitFeedback>();
            monster.SetEffects(false, true, false);
            monster.PreviewHit(10);
            Assert.That(monster.IsFlashing, Is.False);
            Assert.That(camera.GetComponent<HitCameraImpulse>().IsPulsing, Is.True);
            for (int index = 0; index < 20; index++) monster.PreviewHit(10);
            Assert.That(monster.ActiveNumberCount, Is.EqualTo(8));

            CombatFeedbackSettings.Current.MonsterNumbers = false;
            monster.RefreshEffects();
            Assert.That(monster.ActiveNumberCount, Is.Zero);
            CombatFeedbackSettings.Current.MonsterNumbers = true;
            yield return null;
            Assert.That(monster.ActiveNumberCount, Is.Zero, "토글을 켜도 이전 피해는 재생하지 않음");
        }

        [UnityTest]
        public IEnumerator VignetteRisesHoldsAndReturnsWithoutCameraAndSmallHitPreservesPeak()
        {
            var overlay = New("Vignette").AddComponent<CombatFeedbackOverlay>();
            var settings = CombatFeedbackSettings.Current;
            settings.VignetteAttack = 0.06f;
            settings.VignetteHold = 0.4f;
            settings.VignetteRelease = 0.15f;
            overlay.Hurt(1);
            Assert.That(overlay.VignetteAlpha, Is.Zero, "처음부터 최대 밝기로 튀지 않음");
            yield return new WaitForSeconds(0.08f);
            overlay.RefreshPositions(null);
            Assert.That(overlay.VignetteAlpha, Is.EqualTo(settings.VignetteMaximum).Within(0.001));

            overlay.Hurt(0.01f);
            Assert.That(overlay.VignetteAlpha, Is.EqualTo(settings.VignetteMaximum).Within(0.001));
            for (int index = 0; index < 10; index++) overlay.Hurt(1);
            Assert.That(overlay.VignetteAlpha, Is.LessThanOrEqualTo(settings.VignetteMaximum));
            yield return new WaitForSeconds(0.8f);
            overlay.RefreshPositions(null);
            Assert.That(overlay.VignetteAlpha, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PauseAndDisableCannotLeaveOrReplayVignette()
        {
            var player = New("Feedback player").AddComponent<PlayerDamageFeedback>();
            Health health = player.GetComponent<Health>();
            health.IsCombatImmune = true;
            health.TryApplyDamage(10);
            Assert.That(player.GetComponent<CombatFeedbackOverlay>(), Is.Null);
            health.IsCombatImmune = false;
            health.TryApplyDamage(10);
            yield return new WaitForSeconds(0.07f);
            var overlay = player.GetComponent<CombatFeedbackOverlay>();
            float before = overlay.VignetteAlpha;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(overlay.VignetteAlpha, Is.EqualTo(before).Within(0.001));
            player.enabled = false;
            Assert.That(overlay.VignetteAlpha, Is.Zero);
            player.enabled = true;
            Time.timeScale = 1;
            yield return null;
            Assert.That(overlay.VignetteAlpha, Is.Zero);

            overlay.Hurt(0.5f);
            yield return new WaitForSeconds(0.06f);
            CombatFeedbackSettings.Current.PlayerVignette = false;
            overlay.RefreshVignette();
            Assert.That(overlay.VignetteAlpha, Is.Zero);
            CombatFeedbackSettings.Current.PlayerVignette = true;
            yield return null;
            Assert.That(overlay.VignetteAlpha, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CameraImpulseUsesMaximumAndAlwaysRestoresItsPosition()
        {
            var camera = New("Impulse camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(3, 7, -5);
            Vector3 original = camera.transform.position;
            HitCameraImpulse.Pulse(0.1f, 0.2f);
            var impulse = camera.GetComponent<HitCameraImpulse>();
            for (int index = 0; index < 20; index++) HitCameraImpulse.Pulse(0.01f, 0.05f);
            Assert.That(impulse.Strength, Is.EqualTo(0.1f).Within(0.001), "동시 적중은 더하거나 약화하지 않음");
            InvokeRender(impulse, "Begin", camera);
            InvokeRender(impulse, "End", camera);
            Assert.That(camera.transform.position, Is.EqualTo(original));
            InvokeRender(impulse, "Begin", camera);
            impulse.enabled = false;
            Assert.That(camera.transform.position, Is.EqualTo(original));
            Assert.That(impulse.IsPulsing, Is.False);
            yield return null;
        }

        private static void InvokeRender(HitCameraImpulse impulse, string name, Camera camera) =>
            typeof(HitCameraImpulse).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(impulse, new object[] { default(ScriptableRenderContext), camera });

        [Test]
        public void DebugWindowBuildsAndControlsTheLiveSettings()
        {
            var window = EditorWindow.GetWindow<CombatFeedbackDebugWindow>();
            try
            {
                window.CreateGUI();
                var toggles = window.rootVisualElement.Query<Toggle>().ToList();
                Assert.That(toggles.Count, Is.GreaterThanOrEqualTo(10));
                Assert.That(window.rootVisualElement.Query<Slider>().ToList().Count, Is.EqualTo(12));
                Toggle vignette = toggles.Find(toggle => toggle.label == "붉은 비네트");
                vignette.value = false;
                Assert.That(CombatFeedbackSettings.Current.PlayerVignette, Is.False);
                vignette.value = true;
                Assert.That(CombatFeedbackSettings.Current.PlayerVignette, Is.True);
            }
            finally { window.Close(); }
        }
    }
}
