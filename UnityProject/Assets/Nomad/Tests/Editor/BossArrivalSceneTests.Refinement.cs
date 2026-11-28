using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed partial class BossArrivalSceneTests
    {
        [UnityTest]
        public IEnumerator FallingRocksContinueDuringRainWithoutReplacingItAndReserveSixSlots()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.Tuning.RockfallInterval = 0.4f;
            boss.AutomaticRockfall = true;
            boss.BeginPattern(DragonPattern.Rain);
            int attacks = boss.AttacksStarted;
            boss.DropRocks();
            Assert.That(boss.PendingRocks, Is.InRange(1, 2));
            float until = Time.time + 4;
            while (Time.time < until)
            {
                Assert.That(scene.Arena.RockCount + boss.PendingRocks, Is.LessThanOrEqualTo(6));
                Assert.That(boss.Pattern, Is.EqualTo(DragonPattern.Rain));
                Assert.That(boss.AttacksStarted, Is.EqualTo(attacks));
                yield return null;
            }
            Assert.That(scene.Arena.RockCount, Is.InRange(3, 6));
            Assert.That(boss.RainProjectilesSpawned, Is.GreaterThanOrEqualTo(18));
        }

        [UnityTest]
        public IEnumerator EruptionUnlocksOnlyAfterArmorBreakWarnsThenDamagesAtTwoOrThreeLocations()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.LavaEruption);
            Assert.That(boss.AttacksStarted, Is.Zero);
            for (int index = 0; index < 100; index++) Assert.That(boss.ChooseNextPattern(7), Is.Not.EqualTo(DragonPattern.LavaEruption));
            boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
            yield return Wait(3.3f);
            Assert.That(boss.Phase, Is.Not.EqualTo(DragonPhase.Stunned));
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            PlaceHero(scene.Arena.Center - Vector3.forward * 8);
            scene.GodMode = false;
            Health health = scene.Player.GetComponent<Health>();
            health.IsCombatImmune = false;
            int hp = health.CurrentHealth;
            boss.BeginPattern(DragonPattern.LavaEruption);
            var columns = boss.Effects.GetComponentsInChildren<BossLavaEruption>();
            Assert.That(columns.Length, Is.InRange(4, 5));
            PlaceHero(columns[0].transform.position);
            foreach (var column in columns)
                foreach (var other in columns)
                    if (column != other) Assert.That(Vector3.Distance(column.transform.position, other.transform.position), Is.GreaterThanOrEqualTo(5.9f));
            yield return Wait(0.6f);
            Assert.That(health.CurrentHealth, Is.EqualTo(hp));
            yield return Wait(0.5f);
            Assert.That(health.CurrentHealth, Is.LessThan(hp));
            Assert.That(columns.All(column => column.JetCount == 3), Is.True, "각 분출은 세 갈래 수직 기둥");
            Assert.That(boss.Effects.GetComponentsInChildren<BossHazard>().Count(hazard => hazard.EmitsFlame), Is.EqualTo(columns.Length), "분출 뒤에는 별도 용암 지대가 남음");
            Assert.That(columns.All(column => column.GetComponentsInChildren<ParticleSystem>().Any(system => system.name == "Molten eruption jet" && system.particleCount > 0)), Is.True);
            yield return Wait(0.9f);
            Assert.That(boss.Effects.GetComponentsInChildren<BossHazard>().Count(hazard => hazard.Active && hazard.EmitsFlame), Is.InRange(4, 5));
        }
        [UnityTest]
        public IEnumerator FireRainHasVisibleMeteorBodiesLongTailsAndImpactEjecta()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.Rain);
            yield return Wait(1.05f);
            var meteors = boss.Effects.GetComponentsInChildren<BossFireball>().Where(ball => ball.IsMeteor).ToArray();
            Assert.That(meteors.Length, Is.GreaterThanOrEqualTo(3));
            foreach (var meteor in meteors)
            {
                Assert.That(meteor.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.sharedMaterial.shader.name == "Nomad/Molten Rock"), Is.True);
                Assert.That(meteor.GetComponent<TrailRenderer>().sharedMaterial.shader.name, Is.EqualTo("Nomad/Soft Comet Flame"));
                Assert.That(meteor.GetComponent<TrailRenderer>().positionCount, Is.GreaterThan(2), "움직임을 따라 부드럽게 이어지는 화염 꼬리");
            }
            yield return Wait(0.8f);
            Assert.That(boss.Effects.GetComponentsInChildren<BossMeteorImpact>().Length, Is.GreaterThan(0));
        }

    }
}
