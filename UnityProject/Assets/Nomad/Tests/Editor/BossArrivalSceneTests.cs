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
        private const string PreviousSceneKey = "Nomad.ArrivalTestPreviousScene";
        private int capture;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool background;
        private InputSettings.BackgroundBehavior backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode inputBehavior;

        [UnitySetUp]
        public IEnumerator Enter()
        {
            SessionState.SetString(PreviousSceneKey, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene("Assets/Nomad/Monsters/BossDebug.unity");
            yield return new EnterPlayMode();
            capture = Time.captureFramerate;
            Time.captureFramerate = 60;
            background = Application.runInBackground;
            backgroundBehavior = InputSystem.settings.backgroundBehavior;
            inputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (InputDevice device in InputSystem.devices.Where(device => device is Keyboard || device is Mouse).ToArray())
            { InputSystem.DisableDevice(device); muted.Add(device); }
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(Screen.width - 20, Screen.height / 2) });
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
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Application.runInBackground = background;
            InputSystem.settings.backgroundBehavior = backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = inputBehavior;
            muted.Clear();
            Time.captureFramerate = capture;
            yield return new ExitPlayMode();
            string path = SessionState.GetString(PreviousSceneKey, "");
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

        [UnityTest]
        public IEnumerator PersistentGodModeSurvivesMovementAndRollImmunityUpdates()
        {
            scene.Boss.GetComponent<BossArrival>().Skip();
            Health health = scene.Player.GetComponent<Health>();
            health.IsCombatImmune = false;
            scene.GodMode = true;
            int hp = health.CurrentHealth;
            yield return Wait(0.2f);
            Assert.That(health.TryApplyDamage(35), Is.False);
            Assert.That(health.TryApplyDamage(35, DamageKind.Fall), Is.False);
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.LeftShift));
            yield return Wait(0.1f);
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return Wait(0.8f);
            Assert.That(health.TryApplyDamage(35), Is.False, "구르기 종료가 디버그 무적을 지우지 않음");
            Assert.That(health.CurrentHealth, Is.EqualTo(hp));
            scene.GodMode = false;
            Assert.That(health.TryApplyDamage(10), Is.True);
            Assert.That(scene.Player.GetComponent<Nomad.WorldHud.CharacterHealthHud>().Fraction,
                Is.EqualTo((hp - 10) / (float)health.MaxHealth).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator YellowFlashThenFParriesRadialAndSlamOnceAndEruptsSafeRocks()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            Health health = scene.Player.GetComponent<Health>();
            health.IsCombatImmune = false;
            scene.GodMode = false;
            Vector3 point = scene.Arena.GroundPoint(scene.Arena.Center - Vector3.forward * 6);
            scene.Player.transform.position = point;
            Physics.SyncTransforms();
            var signal = boss.GetComponent<BossParrySignal>();
            var parry = scene.Player.GetComponent<Nomad.Equipment.PlayerBossParry>();
            int hp = health.CurrentHealth;
            foreach (DragonPattern pattern in new[] { DragonPattern.RadialFireballs, DragonPattern.Circle })
            {
                int before = scene.Arena.RockCount;
                boss.BeginPattern(pattern);
                while (Time.time < signal.FlashAt + 0.02f) yield return null;
                Assert.That(signal.Flashing, Is.True);
                Assert.That(scene.Player.CanParry, Is.True, "UI 밖에서 실제 F 패링 입력 가능");
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.F));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return Wait(1.1f);
                Assert.That(scene.Arena.RockCount - before, Is.InRange(2, 3), pattern + " F 성공=" + parry.Successes);
                Assert.That(health.CurrentHealth, Is.EqualTo(hp));
            }
            Assert.That(parry.Successes, Is.EqualTo(2));
            Assert.That(parry.Reflections, Is.EqualTo(1), "전방향 72발을 여러 번 반사하지 않음");
            Assert.That(boss.GetComponent<BossArmor>().BreakCount, Is.LessThanOrEqualTo(2));
            foreach (BossBreakableRock rock in scene.Arena.GetComponentsInChildren<BossBreakableRock>())
                Assert.That(rock.GetComponentInChildren<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator EnrageAndBreathHaveRealDamageRulesAndDedicatedVisuals()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            Health health = boss.GetComponent<Health>();
            health.TryApplyDamage(health.CurrentHealth - health.MaxHealth / 4, DamageKind.Fall);
            yield return null;
            Assert.That(boss.Enraged, Is.True);
            Assert.That(boss.Tempo, Is.GreaterThan(1));
            Assert.That(boss.MoveSpeed, Is.GreaterThanOrEqualTo(8));
            Assert.That(boss.ScaleDamage(20), Is.EqualTo(28));
            Assert.That(boss.GetComponent<BossVisualStyle>().EyesBurning, Is.True);
            foreach (string name in new[] { "BossCel", "BossOutline" })
            {
                Shader shader = Resources.Load<Shader>(name);
                Assert.That(shader, Is.Not.Null);
                Assert.That(UnityEditor.ShaderUtil.GetShaderMessages(shader).Any(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.False);
            }
            boss.BeginPattern(DragonPattern.Breath);
            yield return Wait(1.8f);
            BossHazard breath = boss.Effects.GetComponentInChildren<BossHazard>();
            Assert.That(breath.WarningVisible, Is.False);
            Assert.That(breath.Radius, Is.EqualTo(18));
            Assert.That(breath.Contains(boss.transform.position + boss.transform.forward * 2), Is.False);
            Assert.That(breath.VisualRangeMultiplier, Is.GreaterThan(1));
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator RainCanBeWalkedOutOfAfterSeeingTheWarning()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.GodMode = false;
            Health health = scene.Player.GetComponent<Health>();
            health.IsCombatImmune = false;
            boss.Tuning.RainWaves = 1;
            boss.Tuning.RainPerWave = 1;
            boss.BeginPattern(DragonPattern.Rain);
            BossHazard warning = null;
            float deadline = Time.time + 2;
            while (warning == null && Time.time < deadline)
            { warning = boss.Effects.GetComponentInChildren<BossHazard>(); yield return null; }
            Assert.That(warning, Is.Not.Null);
            Vector3 center = warning.transform.position;
            int hp = health.CurrentHealth;
            yield return Wait(0.25f);
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));
            yield return Wait(0.65f);
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            Assert.That(Vector3.ProjectOnPlane(scene.Player.transform.position - center, Vector3.up).magnitude, Is.GreaterThan(2));
            Assert.That(health.CurrentHealth, Is.EqualTo(hp), "표식 확인 후 0.25초 반응/걷기만으로 회피");
            Assert.That(health.IsCombatImmune, Is.False);
        }

        [UnityTest]
        public IEnumerator DenseRadialWaveHitsOnlyOnceAndRuntimeParametersChangeNextAttack()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            scene.Player.transform.position = scene.Arena.GroundPoint(scene.Arena.Center - Vector3.forward * 10);
            Health health = scene.Player.GetComponent<Health>();
            health.IsCombatImmune = false;
            scene.GodMode = false;
            scene.Player.GetComponent<Nomad.Equipment.PlayerStats>().RandomSource = () => 0.99f;
            int hits = 0;
            health.Damaged += (_, _, _) => hits++;
            Physics.SyncTransforms();
            Assert.That(boss.Tuning.RadialRadius * 2, Is.GreaterThan(2 * boss.Tuning.RadialRange * Mathf.Sin(Mathf.PI / boss.Tuning.RadialCount)));
            boss.BeginPattern(DragonPattern.RadialFireballs);
            yield return Wait(2.6f);
            Assert.That(hits, Is.EqualTo(1), "겹친 대형 탄 여러 개가 중복 피해를 주지 않음");

            boss.Tuning.RadialCount = 24;
            boss.Tuning.RadialRadius = 2;
            boss.Tuning.RadialSpeed = 18;
            boss.BeginPattern(DragonPattern.RadialFireballs);
            yield return Wait(1.43f);
            BossFireball[] balls = boss.Effects.GetComponentsInChildren<BossFireball>();
            Assert.That(balls.Length, Is.EqualTo(24));
            Assert.That(balls.All(ball => ball.Radius == 2), Is.True);
            Vector3 start = balls[0].transform.position;
            float since = Time.time;
            yield return Wait(0.05f);
            Assert.That(Vector3.Distance(start, balls[0].transform.position) / (Time.time - since), Is.EqualTo(18).Within(0.5f));
        }
    }
}
