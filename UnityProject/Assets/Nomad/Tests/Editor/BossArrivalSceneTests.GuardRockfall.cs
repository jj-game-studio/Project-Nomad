using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Monsters.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed partial class BossArrivalSceneTests
    {
        [UnityTest]
        public IEnumerator FilledStraightLanesAndBossPulseShareTheGuardClockWithoutPlayerCircle()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            PlaceHero(scene.Arena.Center - Vector3.forward * 7);
            boss.BeginPattern(DragonPattern.RadialFireballs);
            yield return Wait(0.3f);
            var signal = boss.GetComponent<BossParrySignal>();
            var lanes = boss.Effects.GetComponentInChildren<BossParryRangeWarning>();
            Assert.That(lanes.LaneCount, Is.EqualTo(12));
            Assert.That(GameObject.Find("Player parry timing arc"), Is.Null);
            while (Time.time < signal.FlashAt + 0.02f) yield return null;
            Assert.That(lanes.Progress, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<BossGuardPulse>(), Is.Not.Null);
            int hp = scene.Player.GetComponent<Health>().CurrentHealth;
            scene.GodMode = false;
            scene.Player.GetComponent<Health>().IsCombatImmune = false;
            scene.Player.transform.rotation = Quaternion.LookRotation(boss.transform.position - scene.Player.transform.position);
            yield return Wait(0.25f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return Wait(0.9f);
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().Reflections, Is.EqualTo(1));
            Assert.That(scene.Player.GetComponent<Health>().CurrentHealth, Is.EqualTo(hp));
            yield break;
        }

        [UnityTest]
        public IEnumerator StraightLanesMatchOnlyProjectilePaths()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            PlaceHero(scene.Arena.Center - Vector3.forward * 7);
            boss.BeginPattern(DragonPattern.RadialFireballs);
            yield return Wait(1.4f);
            var lanes = boss.Effects.GetComponentInChildren<BossParryRangeWarning>();
            Mesh mesh = lanes.GetComponent<MeshFilter>().sharedMesh;
            Vector3 forward = boss.transform.forward;
            Assert.That(MeshCovers(lanes.transform, mesh, boss.transform.position + forward * 12), Is.True);
            Assert.That(MeshCovers(lanes.transform, mesh, boss.transform.position + Quaternion.Euler(0, 15, 0) * forward * 12), Is.False, "구체 사이 안전 구간을 칠하지 않음");
        }

        private static bool MeshCovers(Transform root, Mesh mesh, Vector3 point)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = root.TransformPoint(vertices[triangles[index]]), b = root.TransformPoint(vertices[triangles[index + 1]]), c = root.TransformPoint(vertices[triangles[index + 2]]);
                float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
                if ((ab >= -0.001f && bc >= -0.001f && ca >= -0.001f) || (ab <= 0.001f && bc <= 0.001f && ca <= 0.001f)) return true;
            }
            return false;
        }

        private static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;

        [UnityTest]
        public IEnumerator SlamFillsYellowAtBossPulseAndLeavesLargeVisibleCracksWithoutRocks()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.Circle);
            var signal = boss.GetComponent<BossParrySignal>();
            var warning = boss.Effects.GetComponentInChildren<BossHazard>();
            Assert.That(warning.WarningColor.g, Is.GreaterThan(0.7f));
            while (Time.time < signal.FlashAt + 0.02f) yield return null;
            Assert.That(Object.FindFirstObjectByType<BossGuardPulse>(), Is.Not.Null);
            Mesh mesh = warning.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertices.Max(vertex => new Vector2(vertex.x, vertex.z).magnitude), Is.EqualTo(18).Within(0.02f));
            yield return Wait(0.7f);
            Assert.That(scene.Arena.RockCount, Is.Zero);
            var cracks = boss.Effects.GetComponentInChildren<BossGroundCracks>();
            Assert.That(cracks, Is.Not.Null);
            var lines = cracks.GetComponentsInChildren<LineRenderer>().Where(line => line.name == "Broken stone seam").ToArray();
            Assert.That(lines.Length, Is.EqualTo(16));
            Assert.That(lines[0].startWidth, Is.GreaterThan(0.6f));
            Assert.That(lines[0].GetPosition(4).y - scene.Arena.GroundHeight(lines[0].GetPosition(4)), Is.GreaterThan(0.15f));
        }

        [UnityTest]
        public IEnumerator RockfallWarnsDamagesThenCreatesOnlySixSolidBouldersAndStoneDebris()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            scene.Player.GetComponent<PlayerStats>().RandomSource = () => 0.99f;
            Health health = scene.Player.GetComponent<Health>();
            scene.GodMode = false;
            health.IsCombatImmune = false;
            PlaceHero(scene.Arena.Center - Vector3.forward * 10);
            int hp = health.CurrentHealth;
            Assert.That(scene.Arena.RockCount, Is.Zero);
            boss.BeginPattern(DragonPattern.Rockfall);
            yield return Wait(0.5f);
            Assert.That(scene.Arena.RockCount, Is.Zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(hp));
            Assert.That(scene.Arena.GetComponentInChildren<BossFallingRock>(), Is.Not.Null);
            yield return Wait(1.9f);
            Assert.That(health.CurrentHealth, Is.LessThan(hp));
            Assert.That(scene.Arena.RockCount, Is.InRange(1, 2));
            var rock = scene.Arena.GetComponentInChildren<BossBreakableRock>();
            Assert.That(rock.GetComponentInChildren<NavMeshObstacle>().enabled, Is.True);
            for (int count = 0; count < 6 && scene.Arena.RockCount < 6; count++)
            {
                boss.BeginPattern(DragonPattern.Rockfall);
                yield return Wait(2.3f);
                Assert.That(scene.Arena.RockCount, Is.InRange(1, 6));
            }
            int attacks = boss.AttacksStarted;
            boss.BeginPattern(DragonPattern.Rockfall);
            yield return Wait(2);
            Assert.That(boss.AttacksStarted, Is.EqualTo(attacks));
            Assert.That(scene.Arena.RockCount, Is.EqualTo(6));
            Assert.That(rock.Shatter(), Is.True);
            Assert.That(scene.Arena.RockCount, Is.EqualTo(5));
            var debris = scene.Arena.GetComponentsInChildren<BossStoneDebris>();
            Assert.That(debris.Length, Is.GreaterThan(0));
            Assert.That(debris.Last().GetComponentsInChildren<MeshRenderer>().All(renderer => renderer.sharedMaterial.shader.name == "Nomad/Volcanic Stone"), Is.True);
        }

        [UnityTest]
        public IEnumerator BreathHopsBackBeforeWarningAndUsesExpandedRange()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            PlaceHero(scene.Arena.Center - Vector3.forward * 5);
            Vector3 start = boss.transform.position;
            boss.BeginPattern(DragonPattern.Breath);
            yield return Wait(0.2f);
            var hop = boss.GetComponent<BossBreathBackstep>();
            Assert.That(hop.Airborne, Is.True);
            Assert.That(boss.transform.Find("Body").localPosition.y, Is.GreaterThan(1.5f));
            yield return Wait(0.4f);
            Assert.That(hop.Landings, Is.EqualTo(1));
            Assert.That(hop.Airborne, Is.False);
            Assert.That(Vector3.Distance(start, boss.transform.position), Is.GreaterThan(3));
            var breath = boss.Effects.GetComponentInChildren<BossHazard>();
            Assert.That(breath, Is.Not.Null);
            Assert.That(breath.Radius, Is.EqualTo(22));
            Assert.That(breath.Contains(boss.transform.position + boss.transform.forward * 2), Is.False);
            Assert.That(breath.Contains(boss.transform.position + boss.transform.forward * 21), Is.True);
            Assert.That(breath.Contains(boss.transform.position + boss.transform.forward * 22.1f), Is.False);
        }

        [UnityTest]
        public IEnumerator ThreeBlastsAlternateFastAndCloseOrbitFollowsBossAndPushesPlayer()
        {
            var boss = scene.Boss;
            boss.GetComponent<BossArrival>().Skip();
            boss.BeginPattern(DragonPattern.OrbitFlames);
            var sequence = boss.Effects.GetComponentInChildren<BossRingSequence>();
            float timeout = Time.time + 5;
            for (int round = 1; round <= 3; round++)
            {
                while (sequence != null && sequence.RoundsReleased < round && Time.time < timeout) yield return null;
                Assert.That(sequence != null && sequence.RoundsReleased == round, Is.True);
                yield return Wait(0.04f);
                AssertBandHit(2, round % 2 == 1); AssertBandHit(5, round % 2 == 0);
                var active = boss.Effects.GetComponentsInChildren<BossHazard>().Where(hazard => hazard.Active).ToArray();
                Assert.That(active.Length, Is.EqualTo(3));
                Assert.That(active.All(hazard => hazard.HighlightActive), Is.True);
                yield return Wait(0.22f);
            }
            var orbit = scene.Arena.GetComponentInChildren<BossOrbitingFlames>();
            Assert.That(orbit.OrbCount, Is.EqualTo(3));
            orbit.enabled = true;
            scene.Player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            boss.GetComponent<NavMeshAgent>().Warp(scene.Arena.Center + Vector3.right * 2);
            yield return null;
            foreach (Transform orb in orbit.transform)
                Assert.That(Vector3.ProjectOnPlane(orb.position - boss.transform.position, Vector3.up).magnitude, Is.EqualTo(3.65f).Within(0.05f));
            scene.GodMode = false;
            scene.Player.GetComponent<Health>().IsCombatImmune = false;
            PlaceHero(orbit.transform.GetChild(0).position - Vector3.up * 1.1f);
            Vector3 before = scene.Player.transform.position;
            yield return Wait(0.5f);
            Assert.That(Vector3.Distance(before, scene.Player.transform.position), Is.GreaterThan(5), "구체 접촉은 큰 밀침");
            boss.Stun(0.5f);
            yield return null;
            Assert.That(orbit.transform.GetChild(0).gameObject.activeSelf, Is.False, "녹다운 딜타임은 구체로 방해하지 않음");
        }

        private void AssertBandHit(float distance, bool expected)
        {
            PlaceHero(scene.Boss.transform.position - Vector3.forward * distance);
            var bands = scene.Boss.Effects.GetComponentsInChildren<BossHazard>().Where(hazard => hazard.Active);
            Assert.That(bands.Any(band => band.Contains(scene.Player.transform.position)), Is.EqualTo(expected), "거리 " + distance);
        }
    }
}
