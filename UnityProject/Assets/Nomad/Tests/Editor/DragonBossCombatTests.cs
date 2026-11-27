using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Tests
{
    public sealed class DragonBossCombatTests
    {
        private readonly List<Object> created = new();
        private BossArena arena;
        private Health player, boss;
        private DragonBossCombat combat;
        private NavMeshDataInstance ground;
        private int previousCapture;

        [UnitySetUp]
        public IEnumerator EnterEncounter()
        {
            yield return new EnterPlayMode();
            BuildEncounter();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        public void BuildEncounter()
        {
            previousCapture = Time.captureFramerate;
            Time.captureFramerate = 60;
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.35f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            NavMeshData data = Track(NavMeshBuilder.BuildNavMeshData(settings,
                new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(36, 0.6f, 36), transform = Matrix4x4.Translate(Vector3.down * 0.3f) } },
                new Bounds(Vector3.zero, new Vector3(40, 8, 40)), Vector3.zero, Quaternion.identity));
            ground = NavMesh.AddNavMeshData(data);
            arena = Track(new GameObject("Boss test arena")).AddComponent<BossArena>();
            Assert.That(arena.Build(new Vector3(0, 0.15f, 15), Vector3.forward, 3), Is.True);
            GameObject target = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            target.name = "Boss test player";
            target.transform.position = arena.Center + Vector3.right * 3;
            target.GetComponent<CapsuleCollider>().center = Vector3.up;
            target.SetActive(false);
            player = target.AddComponent<Health>();
            JsonUtility.FromJsonOverwrite("{\"maxHealth\":2000}", player);
            target.SetActive(true);
            var factory = Track(new GameObject("Boss test factory")).AddComponent<MonsterActorFactory>();
            var material = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            factory.ConfigureMaterials(material, material, material, material);
            MonsterAppearanceSet appearances = Track(ScriptableObject.CreateInstance<MonsterAppearanceSet>());
            appearances.boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Nomad/Monsters/Candidates/Dragon.prefab");
            factory.ConfigureAppearances(appearances);
            boss = factory.CreateRanked(player, MonsterTestSettings.Defaults(MonsterRank.Boss), MonsterRank.Boss,
                arena.Center, Quaternion.LookRotation(Vector3.right), "Test boss", arena.transform);
            arena.PrepareCombat(boss, player);
            combat = boss.GetComponent<DragonBossCombat>();
            combat.AutomaticPatterns = false;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator ClearNavigation()
        {
            foreach (Object item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            if (ground.valid) ground.Remove();
            Time.captureFramerate = previousCapture;
            yield return new ExitPlayMode();
        }

        [Test]
        public void DamageEventReportsActualLossAndNeverReportsImmunityOrHealing()
        {
            int calls = 0, actual = 0;
            player.Damaged += (_, amount, kind) => { calls++; actual = amount; };
            player.IsCombatImmune = true;
            Assert.That(player.TryApplyDamage(20), Is.False);
            Assert.That(calls, Is.Zero);
            player.IsCombatImmune = false;
            player.TryApplyDamage(18);
            Assert.That(actual, Is.EqualTo(18));
            player.RestoreFull();
            Assert.That(calls, Is.EqualTo(1));
            player.TryApplyDamage(int.MaxValue);
            Assert.That(actual, Is.EqualTo(player.MaxHealth), "과잉 피해 대신 실제 감소량");
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ReferenceSwordCombatLastsAboutTwoMinutesAndPlayerHurtFeedbackFades()
        {
            var muted = new List<InputDevice>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                foreach (InputDevice device in InputSystem.devices.Where(device => device != mouse && device != keyboard && (device is Mouse || device is Keyboard)).ToArray())
                { InputSystem.DisableDevice(device); muted.Add(device); }
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                GameObject rig = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab")));
                var attacker = rig.GetComponentInChildren<PlayerEquipmentCombat>();
                var gear = attacker.GetComponent<PlayerEquipment>();
                var bag = attacker.GetComponent<PlayerInventory>();
                attacker.GetComponent<PlayerMovement>().enabled = false;
                attacker.GetComponent<PlayerStats>().RandomSource = () => 0.99f;
                attacker.transform.position = boss.transform.position - Vector3.forward * 3;
                foreach (Camera old in rig.GetComponentsInChildren<Camera>()) old.enabled = false;
                Camera camera = Track(new GameObject("Balance camera")).AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 15;
                camera.transform.position = boss.transform.position + new Vector3(0, 18, -12);
                camera.transform.LookAt(boss.transform.position);
                TestKit.SetField(attacker, "viewCamera", camera);
                yield return null; // 시작 장비 로드가 끝난 뒤 검증 장비를 장착합니다.
                bag.Add("demo-sword", 2);
                int slot = Enumerable.Range(0, bag.SlotCount).First(index => bag.GetSlot(index).Item?.ItemId == "demo-sword");
                Assert.That(gear.TryEquip(slot, EquipmentSlot.Weapon1), Is.True);
                Assert.That(gear.TryEnhance(EquipmentSlot.Weapon1), Is.True);
                Assert.That(attacker.CurrentWeapon.EnhancementLevel, Is.EqualTo(1));
                Physics.SyncTransforms();
                yield return null;

                Health victim = attacker.GetComponent<Health>();
                int before = victim.CurrentHealth;
                victim.TryApplyDamage(18);
                yield return null;
                var overlay = attacker.GetComponent<CombatFeedbackOverlay>();
                Assert.That(overlay.LastDamage, Is.EqualTo(before - victim.CurrentHealth));
                Assert.That(overlay.VignetteAlpha, Is.GreaterThan(0));
                Assert.That(overlay.ActiveNumbers, Is.EqualTo(1));
                Assert.That(attacker.GetComponent<PlayerDamageFeedback>().Reacting, Is.True);
                Animator animator = attacker.GetComponentInChildren<Animator>();
                Assert.That(animator.GetLayerWeight(animator.GetLayerIndex(PlayerDamageFeedback.LayerName)), Is.GreaterThan(0));
                yield return Wait(0.9f);
                Assert.That(overlay.VignetteAlpha, Is.Zero);
                Assert.That(overlay.ActiveNumbers, Is.Zero);
                Assert.That(attacker.GetComponent<PlayerDamageFeedback>().Reacting, Is.False);

                Vector2 pointer = camera.WorldToScreenPoint(boss.transform.position + Vector3.up);
                float started = Time.time;
                int attacks = 0, skills = 0;
                // 8초마다 4.4초 공격, 3.6초 회피. 실제 선딜·쿨다운·스킬·갑옷 보정을 통과합니다.
                while (!boss.IsDead && Time.time - started < 180)
                {
                    if ((Time.time - started) % 8 < 4.4f)
                    {
                        if (attacker.TryUseSkill(pointer)) skills++;
                        else if (attacker.TryAttack(pointer)) attacks++;
                    }
                    yield return null;
                }
                float elapsed = Time.time - started;
                Debug.Log($"BOSS_REFERENCE_FIGHT seconds={elapsed:0.0} health={boss.MaxHealth} weapon=B+1 crit=off attacks={attacks} skills={skills} attackWindow=4.4/8");
                Assert.That(boss.IsDead, Is.True);
                Assert.That(elapsed, Is.InRange(105f, 140f));
                Assert.That(boss.GetComponent<BossArmor>().BreakCount, Is.EqualTo(2));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                foreach (InputDevice device in muted) InputSystem.EnableDevice(device);
            }
        }

        [Test]
        public void RocksLeaveCenterAndEntranceClear()
        {
            BossBreakableRock[] rocks = arena.GetComponentsInChildren<BossBreakableRock>();
            Assert.That(rocks.Length, Is.EqualTo(6));
            foreach (BossBreakableRock rock in rocks)
            {
                Vector3 offset = rock.transform.position - arena.Center;
                Assert.That(Vector3.ProjectOnPlane(offset, Vector3.up).magnitude, Is.GreaterThan(6));
                if (Vector3.Dot(offset, arena.Forward) < 0)
                    Assert.That(Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, arena.Forward))), Is.GreaterThan(4));
                Assert.That(rock.GetComponentInChildren<NavMeshObstacle>().carving, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CircleIsSlowAndCanBeEscapedThenDamagesOnlyOnce()
        {
            combat.BeginPattern(DragonPattern.Circle);
            int hp = player.CurrentHealth;
            yield return Wait(1);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp), "전조 중 피해 없음");
            player.transform.position = arena.Center + Vector3.right * 10.5f;
            Physics.SyncTransforms();
            yield return Wait(0.9f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp), "걸어서 범위를 빠져나가 회피");
            player.transform.position = arena.Center + Vector3.right * 3;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.Circle);
            yield return Wait(1.9f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 32), "강타는 한 번만 피해");
        }

        [UnityTest]
        public IEnumerator TripleHasNoGroundRangeAndBreathDirectionIsLocked()
        {
            player.transform.position = arena.Center + Vector3.right * 7;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.TripleFireball);
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.Zero, "3갈래 바닥 표시 없음");
            yield return Wait(1);
            Assert.That(combat.Effects.GetComponentsInChildren<BossFireball>().Length, Is.EqualTo(3));
            combat.Stun(0.01f);
            yield return Wait(0.03f);
            player.transform.position = arena.Center + Vector3.forward * 6;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.Breath);
            Quaternion facing = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = arena.Center - Vector3.forward * 3;
            Physics.SyncTransforms();
            int hp = player.CurrentHealth;
            yield return Wait(2.1f);
            Assert.That(Quaternion.Angle(boss.transform.rotation, facing), Is.LessThan(0.1f));
            Assert.That(player.CurrentHealth, Is.EqualTo(hp), "브레스 뒤로 회피 가능");
        }

        [UnityTest]
        public IEnumerator ArmorBreaksTwiceIncreaseDamageWithoutStunningOrCancellingAttacks()
        {
            BossArmor armor = boss.GetComponent<BossArmor>();
            combat.BeginPattern(DragonPattern.Circle);
            boss.TryApplyDamage(Mathf.CeilToInt(boss.MaxHealth * 0.31f));
            yield return null;
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Windup), "갑옷 파괴로 공격을 멈추지 않음");
            Assert.That(armor.BreakCount, Is.EqualTo(1));
            Assert.That(armor.ModifyCombatDamage(100), Is.EqualTo(125));
            Assert.That(boss.GetComponent<BossStunDisplay>().Visible, Is.False);
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.EqualTo(1));
            boss.TryApplyDamage(1);
            Assert.That(armor.BreakCount, Is.EqualTo(1));
            boss.TryApplyDamage(Mathf.CeilToInt(boss.MaxHealth * 0.35f / 1.25f));
            yield return null;
            Assert.That(armor.BreakCount, Is.EqualTo(2));
            Assert.That(armor.ModifyCombatDamage(100), Is.EqualTo(155));
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Windup));
            boss.TryApplyDamage(boss.MaxHealth);
            yield return null;
            Assert.That(armor.BreakCount, Is.EqualTo(2), "최대 두 번만 파괴");
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Dead));
            Assert.That(combat.Effects.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ChargingIntoRockBreaksItAndStunsBoss()
        {
            yield return Wait(0.2f);
            BossBreakableRock rock = arena.GetComponentsInChildren<BossBreakableRock>().First();
            Vector3 point = rock.transform.position;
            Assert.That(boss.GetComponent<NavMeshAgent>().Warp(point + Vector3.right * 4), Is.True);
            player.transform.position = point - Vector3.right * 3;
            Physics.SyncTransforms();
            combat.BeginPattern(DragonPattern.Charge);
            yield return Wait(2);
            Assert.That(rock == null || rock.Broken, Is.True, $"phase={combat.Phase} position={boss.transform.position} rock={point}");
            Assert.That(combat.RockStuns, Is.EqualTo(1));
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Stunned));
        }

        [UnityTest]
        public IEnumerator FirePoolTicksAndStopsAfterLeavingAndDeathCleansIt()
        {
            combat.BeginPattern(DragonPattern.FirePool);
            int hp = player.CurrentHealth;
            yield return Wait(1.9f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 10));
            player.transform.position = arena.Center - Vector3.right * 6;
            Physics.SyncTransforms();
            yield return Wait(1);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 10));
            boss.TryApplyDamage(boss.MaxHealth);
            yield return null;
            Assert.That(combat.Effects.childCount, Is.Zero);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator BasicAttackIsSlowAndRainWarningsStayAtTheirOriginalPosition()
        {
            combat.BeginPattern(DragonPattern.Basic);
            int hp = player.CurrentHealth;
            yield return Wait(0.25f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp));
            yield return Wait(0.4f);
            Assert.That(player.CurrentHealth, Is.EqualTo(hp - 18));
            combat.BeginPattern(DragonPattern.Rain);
            yield return Wait(1);
            BossHazard first = combat.Effects.GetComponentsInChildren<BossHazard>().First();
            Vector3 warningPosition = first.transform.position;
            Assert.That(first.Active, Is.False, "작은 불덩이에도 회피 전조 있음");
            Assert.That(first.Radius, Is.EqualTo(1.15f));
            player.transform.position = arena.Center - Vector3.right * 6;
            Physics.SyncTransforms();
            yield return Wait(0.5f);
            Assert.That(first.transform.position, Is.EqualTo(warningPosition), "생성한 표식은 플레이어를 따라가지 않음");
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.GreaterThanOrEqualTo(2));
            boss.TryApplyDamage(boss.MaxHealth);
            yield return null;
            Assert.That(combat.Effects.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BossChainsAttacksWithoutIdleRecovery()
        {
            combat.AutomaticPatterns = true;
            combat.BeginPattern(DragonPattern.Basic);
            Assert.That(boss.GetComponent<NavMeshAgent>().speed, Is.EqualTo(6));
            for (int frame = 0; frame < 300; frame++)
            {
                yield return null;
                if (combat.Phase == DragonPhase.Recovery)
                    Assert.That(boss.GetComponent<MonsterTracker>().enabled && !boss.GetComponent<NavMeshAgent>().isStopped,
                        Is.True, "사거리 밖이면 쉬지 않고 추적");
            }
            Assert.That(combat.AttacksStarted, Is.GreaterThanOrEqualTo(4), "다음 공격으로 즉시 연결");
        }

        [UnityTest]
        public IEnumerator RainContinuesLongerAndSpawnsSixtyFireballs()
        {
            combat.BeginPattern(DragonPattern.Rain);
            yield return Wait(8.4f);
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Active));
            Assert.That(combat.RainProjectilesSpawned, Is.EqualTo(60));
            yield return Wait(0.9f);
            Assert.That(combat.Effects.GetComponentsInChildren<BossHazard>().Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BreathTracksCurrentAnimatedMouthAndStunIndicatorClears()
        {
            Assert.That(combat.Mouth.parent.name, Is.EqualTo("jaw_upper"));
            combat.BeginPattern(DragonPattern.Breath);
            yield return Wait(2);
            BossHazard breath = combat.Effects.GetComponentsInChildren<BossHazard>().Single();
            Assert.That(breath.Radius, Is.EqualTo(13.5f));
            Vector3 widenedEdge = boss.transform.position + Quaternion.Euler(0, 42, 0) * boss.transform.forward * 12.5f;
            Assert.That(breath.Contains(widenedEdge), Is.True, "기존 9m/70도를 벗어난 지점까지 확대");
            Assert.That(breath.GetComponentsInChildren<ParticleSystem>().Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(breath.BreathOrigin, Is.SameAs(combat.Mouth));
            Assert.That(Vector3.Distance(breath.LastBreathOrigin, combat.Mouth.position), Is.LessThan(0.06f), "현재 머리 위치에서 연속 발사");
            combat.Stun(0.3f);
            yield return Wait(0.1f);
            Assert.That(boss.GetComponent<BossStunDisplay>().Visible, Is.True);
            Assert.That(boss.GetComponentInChildren<TextMesh>(true)?.text, Is.Not.EqualTo("기절"));
            yield return Wait(0.4f);
            Assert.That(boss.GetComponent<BossStunDisplay>().Visible, Is.False);
        }

        [UnityTest]
        public IEnumerator ChargeHasPriorityAtDeadlineTracksDuringTellThenLocks()
        {
            combat.AutomaticPatterns = true;
            yield return Wait(0.2f);
            TestKit.SetField(combat, "nextCharge", Time.time - 0.1f);
            combat.Stun(0.01f);
            yield return Wait(0.05f);
            Assert.That(combat.Pattern, Is.EqualTo(DragonPattern.Charge));
            Assert.That(combat.Phase, Is.EqualTo(DragonPhase.Windup));
            Quaternion before = boss.transform.rotation;
            player.transform.position = arena.Center + Vector3.forward * 7;
            Physics.SyncTransforms();
            yield return Wait(0.3f);
            float turned = Quaternion.Angle(before, boss.transform.rotation);
            Assert.That(turned, Is.InRange(10, DragonBossCombat.ChargeTurnSpeed * 0.35f), "전조 중 제한된 속도로 조준 조절");
            while (!combat.ChargeAimLocked) yield return null;
            Quaternion locked = boss.transform.rotation;
            player.transform.position = arena.Center - Vector3.forward * 7;
            Physics.SyncTransforms();
            yield return Wait(0.2f);
            Assert.That(Quaternion.Angle(locked, boss.transform.rotation), Is.LessThan(0.1f), "전조 마지막은 고정되어 회피 가능");
            yield return Wait(0.4f);
            Assert.That(combat.ChargesReleased, Is.EqualTo(1));
            Assert.That(combat.ChargeRemaining, Is.InRange(9, 10));
        }

        [UnityTest]
        public IEnumerator EveryFrameTurnsSmoothlyEvenWhenTargetChangesSides()
        {
            player.transform.position = arena.Center - boss.transform.forward * 7;
            Physics.SyncTransforms();
            Quaternion before = boss.transform.rotation;
            combat.BeginPattern(DragonPattern.Breath);
            Assert.That(Quaternion.Angle(before, boss.transform.rotation), Is.LessThan(0.01f), "공격 시작 시 순간 회전 없음");
            for (int frame = 0; frame < 90; frame++)
            {
                before = boss.transform.rotation;
                yield return null;
                Assert.That(Quaternion.Angle(before, boss.transform.rotation), Is.LessThanOrEqualTo(DragonBossCombat.TurnSpeed / 60 + 0.05f));
            }
        }

        [UnityTest]
        public IEnumerator PointBlankCircleCanBeEscapedWithRealRollAndWalking()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var muted = new List<InputDevice>();
            bool background = Application.runInBackground;
            var behavior = InputSystem.settings.backgroundBehavior;
            var input = InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                foreach (Keyboard device in InputSystem.devices.OfType<Keyboard>().Where(device => device != keyboard).ToArray())
                { InputSystem.DisableDevice(device); muted.Add(device); }
                keyboard.MakeCurrent();
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                var hero = Track(new GameObject("Real roll escape"));
                hero.SetActive(false);
                CharacterController controller = hero.AddComponent<CharacterController>();
                controller.height = 2;
                controller.center = Vector3.up;
                controller.radius = 0.3f;
                Health health = hero.AddComponent<Health>();
                PlayerMovement movement = hero.AddComponent<PlayerMovement>();
                Camera camera = Track(new GameObject("Escape camera")).AddComponent<Camera>();
                camera.transform.position = arena.Center + new Vector3(0, 10, -10);
                camera.transform.LookAt(arena.Center);
                TestKit.SetField(movement, "viewCamera", camera);
                hero.transform.position = arena.Center + new Vector3(0, 0.1f, 1.85f);
                hero.SetActive(true);
                controller.Move(Vector3.down);
                player.gameObject.SetActive(false);
                TestKit.SetField(combat, "target", health);
                yield return Wait(0.1f);
                int hp = health.CurrentHealth;

                combat.BeginPattern(DragonPattern.Circle);
                yield return Wait(0.25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                yield return Wait(0.2f);
                Assert.That(movement.IsRolling, Is.True, "실제 구르기 입력");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Wait(1.4f);
                Assert.That(Vector3.ProjectOnPlane(hero.transform.position - arena.Center, Vector3.up).magnitude,
                    Is.GreaterThan(DragonBossCombat.CircleRadius), "근접에서 구르기 1회+걷기로 광역 탈출");
                Assert.That(health.CurrentHealth, Is.EqualTo(hp));
                Assert.That(health.IsCombatImmune, Is.False, "판정 때 구르기 무적 대신 범위 이탈로 회피");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                foreach (InputDevice device in muted) InputSystem.EnableDevice(device);
                Application.runInBackground = background;
                InputSystem.settings.backgroundBehavior = behavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = input;
            }
        }
    }
}
