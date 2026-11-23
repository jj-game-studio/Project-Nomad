using System;
using System.Collections;
using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Game;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Loot;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class MonsterProgressionVerification
    {
        private const string Key = "Nomad.Progression.Verify";
        private static IEnumerator steps;
        private static int frame = -1;
        private static double deadline;
        private static string error;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static bool terrain;

        public static void Run() => Begin(false);
        public static void RunTerrain() => Begin(true);

        private static void Begin(bool standalone)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("배치 전용 검증입니다.");
            terrain = standalone;
            SessionState.SetBool(Key + ".Terrain", terrain);
            EditorSceneManager.OpenScene(terrain ? "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity" : "Assets/Nomad/Game/Game.unity");
            if (terrain) ConfigureSeed(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            terrain = SessionState.GetBool(Key + ".Terrain", false);
            deadline = EditorApplication.timeSinceStartup + 400;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
        }

        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message;
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "검증 시간 초과");
                Require(error == null, error);
                if (!Application.isPlaying || frame == Time.frameCount) return;
                frame = Time.frameCount;
                if (steps == null) steps = Verify();
                if (!steps.MoveNext()) Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("MONSTER_PROGRESSION_FAIL " + exception);
                Finish(1);
            }
        }

        private static IEnumerator Verify()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            if (!terrain)
            {
                while (GameManager.Instance == null || GameManager.Instance.IsLoading || GameManager.Instance.Player == null) yield return null;
                SceneManager.sceneLoaded += ConfigureSeed;
                Require(GameManager.Instance.LoadRegion("Field"), "필드 진입");
                while (GameManager.Instance.IsLoading) yield return null;
            }
            TerrainIntegrationControls controls;
            while ((controls = Object.FindAnyObjectByType<TerrainIntegrationControls>()) == null ||
                controls.Navigation.State != FieldGenerationState.Ready || !controls.Player.activeInHierarchy) yield return null;
            Health player = controls.Player.GetComponent<Health>();
            player.IsCombatImmune = true;
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            Require(inventory != null, "실제 플레이어 가방");
            for (int warmup = 0; warmup < 10; warmup++) yield return null;

            foreach (int seed in new[] { 0, 7, 51 })
            {
                var data = new SerializedObject(controls);
                data.FindProperty("seed").intValue = seed;
                data.ApplyModifiedPropertiesWithoutUndo();
                controls.Regenerate(false);
                yield return null;
                yield return null;
                if (!terrain)
                    while (controls != null && controls.Navigation.State != FieldGenerationState.Ready &&
                        GameManager.Instance.CurrentRegion.name == "Field") yield return null;
                Require(controls != null, "생성 실패로 필드를 떠나지 않음");
                Require(controls.Navigation.State == FieldGenerationState.Ready, "경로 준비 seed=" + seed);
                Require(controls.GameplayView, "재생성 후 실제 플레이 시점 유지");
                Health[] actors = Actors(controls);
                if (!terrain) Require(actors.Count(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Normal) == 12,
                    "일반 몬스터12마리");
                if (!terrain) Require(Object.FindAnyObjectByType<Nomad.Game.Modules.FieldMonsters>().Enemies.Count == actors.Length,
                    "보스도 관리 목록에 등록됨");
                Require(actors.Count(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Elite) == 1 &&
                    actors.Count(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss) == 1, "필수 엘리트1·보스1 seed=" + seed);
                BossArena arena = Object.FindAnyObjectByType<BossArena>();
                BossRouteGuide guide = arena.GetComponent<BossRouteGuide>();
                Require(arena.Connected && guide.Available && !float.IsInfinity(MonsterEncounterLayout.PathLength(
                    controls.Navigation.LastResult.PlayerPosition, arena.Entrance)), "보스 안내 목적지에 도달 가능");
                Require(arena.transform.Find("Weathered floor tile") != null && arena.transform.Find("Ruined arena parapet") != null,
                    "제단 바닥·난간 외형");
                Health guardedBoss = actors.First(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss);
                Require(arena.IsLocked && !guardedBoss.TryApplyDamage(1) && !guardedBoss.GetComponent<MonsterTracker>().enabled &&
                    !guardedBoss.GetComponent<MonsterMeleeAttack>().enabled, "엘리트 생존 중 보스 휴면·선제 피해 방지");
                Vector3 savedPosition = player.transform.position;
                MovePlayer(player, arena.Center - arena.Forward * (BossArena.HalfSize + 1) + Vector3.up * 0.1f);
                player.GetComponent<CharacterController>().Move(arena.Forward * 4);
                Require(Vector3.Dot(player.transform.position - arena.Center, arena.Forward) < -11.5f, "엘리트 생존 중 물리 진입 차단");
                MovePlayer(player, savedPosition);
                Debug.Log($"PROGRESSION_SEED_PASS requested={seed} actual={controls.Navigation.LastResult.Seed} elite=1 boss=1 gate={arena.UsesGate}");
                if (seed == 51)
                {
                    foreach (Health actor in actors)
                    {
                        actor.GetComponent<MonsterTracker>().enabled = false;
                        actor.GetComponent<MonsterMeleeAttack>().enabled = false;
                        actor.GetComponent<NavMeshAgent>().isStopped = true;
                    }
                    Nomad.Minimap.Editor.MinimapVerification.Capture("boss-guide-179.png");
                    var sight = Object.FindAnyObjectByType<Nomad.Visibility.PlayerVisibility>();
                    if (sight != null) sight.enabled = false;
                    yield return null;
                    CaptureArena(arena);
                    if (sight != null) sight.enabled = true;
                    if (!terrain)
                    {
                        var minimap = Object.FindAnyObjectByType<Nomad.Minimap.RegionMinimap>();
                        while (!minimap.MapReady || !minimap.IsVisible) yield return null;
                        Require(minimap.EnemyMarkerCount == actors.Length, "생존 적만 종류별 미니맵 등록");
                        Health nearby = actors.First(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Normal);
                        MovePlayer(player, nearby.transform.position + Vector3.right * 2);
                        yield return null;
                        yield return null;
                        Require(minimap.VisibleEnemyMarkerCount > 0 && minimap.VisibleEnemyMarkerCount < actors.Length,
                            "현재 지도 범위 안의 적만 표시");
                        Nomad.Minimap.Editor.MinimapVerification.Capture("minimap-enemies-179.png");
                        MovePlayer(player, savedPosition);
                    }
                }
            }

            // 경사로를 만들 수 없는 경우에 쓰는 실제 진입문 경로도 별도로 확인합니다.
            Vector3 origin = controls.Player.transform.position;
            var fallbackRoot = new GameObject("Fallback gate verification");
            BossArena fallback = fallbackRoot.AddComponent<BossArena>();
            Require(fallback.Build(origin, Vector3.right, 160, origin.y, true), "진입문 대체 구역 경로 생성");
            var keyObject = new GameObject("Gate elite verification");
            keyObject.transform.SetParent(fallbackRoot.transform);
            Health gateElite = keyObject.AddComponent<Health>();
            Health gateBoss = Object.FindAnyObjectByType<MonsterActorFactory>().CreateRanked(player,
                MonsterTestSettings.Defaults(MonsterRank.Boss), MonsterRank.Boss, fallback.Center, Quaternion.identity,
                "Gate boss verification", fallbackRoot.transform);
            fallback.RequireElite(gateElite, gateBoss);
            BossGate enter = fallbackRoot.GetComponentsInChildren<BossGate>().First(gate => gate.Destination == fallback.Arrival);
            enter.Interact(new InteractionContext(player.transform, inventory));
            Require(Vector3.Distance(player.transform.position, origin) < 0.2f && fallback.IsLocked, "잠긴 E 진입문 차단");
            gateElite.TryApplyDamage(gateElite.CurrentHealth);
            Require(!fallback.IsLocked && !gateBoss.IsCombatImmune && gateBoss.GetComponent<MonsterTracker>().enabled,
                "엘리트 처치 이벤트로 문·보스 전투 즉시 해제");
            enter.Interact(new InteractionContext(player.transform, inventory));
            Require(Vector3.Distance(player.transform.position, fallback.Arrival) < 0.2f, "E 진입문 입장 위치");
            BossGate leave = fallbackRoot.GetComponentsInChildren<BossGate>().First(gate => gate != enter);
            leave.Interact(new InteractionContext(player.transform, inventory));
            Require(Vector3.Distance(player.transform.position, origin) < 0.2f, "E 진입문 귀환 위치");
            Object.Destroy(fallbackRoot);
            yield return null;

            Health elite = Actors(controls).First(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Elite);
            elite.TryApplyDamage(elite.CurrentHealth + 1);
            BossArena activeArena = Object.FindAnyObjectByType<BossArena>();
            Require(!activeArena.IsLocked && !Actors(controls).First(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss).IsCombatImmune,
                "실제 필수 엘리트 처치 후 제단 개방");
            var encounterSight = Object.FindAnyObjectByType<Nomad.Visibility.PlayerVisibility>();
            if (encounterSight != null)
            {
                Vector3 beforeEncounter = player.transform.position;
                MovePlayer(player, activeArena.Arrival);
                yield return null;
                yield return null;
                Require(encounterSight.OpenAreaOwner == activeArena && !encounterSight.Available,
                    "보스방 조우 중 시야 제한 개방");
                var bossVisibility = Actors(controls).First(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss)
                    .GetComponent<Nomad.Visibility.VisibilityTarget>();
                Require(bossVisibility == null || bossVisibility.IsVisible, "등을 돌려도 보스 모델 표시");
                Vector3 cameraPosition = Camera.main.transform.position;
                Nomad.Minimap.Editor.MinimapVerification.Capture("boss-open-sight-185.png");
                Require(Vector3.Distance(Camera.main.transform.position, cameraPosition) < 0.001f, "피격 충격 렌더 후 카메라 기준 위치 복원");
                MovePlayer(player, beforeEncounter);
                yield return null;
                yield return null;
                Require(encounterSight.OpenAreaOwner == null && encounterSight.Available, "보스방 퇴장 시 필드 시야 복구");
            }
            PlaceholderEquipmentDrop drop = elite.GetComponent<MonsterDeathDrop>().SpawnedDrop;
            float until = Time.unscaledTime + 3;
            while (drop.GetComponent<LootChest>() == null && Time.unscaledTime < until) yield return null;
            LootChest chest = drop.GetComponent<LootChest>();
            Require(chest != null && chest.StackCount == 1 && drop.Landed, "엘리트 착지 후 실제 장비1개");
            if (!terrain) Require(Object.FindAnyObjectByType<Nomad.Minimap.RegionMinimap>().EnemyMarkerCount == Actors(controls).Length - 1,
                "엘리트 사망 시 미니맵 표시 제거");
            string itemId = chest.GetStack(0).ItemId;
            inventory.Add(itemId, int.MaxValue);
            Require(chest.TryLoot(inventory) == 0 && chest.GetStack(0).Count == 1, "가방 가득 차면 드롭 보존");
            MovePlayer(player, drop.transform.position + Vector3.forward * 1.1f);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Require(GameObject.Find("ChestLootPanel") != null, "실제 E 입력으로 기존 루팅 창 열기");
            Nomad.Minimap.Editor.MinimapVerification.Capture("equipment-loot-179.png");
            Require(inventory.Remove(itemId, 1) == 1 && chest.TryLoot(inventory) == 1 && chest.TryLoot(inventory) == 0,
                "장비 실제 획득·반복 획득 무복제");
            GameObject.Find("ChestLootPanel").transform.Find("Close").GetComponent<Button>().onClick.Invoke();

            Health boss = Actors(controls).First(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss);
            boss.GetComponent<MonsterTracker>().enabled = false;
            boss.GetComponent<MonsterMeleeAttack>().enabled = false;
            boss.GetComponent<NavMeshAgent>().isStopped = true;
            MovePlayer(player, boss.transform.position + Vector3.right * 2.4f);
            yield return null;
            var gear = player.GetComponent<Nomad.Equipment.PlayerEquipment>();
            var combat = player.GetComponent<Nomad.Equipment.PlayerEquipmentCombat>();
            inventory.Remove(itemId, 1);
            inventory.Add("weapon-dagger", 1);
            int daggerSlot = Enumerable.Range(0, inventory.SlotCount).First(index => inventory.GetSlot(index).Item?.ItemId == "weapon-dagger");
            Require(gear.TryEquip(daggerSlot, Nomad.Equipment.EquipmentSlot.Weapon1), "피격 검사 단검 장착");
            gear.SelectWeapon(Nomad.Equipment.EquipmentSlot.Weapon1);
            int bossHealth = boss.CurrentHealth;
            Vector2 pointer = Camera.main.WorldToScreenPoint(boss.transform.position + Vector3.up);
            Require(combat.TryAttack(pointer) && boss.CurrentHealth < bossHealth, "몸통 중심2.4m 거리에서도 실제 단검 적중");
            var shot = (Projectile)new SerializedObject(combat).FindProperty("projectile").objectReferenceValue;
            Vector3 originShot = boss.transform.position + new Vector3(0.95f, 1.3f, -5);
            bossHealth = boss.CurrentHealth;
            Require(Projectile.TryLaunch(shot, player.transform, originShot, originShot + Vector3.forward * 10,
                Physics.DefaultRaycastLayers, 40, 10, 1), "비껴간 발사체 생성");
            float hitDeadline = Time.unscaledTime + 2;
            while (boss.CurrentHealth == bossHealth && Time.unscaledTime < hitDeadline) yield return null;
            Require(boss.CurrentHealth < bossHealth, "몸통 중심0.95m 옆을 지나는 실제 발사체 적중");
            NavMeshAgent bossAgent = boss.GetComponent<NavMeshAgent>();
            bossAgent.isStopped = false;
            Require(bossAgent.SetDestination(boss.transform.position + activeArena.Forward * 3), "용 실제 보행 경로");
            for (int walkFrame = 0; walkFrame < 45; walkFrame++)
            {
                yield return null;
                if (walkFrame % 15 == 0) CaptureDragonWalk(boss, walkFrame / 15);
            }
            bossAgent.isStopped = true;
            inventory.Add(itemId, int.MaxValue);
            BossRouteGuide activeGuide = Object.FindAnyObjectByType<BossRouteGuide>();
            boss.TryApplyDamage(boss.CurrentHealth + 1);
            PlaceholderEquipmentDrop bossDrop = boss.GetComponent<MonsterDeathDrop>().SpawnedDrop;
            until = Time.unscaledTime + 3;
            while (bossDrop.GetComponent<LootChest>() == null && Time.unscaledTime < until) yield return null;
            LootChest bossChest = bossDrop.GetComponent<LootChest>();
            Require(bossChest != null && bossChest.StackCount == 2 && !activeGuide.Available, "보스 장비2개·처치 시 길안내 종료");
            inventory.Remove(itemId, 1);
            Require(bossChest.TryLoot(inventory) == 1 && Enumerable.Range(0, bossChest.StackCount).Sum(index => bossChest.GetStack(index).Count) == 1,
                "보스 보상 부분 획득·잔량 보존");
            int before = Object.FindObjectsByType<PlaceholderEquipmentDrop>(FindObjectsSortMode.None).Length;
            boss.TryApplyDamage(1);
            Require(Object.FindObjectsByType<PlaceholderEquipmentDrop>(FindObjectsSortMode.None).Length == before, "사망 중복 드롭 없음");
            controls.Regenerate(false);
            yield return null;
            Require(drop == null && bossDrop == null && Object.FindObjectsByType<BossArena>(FindObjectsSortMode.None).Length == 1,
                "재생성 시 이전 드롭·구역·길안내 정리");
            Debug.Log("MONSTER_PROGRESSION_PASS mode=" + (terrain ? "TerrainIntegration" : "Game") +
                " seeds=3 required=pass elite_lock=pass gates=pass loot=pass full=pass partial=pass no_duplicate=pass cleanup=pass");
        }

        private static Health[] Actors(TerrainIntegrationControls controls) => Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None)
            .Where(identity => identity.gameObject.scene == controls.gameObject.scene).Select(identity => identity.GetComponent<Health>()).ToArray();

        private static void ConfigureSeed(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TerrainIntegrationControls controls = root.GetComponentInChildren<TerrainIntegrationControls>(true);
                if (controls == null) continue;
                var data = new SerializedObject(controls);
                data.FindProperty("newSeedOnStart").boolValue = false;
                data.FindProperty("seed").intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                SceneManager.sceneLoaded -= ConfigureSeed;
                break;
            }
        }

        private static void MovePlayer(Health player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            bool enabled = controller != null && controller.enabled;
            if (enabled) controller.enabled = false;
            player.transform.position = position;
            if (enabled) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void CaptureDragonWalk(Health boss, int frame)
        {
            Camera camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            camera.transform.position = boss.transform.position + new Vector3(8, 5, -9);
            camera.transform.LookAt(boss.transform.position + Vector3.up * 1.8f);
            Nomad.Minimap.Editor.MinimapVerification.Capture("dragon-walk-190-" + frame + ".png");
            camera.transform.SetPositionAndRotation(position, rotation);
        }

        private static void CaptureArena(BossArena arena)
        {
            Camera camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            camera.transform.position = arena.Center + new Vector3(0, 23, -25);
            camera.transform.LookAt(arena.Center);
            Nomad.Minimap.Editor.MinimapVerification.Capture("boss-arena-179.png");
            camera.transform.SetPositionAndRotation(position, rotation);
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            SceneManager.sceneLoaded -= ConfigureSeed;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            EditorApplication.Exit(code);
        }
    }
}
