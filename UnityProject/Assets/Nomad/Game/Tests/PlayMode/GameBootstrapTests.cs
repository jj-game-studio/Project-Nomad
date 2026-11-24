#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Game.Tests
{
    public sealed class GameBootstrapTests
    {
        private const string GameScenePath = "Assets/Nomad/Game/Game.unity";
        private const string VillagePath = "Assets/Nomad/Village/Village.unity";
        private const string FieldPath = "Assets/Nomad/Game/Field.unity";
        private const float Timeout = 20f;
        private const float FieldTimeout = 90f;

        private Scene previousScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            yield return DestroyManager();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyManager();

            // 재시작 테스트는 Game 씬을 Single로 불러 러너 씬까지 내리므로, 마지막 씬을 내릴 수 있게 빈 씬을 둡니다.
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
            else
                SceneManager.SetActiveScene(SceneManager.CreateScene("Test Host"));

            // 테스트가 연 Game·Village 씬만 내립니다.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != previousScene && (scene.path == GameScenePath || scene.path == VillagePath || scene.path == FieldPath))
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator GameScene_LoadsVillageAsActiveRegion()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));

            Scene game = SceneManager.GetSceneByPath(GameScenePath);
            Assert.That(game.GetRootGameObjects().Length, Is.EqualTo(0), "GameManager는 영속 씬으로 이동하고 Game 씬에는 다른 오브젝트가 없음");

            GameManager manager = GameManager.Instance;
            Assert.That(manager, Is.Not.Null, "Game 씬의 GameManager");

            yield return WaitForRegion(manager);
            Assert.That(manager.CurrentRegion.path, Is.EqualTo(VillagePath), "시작 지역");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(manager.CurrentRegion), "지역 씬이 활성 씬(조명)");
            Assert.That(manager.Player.GetComponent("PlayerAnimation"), Is.Not.Null, "메인 플레이어 이동 애니메이션");
            Assert.That(manager.Player.GetComponent("PlayerSwordAnimation"), Is.Not.Null, "기존 검 공격 애니메이션");
            Assert.That(manager.Player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.sharedMesh != null), Is.True,
                "메인 씬 캐릭터 외형이 실제로 활성화되어 있음");
            Assert.That(manager.Player.transform.Find("Player Body").gameObject.activeSelf, Is.False, "임시 캡슐 외형 숨김");
        }

        [UnityTest]
        public IEnumerator LoadRegion_UnloadsPreviousAndKeepsModules()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);

            // 모듈은 GameManager 자식으로 붙어 지역 전환 후에도 남아야 합니다.
            Transform module = new GameObject("Test Module").transform;
            module.SetParent(manager.transform);

            // 활성 지역 씬에 새로 만든 오브젝트는 지역과 함께 사라집니다(런타임 UI 주의점 확인).
            GameObject regionObject = new GameObject("Region Object");
            Assert.That(regionObject.scene, Is.EqualTo(manager.CurrentRegion), "새 오브젝트는 활성 지역 씬에 생성");

            Scene first = manager.CurrentRegion;
            int loadedEvents = 0;
            manager.RegionLoaded += _ => loadedEvents++;

            Assert.That(manager.LoadRegion("Village"), Is.True, "전환 시작");
            Assert.That(manager.LoadRegion("Village"), Is.False, "전환 중 중복 요청 무시");
            yield return WaitForRegion(manager);

            Assert.That(loadedEvents, Is.EqualTo(1), "RegionLoaded 1회");
            Assert.That(first.isLoaded, Is.False, "이전 지역 언로드");
            Assert.That(manager.CurrentRegion.isLoaded && manager.CurrentRegion != first, Is.True, "새 지역 로드");
            Assert.That(CountLoaded(VillagePath), Is.EqualTo(1), "지역 씬 중복 없음");
            Assert.That(module != null, Is.True, "모듈 유지");
            Assert.That(regionObject == null, Is.True, "지역 오브젝트는 함께 언로드");
        }

        [UnityTest]
        public IEnumerator RegionScenePlayedAlone_CreatesManagerForThatRegion()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(VillagePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Scene village = SceneManager.GetSceneByPath(VillagePath);
            SceneManager.SetActiveScene(village);

            Scene reported = default;
            GameManager manager = GameManager.BootstrapRegion();
            Assert.That(manager, Is.Not.Null, "지역 씬 단독 Play에서 생성");
            manager.RegionLoaded += scene => reported = scene;

            yield return null;
            Assert.That(manager.CurrentRegion, Is.EqualTo(village), "이미 열린 씬을 현재 지역으로 사용");
            Assert.That(reported, Is.EqualTo(village), "RegionLoaded 통지");
            Assert.That(CountLoaded(VillagePath), Is.EqualTo(1), "시작 지역을 다시 불러오지 않음");
            Assert.That(GameManager.BootstrapRegion(), Is.Null, "이미 있으면 다시 만들지 않음");
            AssertAtEntrance(manager, null);
        }

        [UnityTest]
        public IEnumerator GameStart_PlayerKeepsInventoryAndUiThroughFieldRoundTrip()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);

            // 마을 기본 입구에 같은 Player 프리팹이 서고, HUD·가방 UI는 Player와 같은 영속 씬에 있습니다.
            GameObject player = manager.Player;
            Assert.That(player, Is.Not.Null, "Player 모듈");
            AssertAtEntrance(manager, null);
            AssertUiKept(player);
            Assert.That(CallInventory(player, "Add", "scrap", 5), Is.EqualTo(5), "가방에 넣기");
            Camera camera = manager.GetComponentInChildren<Camera>();
            CameraClearFlags villageClear = camera.clearFlags;

            // 마을 출구에서 전환하고, 필드의 지형과 플레이어 배치가 준비된 뒤 화면이 열리는지 확인합니다.
            yield return EnterExit(manager, FindInRegion(manager, "Field Exit Trigger"));
            yield return AssertRegionTransition(manager, FieldPath);
            Assert.That(manager.Player, Is.SameAs(player), "같은 Player 유지");
            AssertUiKept(player);
            Assert.That(Terrain.activeTerrain != null && Terrain.activeTerrain.gameObject.scene == manager.CurrentRegion, Is.True, "TerrainTest 지면(#94)");
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor), "필드 카메라 배경");
            AssertFieldChestReplacement(manager);
            yield return AssertFieldMonsters(manager);

            // 필드 출구에 들어가면 마을의 필드 쪽 입구로 돌아옵니다.
            yield return EnterExit(manager, FindInRegion(manager, "FieldExit_"));
            yield return AssertRegionTransition(manager, VillagePath);

            AssertAtEntrance(manager, "Field");
            AssertUiKept(player);
            Assert.That(camera.clearFlags, Is.EqualTo(villageClear), "필드를 떠나면 카메라 복원");
            Assert.That(Object.FindObjectsByType(FieldMonstersType, FindObjectsSortMode.None).Length, Is.EqualTo(0), "필드를 떠나면 몬스터 모듈도 사라짐");
            Assert.That(CallInventory(player, "Remove", "scrap", int.MaxValue), Is.EqualTo(5), "가방 내용 유지");
        }

        [UnityTest]
        public IEnumerator FieldScenePlayedAlone_PlacesSamePlayerPrefab()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(FieldPath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(FieldPath));

            GameManager manager = GameManager.BootstrapRegion();
            Assert.That(manager, Is.Not.Null, "Field 단독 Play에서 생성");
            yield return WaitForActive(manager.Player);
            Assert.That(manager.Player.GetComponent("PlayerInventory"), Is.Not.Null, "메인 게임 Player 프리팹");
            Assert.That(FieldSeed(manager), Is.Not.EqualTo(51), "진입마다 새 seed(검토 씬 고정 seed 51 아님)");
            AssertFieldChestReplacement(manager);
            yield return AssertFieldMonsters(manager);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ShowsNoticeAndRestartsFromGameScene()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);

            GameObject player = manager.Player;
            Assert.That(CallInventory(player, "Add", "scrap", 5), Is.EqualTo(5), "재시작 전 가방에 넣기");
            Component restart = manager.GetComponentInChildren(DeathRestartType);
            Assert.That(restart, Is.Not.Null, "사망 재시작 모듈(GameConfig.modules)");
            yield return null;
            Assert.That(IsShowingDeath(restart), Is.False, "살아 있을 때는 안내 없음");

            // 낙하 피해는 구르기 무적과 관계없이 들어가므로 확실히 쓰러뜨릴 수 있습니다.
            Component health = player.GetComponent("Health");
            System.Reflection.MethodInfo damage = health.GetType().GetMethod("TryApplyDamage");
            object fall = System.Enum.Parse(damage.GetParameters()[1].ParameterType, "Fall");
            damage.Invoke(health, new[] { (object)int.MaxValue, fall });
            yield return null;
            Assert.That(IsShowingDeath(restart), Is.True, "사망 안내");

            // R 키 입력이 호출하는 재시작: Game 씬부터 새 Player로 마을 입구에서 시작합니다.
            manager.Restart();
            float end = Time.realtimeSinceStartup + Timeout;
            while (GameManager.Instance == null || GameManager.Instance.IsLoading || !GameManager.Instance.CurrentRegion.IsValid())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "재시작 시간 초과");
                yield return null;
            }

            GameManager next = GameManager.Instance;
            Assert.That(manager == null, Is.True, "이전 GameManager 정리");
            Assert.That(next.CurrentRegion.path, Is.EqualTo(VillagePath), "시작 지역에서 다시 시작");
            Assert.That(next.Player, Is.Not.SameAs(player), "새 Player");
            AssertAtEntrance(next, null);

            yield return null;
            AssertUiKept(next.Player);
            foreach (string ui in new[] { "Player HUD", "NomadInventory" })
                Assert.That(CountRoots(next.gameObject.scene, ui), Is.EqualTo(1), ui + " 이전 UI가 남지 않음");
            Assert.That(CallInventory(next.Player, "Remove", "scrap", int.MaxValue), Is.EqualTo(0), "가방 초기화");
            Assert.That(IsShowingDeath(next.GetComponentInChildren(DeathRestartType)), Is.False, "새 Player는 살아 있음");
        }

        [UnityTest]
        public IEnumerator NonRegionScene_DoesNotCreateManager()
        {
            // 테스트 러너 씬처럼 지역 목록에 없는 씬에서는 기존 동작을 바꾸지 않습니다.
            yield return null;
            Assert.That(GameManager.BootstrapRegion(), Is.Null);
            Assert.That(GameManager.Instance, Is.Null);
        }

        [UnityTest]
        public IEnumerator Escape_PausesAndSettingsKeepsGameplayPaused()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);
            Transform pause = manager.transform.Find("PauseMenu");
            Assert.That(pause, Is.Not.Null, "GameConfig에 등록된 일시정지 모듈");

            Vector3 position = manager.Player.transform.position;
            Behaviour interaction = (Behaviour)manager.Player.GetComponent("PlayerInteraction");
            float originalScale = Time.timeScale;
            bool originalAudioPause = AudioListener.pause;

            yield return PressEscape();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(interaction.enabled, Is.False);
            Assert.That(Vector3.Distance(manager.Player.transform.position, position), Is.LessThan(0.05f));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(PauseButton(pause, "PausePanel", "Resume")));

            Click(PauseButton(pause, "PausePanel", "Settings"));
            Assert.That(PauseButton(pause, "SettingsPanel", "Back").activeInHierarchy, Is.True);
            Assert.That(PauseButton(pause, "PausePanel", "Resume").activeInHierarchy, Is.False);
            Assert.That(Time.timeScale, Is.Zero);

            yield return PressEscape();
            Assert.That(PauseButton(pause, "SettingsPanel", "Back").activeInHierarchy, Is.False);
            Assert.That(Time.timeScale, Is.Zero);

            Click(PauseButton(pause, "PausePanel", "Resume"));
            Assert.That(Time.timeScale, Is.EqualTo(originalScale));
            Assert.That(AudioListener.pause, Is.EqualTo(originalAudioPause));
            Assert.That(interaction.enabled, Is.True);
            Assert.That(PauseButton(pause, "PausePanel", "Resume").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator Escape_AfterLootingClosesChestBeforePausing()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);

            Component inventory = manager.Player.GetComponent("PlayerInventory");
            Behaviour interaction = (Behaviour)manager.Player.GetComponent("PlayerInteraction");
            var internalMembers = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Type panelType = System.Type.GetType("Nomad.Inventory.InventoryPanel, Assembly-CSharp");
            var panel = (Behaviour)panelType.GetMethod("GetOrCreate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { inventory });
            var hasOpenChest = panelType.GetProperty("HasOpenChest", internalMembers);
            float originalScale = Time.timeScale;
            bool originalAudioPause = AudioListener.pause;

            // 필드와 같은 상자·가방을 사용하되, 랜덤 배치나 몬스터에 영향받지 않게 가까이 생성합니다.
            GameObject chestObject = new GameObject("Escape Test Chest");
            chestObject.transform.position = manager.Player.transform.TransformPoint(
                manager.Player.GetComponent<CharacterController>().center);
            try
            {
                System.Type chestType = System.Type.GetType("Nomad.Loot.LootChest, Assembly-CSharp");
                Component chest = chestObject.AddComponent(chestType);
                System.Type stackType = System.Type.GetType("Nomad.Loot.LootStack, Assembly-CSharp");
                System.Array stacks = System.Array.CreateInstance(stackType, 1);
                stacks.SetValue(System.Activator.CreateInstance(stackType, new object[] { "scrap", 2 }), 0);
                chestType.GetMethod("Initialize").Invoke(chest, new object[] { "escape-test-chest", stacks });
                panelType.GetMethod("ShowChest", internalMembers).Invoke(panel, new object[] { chest, interaction });
                Assert.That(hasOpenChest.GetValue(panel), Is.True, "상자 파밍 창 열림");
                yield return null;
                AssertLootLayout(panel, manager.Player);
                System.Type.GetType("Nomad.Loot.Editor.ChestInteractionVerification, Assembly-CSharp-Editor")
                    .GetMethod("Capture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { "issue200-main-loot" });
                Assert.That(chestType.GetMethod("TryLoot").Invoke(chest, new object[] { inventory }), Is.EqualTo(2));

                yield return PressEscape();
                Assert.That(hasOpenChest.GetValue(panel), Is.False, "첫 Esc는 상자만 닫음");
                var backpack = (GameObject)panelType.GetField("panel", internalMembers).GetValue(panel);
                Assert.That(backpack.activeSelf, Is.False, "가방도 닫힘");
                var equipmentPanel = manager.Player.GetComponent("EquipmentPanel");
                Assert.That(((RectTransform)equipmentPanel.GetType().GetProperty("StatsPanel").GetValue(equipmentPanel))
                    .gameObject.activeSelf, Is.True, "상자 닫은 뒤 스탯 창 복원");
                Assert.That(Time.timeScale, Is.EqualTo(originalScale), "게임 진행 유지");
                Assert.That(AudioListener.pause, Is.EqualTo(originalAudioPause));
                Assert.That(interaction.enabled && panel.enabled, Is.True, "일시정지로 입력을 끄지 않음");
                Assert.That(manager.transform.Find("PauseMenu/PauseCanvas/Backdrop"), Is.Null, "일시정지 창 생성 안 함");

                yield return PressEscape();
                Assert.That(Time.timeScale, Is.Zero, "다음 Esc는 일시정지");
                Assert.That(PauseButton(manager.transform.Find("PauseMenu"), "PausePanel", "Resume").activeInHierarchy, Is.True);

                yield return PressEscape();
                Assert.That(Time.timeScale, Is.EqualTo(originalScale), "다시 Esc로 재개");
                Assert.That(AudioListener.pause, Is.EqualTo(originalAudioPause));
                Assert.That(CallInventory(manager.Player, "Remove", "scrap", 2), Is.EqualTo(2), "획득한 아이템 보존");
            }
            finally
            {
                Object.Destroy(chestObject);
            }
        }

        [UnityTest]
        public IEnumerator PauseRestart_StartsFreshInVillage()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);
            GameObject player = manager.Player;
            Assert.That(CallInventory(player, "Add", "scrap", 5), Is.EqualTo(5));

            yield return PressEscape();
            Assert.That(Time.timeScale, Is.Zero);
            Click(PauseButton(manager.transform.Find("PauseMenu"), "PausePanel", "Restart"));

            float end = Time.realtimeSinceStartup + Timeout;
            while (GameManager.Instance == null || GameManager.Instance.IsLoading || !GameManager.Instance.CurrentRegion.IsValid())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "재시작 시간 초과");
                yield return null;
            }

            GameManager next = GameManager.Instance;
            Assert.That(manager == null, Is.True);
            Assert.That(next.CurrentRegion.path, Is.EqualTo(VillagePath));
            Assert.That(next.Player, Is.Not.SameAs(player));
            Assert.That(CallInventory(next.Player, "Remove", "scrap", int.MaxValue), Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            AssertAtEntrance(next, null);
        }

        [UnityTest]
        public IEnumerator EquipmentInput_DragRegisterNumberUseRightClickAndEscape()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);
            GameObject player = manager.Player;
            Component inventory = player.GetComponent("PlayerInventory");
            Component equipment = player.GetComponent("PlayerEquipment");
            Component panel = player.GetComponent("EquipmentPanel");
            Component quick = player.GetComponent("PlayerQuickSlots");
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Component hud = player.GetComponentInChildren(System.Type.GetType("Nomad.Hud.PlayerHud, Assembly-CSharp"));
            var quickRects = (RectTransform[])hud.GetType().GetField("quickRects", fields).GetValue(hud);
            var bag = (RectTransform)panel.GetType().GetProperty("BagGrid").GetValue(panel);
            var gear = (RectTransform)panel.GetType().GetProperty("EquipmentGrid").GetValue(panel);
            System.Type slotType = System.Type.GetType("Nomad.Equipment.EquipmentSlot, Assembly-CSharp");
            object body = System.Enum.ToObject(slotType, 3);

            bool background = Application.runInBackground;
            var behavior = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse previousMouse = Mouse.current;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();

            try
            {
                Assert.That(CallInventory(player, "Add", "gear-chain-mail", 1), Is.EqualTo(1));
                yield return PressKey(Key.Tab);
                Assert.That(panel.GetType().GetProperty("IsOpen").GetValue(panel), Is.True);
                Canvas.ForceUpdateCanvases();
                int armorSlot = FindBagItem(inventory, "gear-chain-mail");
                yield return DragItem(mouse, ScreenCenter((RectTransform)bag.GetChild(armorSlot)), ScreenCenter(quickRects[0]));
                Assert.That(quick.GetType().GetMethod("GetItem").Invoke(quick, new object[] { 0 }), Is.Not.Null, "장비 드래그 등록");

                int potionSlot = FindBagItem(inventory, "health-potion");
                yield return DragItem(mouse, ScreenCenter((RectTransform)bag.GetChild(potionSlot)), ScreenCenter(quickRects[3]));
                Assert.That(quick.GetType().GetMethod("GetCount").Invoke(quick, new object[] { 3 }), Is.EqualTo(3), "회복약 드래그 등록·잔량");
                yield return DragItem(mouse, ScreenCenter(quickRects[0]), ScreenCenter(quickRects[1]));
                Assert.That(quick.GetType().GetMethod("GetItem").Invoke(quick, new object[] { 0 }), Is.Null, "빈 퀵슬롯으로 이동");
                yield return DragItem(mouse, ScreenCenter(quickRects[1]), ScreenCenter(quickRects[3]));
                object swapped = quick.GetType().GetMethod("GetItem").Invoke(quick, new object[] { 1 });
                Assert.That(swapped.GetType().GetProperty("ItemId").GetValue(swapped), Is.EqualTo("health-potion"), "장비·회복약 퀵슬롯 교환");
                yield return DragItem(mouse, ScreenCenter(quickRects[3]), ScreenCenter(quickRects[0]));
                yield return DragItem(mouse, ScreenCenter(quickRects[1]), ScreenCenter(quickRects[3]));
                yield return PressKey(Key.Digit1);
                Assert.That(equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body }), Is.Null, "열린 창에서 번호 사용 차단");

                yield return PressEscape();
                Assert.That(panel.GetType().GetProperty("IsOpen").GetValue(panel), Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f), "첫 Esc는 장비만 닫음");
                Assert.That(manager.transform.Find("PauseMenu/PauseCanvas/Backdrop"), Is.Null, "퍼즈 생성 안 함");
                yield return PressKey(Key.Digit1);
                var armor = (Object)quick.GetType().GetMethod("GetItem").Invoke(quick, new object[] { 0 });
                Assert.That(equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body }), Is.SameAs(armor), "번호키 장착·등록 유지");

                Component health = player.GetComponent("Health");
                var damage = health.GetType().GetMethod("TryApplyDamage");
                damage.Invoke(health, new[] { (object)60, System.Enum.ToObject(damage.GetParameters()[1].ParameterType, 1) });
                int before = (int)health.GetType().GetProperty("CurrentHealth").GetValue(health);
                yield return PressKey(Key.Digit4);
                Assert.That(health.GetType().GetProperty("CurrentHealth").GetValue(health), Is.EqualTo(before + 40), "4번 회복약 사용");
                Assert.That(quick.GetType().GetMethod("GetCount").Invoke(quick, new object[] { 3 }), Is.EqualTo(2));

                yield return PressKey(Key.Tab);
                Canvas.ForceUpdateCanvases();
                // 장착 칸 우클릭으로 해제한 다음 가방에서 우클릭 한 번으로 다시 장착합니다.
                yield return RightClick(mouse, ScreenCenter((RectTransform)gear.GetChild(3)));
                Assert.That(equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body }), Is.Null);
                armorSlot = FindBagItem(inventory, "gear-chain-mail");
                yield return RightClick(mouse, ScreenCenter((RectTransform)bag.GetChild(armorSlot)));
                Assert.That(equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body }), Is.SameAs(armor), "우클릭 한 번 장착");
                yield return DragItem(mouse, ScreenCenter((RectTransform)gear.GetChild(3)), ScreenCenter(quickRects[1]));
                Assert.That(quick.GetType().GetMethod("GetItem").Invoke(quick, new object[] { 0 }), Is.Null, "같은 장비 등록 위치 이동");
                Assert.That(quick.GetType().GetMethod("GetItem").Invoke(quick, new object[] { 1 }), Is.SameAs(armor), "장착 칸에서도 직접 퀵슬롯 등록");
                Assert.That(equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body }), Is.SameAs(armor), "등록은 장비를 해제하지 않음");
                Assert.That(CallInventory(player, "Add", "gear-padded-armor", 1), Is.EqualTo(1));
                int replacementSlot = FindBagItem(inventory, "gear-padded-armor");
                yield return RightClick(mouse, ScreenCenter((RectTransform)bag.GetChild(replacementSlot)));
                object replacement = equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body });
                Assert.That(replacement.GetType().GetProperty("ItemId").GetValue(replacement), Is.EqualTo("gear-padded-armor"), "우클릭 한 번으로 기존 장비 교체");
                Assert.That(FindBagItem(inventory, "gear-chain-mail"), Is.GreaterThanOrEqualTo(0));
                yield return PressEscape();
                yield return PressKey(Key.Digit2);
                Assert.That(equipment.GetType().GetMethod("GetItem").Invoke(equipment, new[] { body }), Is.SameAs(armor), "교체 후 번호키로 재장착");
                yield return PressEscape();
                Assert.That(Time.timeScale, Is.Zero, "창 없는 다음 Esc는 퍼즈");
                yield return PressEscape();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                previousMouse?.MakeCurrent();
                InputSystem.settings.backgroundBehavior = behavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                Application.runInBackground = background;
            }
        }

        private static int FindBagItem(Component inventory, string id)
        {
            int count = (int)inventory.GetType().GetProperty("SlotCount").GetValue(inventory);
            for (int slot = 0; slot < count; slot++)
            {
                object stack = inventory.GetType().GetMethod("GetSlot").Invoke(inventory, new object[] { slot });
                object item = stack.GetType().GetProperty("Item").GetValue(stack);
                if (item != null && (string)item.GetType().GetProperty("ItemId").GetValue(item) == id) return slot;
            }
            Assert.Fail("가방에 없는 검증 아이템: " + id);
            return -1;
        }

        private static Vector2 ScreenCenter(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        private static IEnumerator DragItem(Mouse mouse, Vector2 source, Vector2 target)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = source }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = target }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = target });
            yield return null;
            yield return null;
        }

        private static IEnumerator RightClick(Mouse mouse, Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Right));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }

        private static IEnumerator PressEscape() => PressKey(Key.Escape);

        private static IEnumerator PressKey(Key key)
        {
            bool previousRunInBackground = Application.runInBackground;
            InputSettings settings = InputSystem.settings;
            var previousBackground = settings.backgroundBehavior;
            var previousEditorInput = settings.editorInputBehaviorInPlayMode;
            Keyboard previousKeyboard = Keyboard.current;

            // 배치 모드에서 Game 뷰에 포커스가 없어도 가상 키 입력을 처리합니다.
            Application.runInBackground = true;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();

            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null;
                Assert.That(keyboard[key].isPressed, Is.True, "가상 키가 Input System에 도달");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                previousKeyboard?.MakeCurrent();
                settings.editorInputBehaviorInPlayMode = previousEditorInput;
                settings.backgroundBehavior = previousBackground;
                Application.runInBackground = previousRunInBackground;
            }
        }

        private static GameObject PauseButton(Transform pause, string panel, string button)
        {
            Transform target = pause.Find($"PauseCanvas/Backdrop/{panel}/{button}");
            Assert.That(target, Is.Not.Null, button);
            return target.gameObject;
        }

        private static void Click(GameObject button)
        {
            Assert.That(button.activeInHierarchy, Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private static IEnumerator WaitForRegion(GameManager manager)
        {
            float end = Time.realtimeSinceStartup + Timeout;
            while (manager.IsLoading || !manager.CurrentRegion.IsValid())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "지역 로드 시간 초과");
                yield return null;
            }
        }

        private static IEnumerator EnterExit(GameManager manager, Transform exit)
        {
            Assert.That(exit, Is.Not.Null, "지역 출구");
            GameObject player = manager.Player;
            manager.PlacePlayer(exit.position + Vector3.up * 0.1f, Quaternion.identity);
            if (exit.GetComponent("FieldExit") != null)
            {
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(manager.IsLoading, Is.False, "전투 중 출구를 스쳐도 자동 귀환하지 않음");
                InputSettings settings = InputSystem.settings;
                var previousBackground = settings.backgroundBehavior;
                var previousEditor = settings.editorInputBehaviorInPlayMode;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                try
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                finally
                {
                    InputSystem.RemoveDevice(keyboard);
                    settings.backgroundBehavior = previousBackground;
                    settings.editorInputBehaviorInPlayMode = previousEditor;
                }
            }
            float end = Time.realtimeSinceStartup + Timeout;

            while (!manager.IsLoading)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "출구 트리거로 전환이 시작되어야 합니다.");

                // 마을→필드 출구는 필드 선택창을 띄웁니다(#194). 후보 3~4개 중 첫 필드를 골라 출발합니다.
                if (manager.IsChoosingField && manager.FieldChoices != null)
                {
                    Assert.That(manager.FieldChoices.Count, Is.InRange(3, 4), "필드 후보 3~4개");
                    FieldPreset chosen = manager.FieldChoices[0];
                    manager.ChooseField(chosen);
                    Assert.That(manager.SelectedField, Is.SameAs(chosen), "고른 필드 변형");
                    Assert.That(Time.timeScale, Is.EqualTo(1f), "선택 후 시간 재개");
                    yield return null;
                    continue;
                }

                player.GetComponent<CharacterController>().Move(Vector3.down * 0.01f);
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator AssertRegionTransition(GameManager manager, string destination)
        {
            float end = Time.realtimeSinceStartup + FieldTimeout;
            CanvasGroup overlay = null;
            bool fadedOut = false;
            bool showedLoading = false;
            bool fadedIn = false;

            while (manager.IsLoading)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "지역 전환 시간 초과");
                overlay ??= manager.transform.Find("RegionTransition")?.GetComponent<CanvasGroup>();

                if (overlay != null && overlay.gameObject.activeSelf)
                {
                    bool arrived = manager.CurrentRegion.path == destination;
                    bool fading = overlay.alpha > 0f && overlay.alpha < 1f;
                    if (overlay.transform.Find("Message").gameObject.activeSelf)
                    {
                        Assert.That(overlay.alpha, Is.EqualTo(1f), "로딩 안내는 화면을 완전히 덮은 뒤 표시");
                        showedLoading = true;
                    }

                    fadedOut |= !arrived && fading;
                    if (arrived && fading)
                    {
                        Assert.That(manager.Player.activeInHierarchy, Is.True, "목적지 플레이어 배치 후 페이드 인");
                        fadedIn = true;
                    }
                }

                yield return null;
            }

            Assert.That(manager.CurrentRegion.path, Is.EqualTo(destination));
            Assert.That(fadedOut && showedLoading && fadedIn, Is.True, "페이드 아웃 → 로딩 안내 → 페이드 인");
            Assert.That(overlay.gameObject.activeSelf, Is.False, "전환 화면 정리");
        }

        private static void AssertAtEntrance(GameManager manager, string fromRegion)
        {
            Assert.That(manager.Player.activeSelf, Is.True, "입구에 배치되어 활성화");
            RegionEntrance entrance = RegionEntrance.Find(manager.CurrentRegion, fromRegion);
            Assert.That(entrance, Is.Not.Null, "지역 입구");
            Assert.That(Vector3.Distance(manager.Player.transform.position, entrance.transform.position), Is.LessThan(0.5f), "입구 위치");
        }

        // 지역 씬이 바뀌어도 HUD·가방 UI가 사라지지 않도록 Player와 같은 씬에 있어야 합니다.
        private static void AssertUiKept(GameObject player)
        {
            foreach (string name in new[] { "Player HUD", "NomadInventory" })
            {
                GameObject ui = FindRoot(player.scene, name);
                Assert.That(ui, Is.Not.Null, name + "가 Player와 같은 씬에 있음");
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        private static Transform FindInRegion(GameManager manager, string prefix)
        {
            foreach (GameObject root in manager.CurrentRegion.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                {
                    if (child.name.StartsWith(prefix))
                        return child;
                }
            }

            return null;
        }

        private static void AssertFieldChestReplacement(GameManager manager)
        {
            GameObject[] roots = manager.CurrentRegion.GetRootGameObjects();
            System.Type scatterType = System.Type.GetType("Nomad.Props.PropScatter, Assembly-CSharp");
            System.Type chestType = System.Type.GetType("Nomad.Loot.LootChest, Assembly-CSharp");
            Component scatter = roots.SelectMany(root => root.GetComponentsInChildren(scatterType)).Single();
            var generated = (Transform)scatterType.GetProperty("GeneratedRoot").GetValue(scatter);
            Assert.That(generated == null || !generated.gameObject.activeInHierarchy, Is.True, "구형 큐브 숨김");

            var positions = ((IEnumerable)scatterType.GetProperty("Positions").GetValue(scatter)).Cast<Vector3>().ToArray();
            Component[] chests = roots.SelectMany(root => root.GetComponentsInChildren(chestType)).ToArray();
            Assert.That(chests.Length, Is.EqualTo(positions.Length), "구형 큐브 자리마다 신규 상자 1개");
            foreach (Component chest in chests)
                Assert.That(positions.Any(position => (position - chest.transform.position).sqrMagnitude < 0.0001f),
                    Is.True, "돌담·나무 등 다른 꾸밈에는 상자가 생기지 않음");
        }

        private static System.Type DeathRestartType => System.Type.GetType("Nomad.Game.Modules.DeathRestart, Assembly-CSharp");

        private static bool IsShowingDeath(Component restart) => (bool)DeathRestartType.GetProperty("IsShowing").GetValue(restart);

        private static int CountRoots(Scene scene, string name)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    count++;
            }

            return count;
        }

        private static System.Type FieldMonstersType => System.Type.GetType("Nomad.Game.Modules.FieldMonsters, Assembly-CSharp");

        // 필드 몬스터 지역 모듈(#89 5단계)이 필드 씬에 생성되어, 시작점 밖에 Player를 노리는 몬스터를 배치합니다.
        private static IEnumerator AssertFieldMonsters(GameManager manager)
        {
            Component module = null;
            foreach (GameObject root in manager.CurrentRegion.GetRootGameObjects())
            {
                if (module == null)
                    module = root.GetComponentInChildren(FieldMonstersType);
            }
            Assert.That(module, Is.Not.Null, "GameConfig에 등록된 Field 몬스터 모듈");

            // 모듈 Start가 생성 완료보다 늦을 수 있어 몇 프레임 기다립니다.
            var enemies = new System.Collections.Generic.List<Component>();
            float end = Time.realtimeSinceStartup + Timeout;
            while (enemies.Count == 0)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "몬스터 배치 시간 초과");
                yield return null;
                foreach (object enemy in (IEnumerable)FieldMonstersType.GetProperty("Enemies").GetValue(module))
                    enemies.Add((Component)enemy);
            }

            Assert.That(enemies.Count, Is.LessThanOrEqualTo(14), "일반12·엘리트1·보스1 이하");
            foreach (Component enemy in enemies)
            {
                Assert.That(enemy.gameObject.scene, Is.EqualTo(manager.CurrentRegion), "몬스터는 필드 씬에 생성");
                Assert.That(Vector3.Distance(enemy.transform.position, manager.Player.transform.position),
                    Is.GreaterThan(11f), "시작점 안전 거리 밖");
            }
        }

        private static int FieldSeed(GameManager manager)
        {
            foreach (GameObject root in manager.CurrentRegion.GetRootGameObjects())
            {
                Component controls = root.GetComponentInChildren(System.Type.GetType("Nomad.Terrain.TerrainIntegrationControls, Assembly-CSharp"));
                if (controls != null)
                    return (int)controls.GetType().GetProperty("Seed").GetValue(controls);
            }

            Assert.Fail("Field 생성 컨트롤");
            return 0;
        }

        private static void AssertLootLayout(Component inventoryPanel, GameObject player)
        {
            const System.Reflection.BindingFlags members = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var bag = (GameObject)inventoryPanel.GetType().GetField("panel", members).GetValue(inventoryPanel);
            var chest = (RectTransform)GameObject.Find("ChestLootPanel").transform;
            var bagCorners = new Vector3[4];
            var chestCorners = new Vector3[4];
            ((RectTransform)bag.transform).GetWorldCorners(bagCorners);
            chest.GetWorldCorners(chestCorners);
            Assert.That(bagCorners[3].x, Is.LessThan(chestCorners[0].x), "배율 적용 후 가방·상자 사이 여백");
            Assert.That(bagCorners[0].x, Is.GreaterThanOrEqualTo(0), "가방 화면 내부");
            Assert.That(chestCorners[3].x, Is.LessThanOrEqualTo(Screen.width), "상자 화면 내부");

            var equipment = player.GetComponent("EquipmentPanel");
            var stats = (RectTransform)equipment.GetType().GetProperty("StatsPanel").GetValue(equipment);
            Assert.That(stats.gameObject.activeSelf, Is.False, "루팅 중 겹치는 스탯 창 숨김");
        }

        // 테스트 어셈블리는 Player 쪽 스크립트를 참조할 수 없어 이름으로 호출합니다.
        private static int CallInventory(GameObject player, string method, string itemId, int count)
        {
            Component inventory = player.GetComponent("PlayerInventory");
            return (int)inventory.GetType().GetMethod(method, new[] { typeof(string), typeof(int) })
                .Invoke(inventory, new object[] { itemId, count });
        }

        private static IEnumerator WaitForActive(GameObject target)
        {
            float end = Time.realtimeSinceStartup + FieldTimeout;
            while (!target.activeSelf)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "필드 준비 후 플레이어 배치 시간 초과");
                yield return null;
            }
        }

        private static int CountLoaded(string path)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == path && SceneManager.GetSceneAt(i).isLoaded)
                    count++;
            }

            return count;
        }

        private static IEnumerator DestroyManager()
        {
            if (GameManager.Instance == null)
                yield break;

            Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }
    }
}
#endif
