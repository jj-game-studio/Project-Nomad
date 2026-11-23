#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Nomad.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterTestPanelTests
    {
        private Scene previous;
        private Scene scene;
        private MonoBehaviour panel;
        private Health player;

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
                if (root.name == "Test Controls")
                    panel = (MonoBehaviour)root.GetComponent(Type.GetType("Nomad.Monsters.Demo.MonsterTestPanel, Assembly-CSharp"));
                if (root.name == "Player")
                    player = root.GetComponent<Health>();
            }
            Assert.That(panel, Is.Not.Null);
            yield return null;
            Call("ClearMonsters");
            Set(Settings(1), "skills", false);
            Set(Settings(2), "skills", false);
            yield return null;
            foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
            {
                if (component.GetType().FullName == "Nomad.Player.PlayerMovement")
                    component.enabled = false;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator RangedWindupLaunchesRealProjectileAndHitsPlayerOnce()
        {
            player.transform.position = new Vector3(3f, 0f, 4f);
            Call("SetKind", 0);
            Set(Settings(0), "normalStyle", 1);
            Health actor = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            actor.GetComponent<NavMeshAgent>().speed = 0f;
            MonsterRangedAttack ranged = actor.GetComponent<MonsterRangedAttack>();
            Assert.That(ranged.Visual, Is.Not.Null);
            float deadline = Time.time + 4f;
            while (ranged.ShotsFired == 0 && Time.time < deadline) yield return null;
            Assert.That(ranged.ShotsFired, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.EqualTo(100), "damage must wait for projectile travel");
            ranged.enabled = false;
            while (player.CurrentHealth == 100 && Time.time < deadline) yield return null;
            Assert.That(player.CurrentHealth, Is.EqualTo(85));
            yield return new WaitForSeconds(0.2f);
            Assert.That(player.CurrentHealth, Is.EqualTo(85), "one impact only");
        }

        [UnityTest]
        public IEnumerator RangedAimLocksAndPlayerCanDodgeDuringWindup()
        {
            player.transform.position = new Vector3(3f, 0f, 4f);
            Health actor = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            actor.GetComponent<NavMeshAgent>().speed = 0f;
            var attack = actor.GetComponent<MonsterMeleeAttack>();
            var ranged = actor.GetComponent<MonsterRangedAttack>();
            float deadline = Time.time + 4f;
            while (attack.Phase != MeleePhase.Windup && Time.time < deadline) yield return null;
            Assert.That(attack.IsRangedAttack, Is.True);
            player.transform.position += Vector3.right * 3f;
            while (ranged.ShotsFired == 0 && Time.time < deadline) yield return null;
            Assert.That(ranged.ShotsFired, Is.EqualTo(1));
            ranged.enabled = false;
            yield return new WaitForSeconds(1f);
            Assert.That(player.CurrentHealth, Is.EqualTo(100), "locked aim must not home onto dodging player");
        }

        [UnityTest]
        public IEnumerator RangedProjectileIsBlockedByCoverAndDeathCancelsPendingShot()
        {
            player.transform.position = new Vector3(3f, 0f, 4f);
            Health actor = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            actor.GetComponent<NavMeshAgent>().speed = 0f;
            var attack = actor.GetComponent<MonsterMeleeAttack>();
            var ranged = actor.GetComponent<MonsterRangedAttack>();
            float deadline = Time.time + 4f;
            while (attack.Phase != MeleePhase.Windup && Time.time < deadline) yield return null;
            Assert.That(attack.IsRangedAttack, Is.True);
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.position = new Vector3(3f, 1.5f, 1f);
            cover.transform.localScale = new Vector3(2f, 3f, 0.3f);
            while (ranged.ShotsFired == 0 && Time.time < deadline) yield return null;
            Assert.That(ranged.ShotsFired, Is.EqualTo(1));
            yield return new WaitForSeconds(0.8f);
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            UnityEngine.Object.Destroy(cover);
            while (attack.Phase != MeleePhase.Windup && Time.time < deadline + 3f) yield return null;
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Windup));
            int shots = ranged.ShotsFired;
            actor.TryApplyDamage(10000);
            yield return new WaitForSeconds(1.5f);
            Assert.That(ranged.ShotsFired, Is.EqualTo(shots));
        }

        [UnityTest]
        public IEnumerator GolemBonesWalkAndDieAndDragonHasLargerBodyAndDamageTrail()
        {
            Call("SetKind", 1);
            Health golem = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            golem.GetComponent<MonsterMeleeAttack>().enabled = false;
            golem.GetComponent<MonsterRangedAttack>().enabled = false;
            yield return null;
            Transform arm = Array.Find(golem.GetComponentsInChildren<Transform>(), t => t.name == "upper_arm.L");
            Assert.That(arm, Is.Not.Null);
            Quaternion before = arm.localRotation;
            NavMeshAgent agent = golem.GetComponent<NavMeshAgent>();
            agent.speed = 4f;
            agent.SetDestination(new Vector3(3f, 0f, 4f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(Quaternion.Angle(before, arm.localRotation), Is.GreaterThan(1f));
            Transform body = golem.transform.Find("Body");
            Quaternion standing = body.localRotation;
            golem.TryApplyDamage(10000);
            yield return new WaitForSeconds(0.8f);
            Assert.That(Quaternion.Angle(standing, body.localRotation), Is.GreaterThan(50f));
            Call("SetKind", 2);
            Health boss = (Health)Call("SpawnAt", new Vector3(6f, 0f, -2f));
            boss.GetComponent<MonsterMeleeAttack>().enabled = false;
            Assert.That(boss.transform.Find("Body").localScale.x, Is.GreaterThan(0f));
            var bar = boss.GetComponent<SegmentedHealthBar>();
            Assert.That(bar.Boss, Is.True);
            boss.TryApplyDamage(250);
            yield return null;
            float remaining = 1f - 250f / boss.MaxHealth;
            Assert.That(bar.DamageTrailRatio, Is.GreaterThan(remaining));
            yield return new WaitForSeconds(1.5f);
            Assert.That(bar.DamageTrailRatio, Is.EqualTo(remaining).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator SpawnUsesPerTypeHealthSpeedAndTrackingSettings()
        {
            Call("SetKind", 0);
            object config = Settings(0);
            Set(config, "health", 375);
            Set(config, "moveSpeed", 2f);
            Set(config, "attackSpeed", 2.5f);
            Set(config, "sightRange", 6f);
            Set(config, "homeRange", 7f);
            Health normal = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            Assert.That(normal, Is.Not.Null);
            Assert.That(normal.CurrentHealth, Is.EqualTo(375));
            Assert.That(normal.GetComponent<NavMeshAgent>().speed, Is.EqualTo(2f));
            Assert.That(normal.GetComponent<MonsterMeleeAttack>().AttackSpeed, Is.EqualTo(2.5f));
            Assert.That(normal.GetComponent<MonsterTracker>().SightRange, Is.EqualTo(6f));
            Assert.That(normal.GetComponent<MonsterTracker>().HomeRange, Is.EqualTo(7f));
            Call("SetKind", 1);
            Health elite = (Health)Call("SpawnAt", new Vector3(5f, 0f, -2f));
            Assert.That(elite.CurrentHealth, Is.EqualTo(220));
            Assert.That(elite.GetComponent<MonsterTracker>().HomePosition.x, Is.EqualTo(5f).Within(0.1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossSpawnsWithModelAndInvalidOrOccupiedSpawnAreRejected()
        {
            Call("SetKind", 2);
            Health boss = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.MaxHealth, Is.EqualTo(850));
            Assert.That(boss.GetComponent<MonsterIdentity>().Rank, Is.EqualTo(MonsterRank.Boss));
            Assert.That(boss.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
            Call("SetKind", 0);
            Assert.That(Call("SpawnAt", new Vector3(100f, 0f, 100f)), Is.Null);
            Assert.That(Call("SpawnAt", player.transform.position), Is.Null);
            Assert.That(Call("SpawnAt", new Vector3(-3f, 0f, 0f)), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragonIdleMovesActualBonesAndDeathPlaysSuppliedClip()
        {
            Call("SetKind", 2);
            Health boss = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            boss.GetComponent<MonsterMeleeAttack>().enabled = false;
            boss.GetComponent<NavMeshAgent>().speed = 0f;
            yield return new WaitForSeconds(0.2f);
            Animator animator = boss.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle_A"), Is.True);
            Transform[] bones = animator.GetComponentsInChildren<Transform>();
            Quaternion[] rotations = Array.ConvertAll(bones, bone => bone.localRotation);
            yield return new WaitForSeconds(0.4f);
            bool moved = false;
            for (int i = 0; i < bones.Length; i++)
                moved |= Quaternion.Angle(rotations[i], bones[i].localRotation) > 0.1f;
            Assert.That(moved, Is.True, "Dragon clip bindings must move the model's actual skeleton");
            boss.TryApplyDamage(10000);
            yield return new WaitForSeconds(0.2f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Death_A"), Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [UnityTest]
        public IEnumerator AllRanksDropOneVisualEquipmentMarkerWithoutPickupAndClearTogether()
        {
            for (int rank = 0; rank < 3; rank++)
            {
                Call("SetKind", rank);
                Health actor = (Health)Call("SpawnAt", new Vector3(3f + rank * 2f, 0f, -2f));
                Assert.That(actor, Is.Not.Null);
                Assert.That(actor.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
                actor.TryApplyDamage(10000);
                actor.TryApplyDamage(10000);
                var drop = actor.GetComponent<MonsterDeathDrop>().SpawnedDrop;
                Assert.That(drop, Is.Not.Null);
                Assert.That(drop.SourceRank, Is.EqualTo((MonsterRank)rank));
                yield return null;
                Assert.That(drop.GetComponentsInChildren<Collider>().Length, Is.Zero);
                Assert.That(drop.GetComponentsInChildren<MonoBehaviour>().Length, Is.EqualTo(1),
                    "visual marker only: no pickup / loot / inventory component");
            }
            yield return new WaitForSeconds(0.8f);
            var drops = panel.GetComponentsInChildren<PlaceholderEquipmentDrop>();
            Assert.That(drops.Length, Is.EqualTo(3), "one per death, no duplicates");
            foreach (var drop in drops)
            {
                Assert.That(drop.Landed, Is.True);
                Assert.That(Physics.Raycast(drop.transform.position + Vector3.up, Vector3.down,
                    out RaycastHit floor, 3f), Is.True);
                Assert.That(drop.transform.position.y - floor.point.y, Is.EqualTo(0.14f).Within(0.05f));
            }
            Call("ClearMonsters");
            yield return null;
            Assert.That(panel.GetComponentsInChildren<PlaceholderEquipmentDrop>().Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PanelAndPlacementBlockTheRealPlayerShootingApi()
        {
            Component combat = player.GetComponent(Type.GetType("Nomad.Player.PlayerCombat, Assembly-CSharp"));
            MethodInfo shoot = combat.GetType().GetMethod("TryShoot");
            Vector2 panelPoint = new Vector2(Screen.width - 50f, Screen.height - 50f);
            Assert.That(shoot.Invoke(combat, new object[] { panelPoint }), Is.False);
            Set(panel, "placing", true);
            Vector2 worldPoint = Camera.main.WorldToScreenPoint(new Vector3(3f, 0f, -2f));
            Assert.That(shoot.Invoke(combat, new object[] { worldPoint }), Is.False);
            Set(panel, "placing", false);
            Set(panel, "placementClickFrame", Time.frameCount);
            Assert.That(shoot.Invoke(combat, new object[] { worldPoint }), Is.False,
                "소환을 끝낸 바로 그 클릭도 사격으로 전달하지 않아야 합니다.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeLimitReturnsToSpawnWithoutTrackingDuringReturn()
        {
            Call("SetKind", 0);
            object config = Settings(0);
            Set(config, "homeRange", 2f);
            Set(config, "ranged", false);
            Set(config, "sightRange", 20f);
            Set(config, "abandonRange", 30f);
            Set(config, "attackRange", 0.5f);
            Set(config, "moveSpeed", 6f);
            player.transform.position = new Vector3(3f, 0.05f, 5f);
            Health health = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            MonsterTracker tracker = health.GetComponent<MonsterTracker>();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!tracker.IsReturning && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(tracker.IsReturning, Is.True, "활동 반경을 넘으면 복귀");
            Assert.That(tracker.IsTracking, Is.False);
            while (tracker.IsReturning && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(tracker.IsTracking, Is.False, "복귀 도중 재감지 금지");
                yield return null;
            }
            Assert.That(Vector3.Distance(health.transform.position, tracker.HomePosition), Is.LessThan(0.7f));
        }

        [UnityTest]
        public IEnumerator ReturnHomeWithNormalStoppingDistanceFinishesAndDetectsAgain()
        {
            Call("SetKind", 0);
            object config = Settings(0);
            Set(config, "homeRange", 2f);
            Set(config, "ranged", false);
            Set(config, "moveSpeed", 1.5f);
            Call("SetInvincible", true);
            player.transform.position = new Vector3(3, 0.05f, 5);
            Health actor = (Health)Call("SpawnAt", new Vector3(3, 0, -2));
            MonsterTracker tracker = actor.GetComponent<MonsterTracker>();
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            float attackStop = agent.stoppingDistance;
            Assert.That(attackStop, Is.GreaterThan(0.5f), "원점 도착 반경보다 큰 기본 정지거리 재현");
            float deadline = Time.time + 8;
            while (!tracker.IsReturning && Time.time < deadline) yield return null;
            Assert.That(tracker.IsReturning, Is.True);
            player.transform.position = new Vector3(3, 0.05f, 20);
            while (tracker.IsReturning && Time.time < deadline) yield return null;
            Assert.That(tracker.IsReturning, Is.False, "원점 근처에서 영구 복귀 상태에 머물지 않음");
            Assert.That(Vector3.Distance(actor.transform.position, tracker.HomePosition), Is.LessThan(0.6f));
            Assert.That(agent.stoppingDistance, Is.EqualTo(attackStop), "전투 정지거리 복원");
            player.transform.position = actor.transform.position + Vector3.forward * 3;
            Vector3 start = actor.transform.position;
            deadline = Time.time + 2;
            while (Vector3.Distance(start, actor.transform.position) < 0.5f && Time.time < deadline) yield return null;
            Assert.That(tracker.IsTracking, Is.True, "복귀 후 다시 탐지");
            Assert.That(Vector3.Distance(start, actor.transform.position), Is.GreaterThan(0.5f), "재추적 실제 이동");
        }

        [UnityTest]
        public IEnumerator InterruptedAttackResumesPursuitAfterReactivation()
        {
            Call("SetKind", 0);
            Set(Settings(0), "ranged", false);
            Set(Settings(0), "skills", false);
            player.transform.position = new Vector3(3, 0.05f, -0.7f);
            Call("SetInvincible", true);
            Health actor = (Health)Call("SpawnAt", new Vector3(3, 0, -2));
            var attack = actor.GetComponent<MonsterMeleeAttack>();
            float deadline = Time.time + 3;
            while (attack.Phase != MeleePhase.Windup && Time.time < deadline) yield return null;
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Windup));
            actor.gameObject.SetActive(false);
            player.transform.position = new Vector3(3, 0.05f, 4);
            actor.gameObject.SetActive(true);
            Vector3 start = actor.transform.position;
            deadline = Time.time + 2;
            while (Vector3.Distance(start, actor.transform.position) < 0.5f && Time.time < deadline) yield return null;
            Assert.That(actor.GetComponent<MonsterTracker>().enabled, Is.True, "공격 중 정지시킨 탐지 재개");
            Assert.That(actor.GetComponent<NavMeshAgent>().isStopped, Is.False, "재활성화 후 남은 정지 상태 해제");
            Assert.That(Vector3.Distance(start, actor.transform.position), Is.GreaterThan(0.5f), "실제 추격 이동 재개");
        }

        [UnityTest]
        public IEnumerator RestoreRevivesPlayerAndClearRemovesTrackedActors()
        {
            Call("SetKind", 0);
            Call("SpawnAt", new Vector3(3f, 0f, -2f));
            player.TryApplyDamage(1000);
            Assert.That(player.IsDead, Is.True);
            Call("RestorePlayer");
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
            Assert.That(player.GetComponent<SegmentedHealthBar>().Compact, Is.True);
            Call("ClearMonsters");
            yield return null;
            Assert.That(panel.GetType().GetProperty("MonsterCount").GetValue(panel), Is.EqualTo(0));
            Assert.That(UnityEngine.Object.FindObjectsByType<MonsterTracker>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest]
        public IEnumerator InvincibilitySurvivesMovementAndBlocksActualMeleeHits()
        {
            foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
                if (component.GetType().FullName == "Nomad.Player.PlayerMovement") component.enabled = true;
            Call("SetInvincible", true);
            Call("SetKind", 0);
            object config = Settings(0);
            Set(config, "attackSpeed", 4f);
            Health monster = (Health)Call("SpawnAt", player.transform.position + Vector3.back * 1.4f);
            Assert.That(monster, Is.Not.Null);
            float end = Time.time + 1.5f;
            bool attacked = false;
            while (Time.time < end)
            {
                attacked |= monster.GetComponent<MonsterMeleeAttack>().Phase == MeleePhase.Recovery;
                yield return null;
            }
            Assert.That(attacked, Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
            Call("SetInvincible", false);
            Assert.That(player.TryApplyDamage(1), Is.True);
        }

        [UnityTest]
        public IEnumerator ApplyResetsOnlySelectedAndKeepsItIfHomeIsOccupied()
        {
            Call("SetKind", 0);
            Health original = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            Set(Settings(0), "health", 500);
            Call("ApplySelected");
            yield return null;
            Assert.That(original == null, Is.True);
            MonsterTracker[] active = UnityEngine.Object.FindObjectsByType<MonsterTracker>(FindObjectsSortMode.None);
            Assert.That(active.Length, Is.EqualTo(1));
            Health replacement = active[0].GetComponent<Health>();
            Assert.That(replacement.CurrentHealth, Is.EqualTo(500));
            player.transform.position = active[0].HomePosition;
            Call("ApplySelected");
            yield return null;
            Assert.That(replacement != null && replacement.gameObject.activeSelf, Is.True,
                "복귀 위치가 막히면 기존 몬스터를 잃지 않아야 합니다.");
        }

        [UnityTest]
        public IEnumerator RangeTogglesAndPauseControlActualSimulation()
        {
            Call("SetKind", 0);
            Health health = (Health)Call("SpawnAt", new Vector3(3f, 0f, -2f));
            yield return null;
            yield return null;
            LineRenderer detection = Array.Find(health.GetComponentsInChildren<LineRenderer>(),
                line => line.name == "Test range 0");
            Assert.That(detection.positionCount, Is.EqualTo(65));
            Assert.That(Vector3.Distance(detection.GetPosition(0), detection.GetPosition(64)), Is.LessThan(0.01f));
            // 코루틴과 LateUpdate 사이에 개체가 이동할 수 있으므로 그려진 원의 중심으로 검사합니다.
            Vector3 center = (detection.GetPosition(0) + detection.GetPosition(32)) * 0.5f;
            Assert.That(Vector3.Distance(detection.GetPosition(16), center),
                Is.EqualTo(health.GetComponent<MonsterTracker>().SightRange).Within(0.01f));
            Set(panel, "detection", false);
            Set(panel, "travel", true);
            yield return null;
            yield return null;
            Component ranges = health.GetComponent(Type.GetType("Nomad.Monsters.Demo.MonsterTestRanges, Assembly-CSharp"));
            Assert.That(ranges.GetType().GetProperty("ShowDetection").GetValue(ranges), Is.False);
            Assert.That(ranges.GetType().GetProperty("ShowTravel").GetValue(ranges), Is.True);
            LineRenderer[] lines = health.GetComponentsInChildren<LineRenderer>();
            Assert.That(Array.Find(lines, line => line.name == "Test range 0").enabled, Is.False);
            Assert.That(Array.Find(lines, line => line.name == "Test range 2").enabled, Is.True);
            Call("SetPaused", true);
            Vector3 position = health.transform.position;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Vector3.Distance(position, health.transform.position), Is.LessThan(0.01f));
            Call("SetPaused", false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        private object Call(string name, params object[] arguments) =>
            panel.GetType().GetMethod(name).Invoke(panel, arguments);

        private object Settings(int index) =>
            ((Array)panel.GetType().GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(panel)).GetValue(index);

        private static void Set(object owner, string name, object value) =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(owner, value);
    }
}
#endif
