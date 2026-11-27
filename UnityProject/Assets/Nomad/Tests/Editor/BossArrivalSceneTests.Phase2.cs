using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
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
        public IEnumerator CapsuleFallbackFallsWithoutTiltingThePhysicsRootAndRestores()
        {
            foreach (bool childMesh in new[] { false, true })
            {
                GameObject root = childMesh ? new GameObject("Capsule test root") : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.transform.position = new Vector3(200, 4, 200);
                if (childMesh)
                {
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.transform.SetParent(root.transform, false);
                    capsule.transform.localPosition = Vector3.up * 0.9f;
                }
                Health health = root.AddComponent<Health>();
                float height = root.GetComponentInChildren<MeshRenderer>().bounds.size.y;
                var reaction = PlayerBossReaction.Play(health, root.transform.position + Vector3.forward, 0, 2);
                yield return Wait(0.6f);
                Assert.That(reaction.HasVisual, Is.True);
                Assert.That(reaction.KnockdownWeight, Is.GreaterThan(0.9f));
                Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.identity), Is.LessThan(0.01f));
                var renderer = root.GetComponentsInChildren<MeshRenderer>().First(body => body.enabled);
                Assert.That(renderer.bounds.size.y, Is.LessThan(height * 0.9f));
                reaction.Cancel();
                yield return null;
                Assert.That(reaction.Locked, Is.False);
                Assert.That(renderer.bounds.size.y, Is.EqualTo(height).Within(0.02f));
                Object.DestroyImmediate(root);
            }
        }

        private void PlaceHero(Vector3 point)
        {
            var controller = scene.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            scene.Player.transform.position = scene.Arena.GroundPoint(point) + Vector3.up * 0.03f;
            controller.enabled = true;
            scene.Player.transform.forward = Vector3.ProjectOnPlane(scene.Boss.transform.position - point, Vector3.up).normalized;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator SuccessfulRadialParryProtectsWholeVolleyAndDoesNotDamageBoss()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            scene.GodMode = false;
            Health playerHealth = scene.Player.GetComponent<Health>();
            playerHealth.IsCombatImmune = false;
            scene.Player.GetComponent<PlayerStats>().RandomSource = () => 0.99f;
            PlaceHero(scene.Arena.Center - Vector3.forward * 6);
            int hp = playerHealth.CurrentHealth, bossHp = boss.GetComponent<Health>().CurrentHealth;
            float armor = boss.GetComponent<BossArmor>().Durability;
            boss.BeginPattern(DragonPattern.RadialFireballs);
            yield return Wait(0.4f);
            var warning = boss.Effects.GetComponentInChildren<BossParryRangeWarning>();
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Progress, Is.InRange(0.1f, 0.5f));
            float progress = warning.Progress;
            yield return Wait(0.3f);
            Assert.That(warning.Progress, Is.GreaterThan(progress));

            var signal = boss.GetComponent<BossParrySignal>();
            while (!signal.Flashing) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(1.1f);
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().Reflections, Is.EqualTo(1));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(hp));
            Assert.That(boss.GetComponent<Health>().CurrentHealth, Is.EqualTo(bossHp));
            Assert.That(boss.GetComponent<BossArmor>().Durability, Is.LessThan(armor));

            boss.HitRadialWave(20, 900);
            boss.ProtectVolley(900);
            yield return null;
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(hp), "같은 프레임 선행 접촉도 취소");
            boss.HitRadialWave(20, 901);
            yield return null;
            Assert.That(playerHealth.CurrentHealth, Is.LessThan(hp), "다음 발사 묶음은 별도로 방어해야 함");
        }

        [UnityTest]
        public IEnumerator BodySweepKnocksPlayerAwayButFPreventsDamageAndPush()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.GodMode = false;
            Health health = scene.Player.GetComponent<Health>();
            health.IsCombatImmune = false;
            scene.Player.GetComponent<PlayerStats>().RandomSource = () => 0.99f;
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            PlaceHero(scene.Arena.Center - Vector3.forward * 4);
            Vector3 before = scene.Player.transform.position;
            int hp = health.CurrentHealth;
            boss.BeginPattern(DragonPattern.BodySweep);
            yield return Wait(1.6f);
            Assert.That(health.CurrentHealth, Is.LessThan(hp));
            Assert.That(scene.Player.GetComponent<PlayerBossReaction>().Locked, Is.True);
            Assert.That(Vector3.Distance(before, scene.Player.transform.position), Is.GreaterThan(1));
            yield return Wait(1.8f);
            Assert.That(scene.Player.GetComponent<PlayerBossReaction>().Locked, Is.False);

            PlaceHero(scene.Arena.Center - Vector3.forward * 4);
            before = scene.Player.transform.position;
            hp = health.CurrentHealth;
            int bossHp = boss.GetComponent<Health>().CurrentHealth;
            boss.BeginPattern(DragonPattern.BodySweep);
            while (!boss.GetComponent<BossParrySignal>().Flashing) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(0.65f);
            Assert.That(health.CurrentHealth, Is.EqualTo(hp));
            Assert.That(Vector3.Distance(before, scene.Player.transform.position), Is.LessThan(0.1f));
            Assert.That(scene.Player.GetComponent<PlayerBossReaction>().Locked, Is.False);
            Assert.That(boss.GetComponent<Health>().CurrentHealth, Is.EqualTo(bossHp));
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().Successes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EnrageRoarsPushesAndKnocksDownThenUnlocksPhaseTwo()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            PlaceHero(scene.Arena.Center - Vector3.forward * 5);
            Health health = boss.GetComponent<Health>();
            health.TryApplyDamage(health.CurrentHealth - health.MaxHealth / 4, DamageKind.Fall);
            yield return Wait(0.2f);
            Assert.That(boss.EnrageTransitioning, Is.True);
            Assert.That(boss.Enraged, Is.False);
            Assert.That(scene.Player.InputBlocked, Is.True);
            Vector2 pointer = Camera.main.WorldToScreenPoint(boss.transform.position + Vector3.up);
            Assert.That(scene.Player.TryAttack(pointer), Is.False);
            Assert.That(scene.Player.TryUseSkill(pointer), Is.False);
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().TryBegin(), Is.False);
            Assert.That(health.TryApplyCombatDamage(200, false), Is.False, "공중에 남아 있던 탄도 전환 중 공격 불가");
            float redness = boss.EnrageAmount;
            Vector3 before = scene.Player.transform.position;
            yield return Wait(1.6f);
            Assert.That(boss.EnrageAmount, Is.GreaterThan(redness));
            Assert.That(scene.Player.GetComponent<PlayerBossReaction>().KnockdownWeight, Is.GreaterThan(0.8f));
            Assert.That(Vector3.Distance(before, scene.Player.transform.position), Is.GreaterThan(2));
            Assert.That(boss.GetComponent<AudioSource>().isPlaying, Is.True);
            yield return Wait(2.6f);
            Assert.That(boss.Enraged, Is.True);
            Assert.That(boss.EnrageTransitioning, Is.False);
            Assert.That(scene.Player.InputBlocked, Is.False);
            Assert.That(scene.Player.GetComponent<PlayerBossReaction>().Locked, Is.False);
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(text => text.text.Contains("2페이즈")), Is.True);
            Canvas.ForceUpdateCanvases();
            var flames = boss.GetComponentInChildren<BossHealthFlameGraphic>();
            Assert.That(flames.Intensity, Is.GreaterThan(0.5f));
            Assert.That(flames.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
            boss.BeginPattern(DragonPattern.Breath);
            Assert.That(boss.Phase, Is.EqualTo(DragonPhase.Windup));
        }

        [UnityTest]
        public IEnumerator LavaWarningAndDamageUseTheSameUnevenBoundary()
        {
            scene.Boss.GetComponent<BossArrival>().Skip();
            foreach (BossBreakableRock rock in scene.Arena.GetComponentsInChildren<BossBreakableRock>()) Object.DestroyImmediate(rock.gameObject);
            scene.Boss.BeginPattern(DragonPattern.FirePool);
            yield return Wait(1.6f);
            var pool = scene.Boss.Effects.GetComponentsInChildren<BossHazard>().First();
            Assert.That(pool.Irregular, Is.True);
            float[] edges = Enumerable.Range(0, 36).Select(index => pool.EdgeRadius(Quaternion.Euler(0, index * 10, 0) * Vector3.forward)).ToArray();
            Assert.That(edges.Max() - edges.Min(), Is.GreaterThan(0.4f));
            for (int index = 0; index < 12; index++)
            {
                Vector3 heading = Quaternion.Euler(0, index * 30, 0) * Vector3.forward;
                float edge = pool.EdgeRadius(heading);
                PlaceHero(pool.transform.position + heading * (edge - 0.08f));
                Assert.That(pool.Contains(scene.Player.transform.position), Is.True);
                PlaceHero(pool.transform.position + heading * (edge + 0.08f));
                Assert.That(pool.Contains(scene.Player.transform.position), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RandomSelectionAvoidsRepeatsAndRespectsChargeAndMeleeRange()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.Circle);
            var choices = Enumerable.Range(0, 200).Select(_ => boss.ChooseNextPattern(3)).ToArray();
            Assert.That(choices.Distinct().Count(), Is.GreaterThanOrEqualTo(5));
            Assert.That(choices, Has.None.EqualTo(DragonPattern.Circle));
            Assert.That(choices, Has.None.EqualTo(DragonPattern.Charge), "34초 이전 돌진 후보 제외");
            Assert.That(choices, Has.Some.EqualTo(DragonPattern.BodySweep));
            Assert.That(Enumerable.Range(0, 100).Select(_ => boss.ChooseNextPattern(12)), Has.None.EqualTo(DragonPattern.BodySweep));
            Assert.That(boss.Tuning.SlamRadius, Is.EqualTo(18));
            Assert.That(boss.Effects.GetComponentInChildren<BossHazard>().Radius, Is.EqualTo(18));
            yield return null;
        }
    }
}
