using System.Collections;
using System.Linq;
using System.Reflection;
using Nomad.Combat;
using Nomad.Loot;
using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed partial class BossArrivalSceneTests
    {
        [UnityTest]
        public IEnumerator EnragedRainKeepsNormalFallTimeAndOnlyChanceCreatesLava()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.GodMode = true;
            boss.Tuning.RainWaves = 1;
            boss.Tuning.RainPerWave = 1;
            boss.Tuning.EnragedRainPoolChance = 0;
            for (int pass = 0; pass < 3; pass++)
            {
                if (pass == 1)
                {
                    Health health = boss.GetComponent<Health>();
                    health.TryApplyDamage(health.CurrentHealth - health.MaxHealth / 4, DamageKind.Fall);
                    yield return Wait(BossEnrageTransition.Seconds + 0.1f);
                }
                if (pass == 2) boss.Tuning.EnragedRainPoolChance = 1;

                boss.BeginPattern(DragonPattern.Rain);
                float timeout = Time.time + 3;
                while ((boss.Phase != DragonPhase.Active || boss.RainProjectilesSpawned < 1) && Time.time < timeout) yield return null;
                Assert.That(boss.RainProjectilesSpawned, Is.EqualTo(1));
                var ball = boss.Effects.GetComponentInChildren<BossFireball>();
                Assert.That(ball, Is.Not.Null);
                float seconds = (float)typeof(BossFireball).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ball);
                Assert.That(seconds, Is.EqualTo(0.85f).Within(0.001f), "일반/광폭 낙하 속도 동일");
                yield return Wait(1.1f);
                BossHazard[] pools = boss.Effects.GetComponentsInChildren<BossHazard>().Where(hazard => hazard.Radius == 1.8f).ToArray();
                Assert.That(pools.Length, Is.EqualTo(pass == 2 ? 1 : 0));
                if (pass == 2) Assert.That(pools[0].GetComponentInChildren<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("Nomad/Lava Pool"));
            }
        }

        [UnityTest]
        public IEnumerator BreathParticlesAndWarningRespectTheSafeGapAndRange()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.Breath);
            yield return Wait(0.8f);
            BossHazard breath = boss.Effects.GetComponentInChildren<BossHazard>();
            MeshFilter warning = breath.GetComponentInChildren<MeshFilter>();
            foreach (Vector3 vertex in warning.sharedMesh.vertices)
                Assert.That(new Vector2(vertex.x, vertex.z).magnitude, Is.GreaterThanOrEqualTo(breath.InnerRadius - 0.01f), "전조도 안전 구간을 칠하지 않음");
            yield return Wait(2);
            ParticleSystem flame = breath.GetComponentsInChildren<ParticleSystem>().First(system => system.name == "Burning embers");
            var particles = new ParticleSystem.Particle[1024];
            int count = flame.GetParticles(particles);
            Assert.That(count, Is.GreaterThan(30));
            for (int index = 0; index < count; index++)
            {
                float radius = Vector3.ProjectOnPlane(particles[index].position - breath.transform.position, Vector3.up).magnitude;
                Assert.That(radius, Is.InRange(breath.InnerRadius + 0.1f, breath.Radius - 0.1f));
            }
        }

        [UnityTest]
        public IEnumerator SlamCameraImpulseMovesRenderCameraAndRestoresItsPosition()
        {
            scene.Boss.GetComponent<BossArrival>().Skip();
            scene.Boss.BeginPattern(DragonPattern.Circle);
            float timeout = Time.time + 4;
            while (scene.Boss.GetComponent<BossJumpSlam>().Landings == 0 && Time.time < timeout) yield return null;
            var camera = Camera.main;
            var impulse = camera.GetComponent<Nomad.Monsters.HitCameraImpulse>();
            Assert.That(impulse, Is.Not.Null);
            Vector3 original = camera.transform.position;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var context = new UnityEngine.Rendering.ScriptableRenderContext();
            typeof(Nomad.Monsters.HitCameraImpulse).GetMethod("Begin", flags).Invoke(impulse, new object[] { context, camera });
            Assert.That(Vector3.Distance(camera.transform.position, original), Is.GreaterThan(0.01f));
            typeof(Nomad.Monsters.HitCameraImpulse).GetMethod("End", flags).Invoke(impulse, new object[] { context, camera });
            Assert.That(Vector3.Distance(camera.transform.position, original), Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator StonePavingCartoonPlayerAndBossTreasureExistInActualScene()
        {
            scene.Boss.GetComponent<BossArrival>().Skip();
            scene.Arena.LandRock(scene.Arena.Center + new Vector3(12, 0, 12));
            yield return Wait(0.6f);
            var paving = GameObject.Find("Hand laid volcanic stone paving").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(paving.vertexCount, Is.GreaterThan(1500));
            Assert.That(paving.normals.Count(normal => normal.y > 0.9f), Is.GreaterThan(500));
            Assert.That(scene.Player.GetComponent<CartoonSurfaceStyle>(), Is.Not.Null);
            var rockMaterial = scene.Arena.GetComponentsInChildren<BossBreakableRock>()[0].GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(rockMaterial.mainTexture, Is.Not.Null, "Synty _Albedo_Map 텍스처 보존");
            Assert.That(scene.Player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(renderer => renderer.sharedMaterials.Any(material => material.shader.name == "Nomad/Boss Cel")), Is.True);
            scene.Boss.GetComponent<Health>().TryApplyDamage(BossArmor.ReferenceHealth * 2, DamageKind.Fall);
            yield return null;
            var chests = Object.FindObjectsByType<BossTreasurePresentation>(FindObjectsSortMode.None);
            Assert.That(chests.Length, Is.EqualTo(1), "사망 시 보상 상자 하나");
            Assert.That(chests[0].transform.Find("Closed/Gilded royal treasure chest"), Is.Not.Null);
            Assert.That(chests[0].GetComponent<BoxCollider>().bounds.size.x, Is.GreaterThan(2.5f));
            Assert.That(chests[0].GetComponent<LootChest>().Status, Is.EqualTo(LootStatus.Closed));
        }
    }
}
