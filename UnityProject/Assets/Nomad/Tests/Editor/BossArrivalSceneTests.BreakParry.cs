using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nomad.Tests
{
    public sealed partial class BossArrivalSceneTests
    {
        [UnityTest]
        public IEnumerator SlamRejectsBufferedFAndAcceptsFreshDescendingInput()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            PlaceHero(scene.Arena.Center - Vector3.forward * 6);
            scene.GodMode = false;
            Health hp = scene.Player.GetComponent<Health>(); hp.IsCombatImmune = false;
            scene.Player.GetComponent<PlayerStats>().RandomSource = () => 0.99f;
            var parry = scene.Player.GetComponent<PlayerBossParry>();
            var signal = boss.GetComponent<BossParrySignal>();
            boss.BeginPattern(DragonPattern.Circle);
            while (Time.time < signal.ImpactAt - 0.4f) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(1);
            Assert.That(parry.Successes, Is.Zero, "미리 누른 가드를 착지에 넘기지 않음");
            Assert.That(hp.CurrentHealth, Is.LessThan(hp.MaxHealth));

            int before = hp.CurrentHealth;
            boss.BeginPattern(DragonPattern.Circle);
            while (Time.time < signal.ImpactAt - 0.15f) yield return null;
            Assert.That(boss.GetComponent<BossJumpSlam>().Airborne, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(1);
            Assert.That(parry.Successes, Is.EqualTo(1));
            Assert.That(hp.CurrentHealth, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator RadialContactAllowsLateFWithoutDamageAndOnlyOneReflection()
        {
            var boss = scene.Boss; boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            scene.Player.transform.rotation = Quaternion.identity;
            Health hp = scene.Player.GetComponent<Health>(); hp.IsCombatImmune = false; scene.GodMode = false;
            int before = hp.CurrentHealth;
            Vector3 center = scene.Player.transform.position + Vector3.up * 1.3f;
            var ball = BossFireball.Launch(boss.Effects, boss.transform, hp, center + Vector3.forward, center - Vector3.forward, 0.05f, false, radius:1);
            ball.SignalControlled = true; ball.VolleyId = 777;
            yield return Wait(0.06f);
            Assert.That(hp.CurrentHealth, Is.EqualTo(before));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(0.7f);
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().Reflections, Is.EqualTo(1));
            Assert.That(hp.CurrentHealth, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator TwoPhysicalShellsAndHudLayersBreakAndOrbitEndsOnFirstBreak()
        {
            var boss = scene.Boss; boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.BodySweep);
            yield return null;
            var armor = boss.GetComponent<BossArmor>(); var shell = boss.GetComponent<BossArmorShell>();
            Assert.That(shell.LayersVisible, Is.EqualTo(2));
            Assert.That(boss.GetComponentsInChildren<Transform>().Count(t => t.name == "Outer fitted carapace"), Is.GreaterThanOrEqualTo(1));
            armor.DamageArmor(BossArmor.DurabilityPerLayer);
            yield return null;
            Assert.That(scene.Arena.GetComponentInChildren<BossOrbitingFlames>(), Is.Null);
            Assert.That(shell.LayersVisible, Is.EqualTo(1));
            Assert.That(boss.GetComponentsInChildren<Transform>().Any(t => t.name == "Outer fitted carapace"), Is.False);
            Assert.That(boss.GetComponentsInChildren<Text>().Any(t => t.text.Contains("내피") && t.text.Contains("1/2")), Is.True);
            Assert.That(armor.ModifyCombatDamage(100), Is.EqualTo(110));
            armor.DamageArmor(BossArmor.DurabilityPerLayer);
            yield return null;
            Assert.That(shell.LayersVisible, Is.Zero);
            Assert.That(armor.ModifyCombatDamage(100), Is.EqualTo(220));
        }

        [UnityTest]
        public IEnumerator ParrySlowMotionRestoresAfterPauseResumes()
        {
            float scale = Time.timeScale, fixedStep = Time.fixedDeltaTime;
            try
            {
                BossParrySlowMotion.Pulse();
                Assert.That(Time.timeScale, Is.EqualTo(BossParrySlowMotion.Scale));
                Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(0.22f);
                Assert.That(Time.timeScale, Is.Zero, "일시정지를 슬로모션 종료가 해제하지 않음");
                Time.timeScale = BossParrySlowMotion.Scale;
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(scale));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(0.0000001f));
            }
            finally { Time.timeScale = scale; Time.fixedDeltaTime = fixedStep; }
        }
    }
}
