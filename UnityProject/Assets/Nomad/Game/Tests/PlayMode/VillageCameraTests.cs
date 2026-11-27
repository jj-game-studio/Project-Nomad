#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nomad.Game.Tests
{
    public sealed class VillageCameraTests
    {
        private Mouse mouse;
        private Keyboard keyboard;
        private Mouse oldMouse;
        private Keyboard oldKeyboard;
        private bool hadPreference;
        private int oldPreference;
        private bool oldBackground;
        private float previousCaptureDelta;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private Scene previousScene;
        private bool oldCursorVisible;
        private CursorLockMode oldCursorLock;
        private Canvas.WillRenderCanvases eyeObserver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadPreference = PlayerPrefs.HasKey(VillageViewSettings.PreferenceKey);
            oldPreference = PlayerPrefs.GetInt(VillageViewSettings.PreferenceKey);
            PlayerPrefs.DeleteKey(VillageViewSettings.PreferenceKey);

            previousScene = SceneManager.GetActiveScene();
            oldCursorVisible = Cursor.visible;
            oldCursorLock = Cursor.lockState;

            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            oldBackground = Application.runInBackground;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            oldMouse = Mouse.current;
            oldKeyboard = Keyboard.current;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent();
            keyboard.MakeCurrent();

            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (eyeObserver != null) Canvas.willRenderCanvases -= eyeObserver;
            eyeObserver = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Village Camera Test Host"));
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != SceneManager.GetActiveScene() && new[] { "Game", "Village", "Field", "MainMenu" }.Contains(scene.name))
                    yield return SceneManager.UnloadSceneAsync(scene);
            }

            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            oldMouse?.MakeCurrent();
            oldKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = oldBackground;
            Time.captureDeltaTime = previousCaptureDelta;
            Cursor.lockState = oldCursorLock;
            Cursor.visible = oldCursorVisible;
            if (hadPreference) PlayerPrefs.SetInt(VillageViewSettings.PreferenceKey, oldPreference);
            else PlayerPrefs.DeleteKey(VillageViewSettings.PreferenceKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator MenuChoicePersistsIntoDirectGameStart()
        {
            Assert.That(VillageViewSettings.FirstPerson, Is.False);
            yield return Load("MainMenu");
            ButtonNamed("Settings").onClick.Invoke();
            ButtonNamed("VillageView").onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt(VillageViewSettings.PreferenceKey), Is.EqualTo(1));
            Capture("menu-settings");
            yield return Frames();
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("MainMenu"));
            yield return Load("Game");
            yield return WaitRegion();
            Assert.That(State("IsActive"), Is.True, "직접 Game 실행도 저장된 1인칭 적용");
        }

        [UnityTest]
        public IEnumerator UiMovementAndFieldRoundTripRestoreState()
        {
            yield return Load("Game");
            yield return WaitRegion();
            GameManager game = GameManager.Instance;
            Camera camera = game.GetComponentInChildren<Camera>();
            Behaviour follow = camera.GetComponent(RuntimeType("Nomad.Player.TopDownCamera")) as Behaviour;
            Assert.That(State("IsActive"), Is.False);
            float fov = camera.fieldOfView;
            float near = camera.nearClipPlane;

            yield return KeyPress(Key.Escape);
            ButtonNamed("Settings").onClick.Invoke();
            ButtonNamed("VillageView").onClick.Invoke();
            yield return Frames();
            Assert.That(State("IsActive"), Is.True);
            Assert.That(State("InputBlocked"), Is.True);
            Capture("pause-settings");
            yield return KeyPress(Key.Escape);
            yield return KeyPress(Key.Escape);
            Controller().SendMessage("OnApplicationFocus", true);
            yield return Frames();
            Assert.That(State("InputBlocked"), Is.False);
            Assert.That(follow.enabled, Is.False);
            Assert.That(camera.fieldOfView, Is.EqualTo(70f));
            Assert.That(game.Player.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff), Is.True);
            Capture("village-first-person");

            Vector3 start = game.Player.transform.position;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(0.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(Vector3.Dot(game.Player.transform.position - start, right), Is.GreaterThan(0.1f));
            Assert.That(Vector3.Angle(game.Player.transform.forward, Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up)), Is.LessThan(1f));

            float renderedEyeOffset = float.NaN;
            eyeObserver = () => { if (camera != null && game.Player != null) renderedEyeOffset = camera.transform.position.y - game.Player.transform.position.y; };
            Canvas.willRenderCanvases += eyeObserver;
            yield return KeyPress(Key.Space);
            Assert.That(renderedEyeOffset, Is.EqualTo(1.65f).Within(0.01f), "렌더 직전 점프 눈높이");
            Assert.That(Vector3.Angle(camera.transform.up, Vector3.up), Is.LessThan(0.1f), "점프 모션에 시점이 흔들리지 않음");
            yield return new WaitForSeconds(0.8f);
            yield return KeyPress(Key.LeftShift);
            Assert.That(Vector3.Angle(camera.transform.up, Vector3.up), Is.LessThan(0.1f), "구르기에 시점이 뒤집히지 않음");
            yield return new WaitForSeconds(0.4f);
            yield return KeyPress(Key.Tab);
            Assert.That(State("InputBlocked"), Is.True, "가방·장비 UI 시선 중지");
            Vector3 stopped = game.Player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(Vector3.ProjectOnPlane(game.Player.transform.position - stopped, Vector3.up).magnitude, Is.LessThan(0.01f));
            yield return KeyPress(Key.Escape);
            Assert.That(State("InputBlocked"), Is.False);
            Controller().SendMessage("OnApplicationFocus", false);
            yield return Frames();
            Assert.That(State("InputBlocked"), Is.True);
            Controller().SendMessage("OnApplicationFocus", true);
            yield return Frames();
            Assert.That(State("InputBlocked"), Is.False);

            VillageViewSettings.SetFirstPerson(false);
            yield return Frames();
            Assert.That(follow.enabled, Is.True);
            Assert.That(camera.fieldOfView, Is.EqualTo(fov));
            Assert.That(camera.nearClipPlane, Is.EqualTo(near));
            Assert.That(game.Player.GetComponentsInChildren<Renderer>().Any(r => !r.forceRenderingOff), Is.True);
            VillageViewSettings.SetFirstPerson(true);
            yield return Frames();
            Assert.That(game.LoadRegion("Field"), Is.True);
            yield return WaitRegion();
            Assert.That(State("IsActive"), Is.False);
            Assert.That(follow.enabled, Is.True);
            Assert.That(camera.fieldOfView, Is.EqualTo(fov));
            Assert.That(game.LoadRegion("Village"), Is.True);
            float returnDeadline = Time.realtimeSinceStartup + 30f;
            while (game.CurrentRegion.name != "Village" || !game.Player.activeInHierarchy)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(returnDeadline));
                yield return null;
            }
            yield return Frames();
            Assert.That(State("IsActive"), Is.True, "페이드인 완료 전에 마을 시점을 적용");
            yield return WaitRegion();
            Assert.That(State("IsActive"), Is.True);
            Assert.That(PlayerPrefs.GetInt(VillageViewSettings.PreferenceKey), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MouseLookAndShopInteractionReleaseInput()
        {
            VillageViewSettings.SetFirstPerson(true);
            yield return Load("Game");
            yield return WaitRegion();
            Controller().SendMessage("OnApplicationFocus", true);
            yield return Frames();
            GameManager game = GameManager.Instance;
            Camera camera = game.GetComponentInChildren<Camera>();
            Quaternion before = camera.transform.rotation;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(200, -10000));
            yield return Frames();
            Assert.That(Quaternion.Angle(before, camera.transform.rotation), Is.GreaterThan(10f), "마우스 시선");
            float pitch = Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            Assert.That(pitch, Is.EqualTo(75f).Within(0.1f), "위아래 시선 제한");

            Component shop = Object.FindAnyObjectByType(RuntimeType("Nomad.Shop.Shopkeeper")) as Component;
            Vector3 point = (Vector3)shop.GetType().GetProperty("InteractionPoint").GetValue(shop);
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 position = point - forward * 1.4f;
            position.y = 0.05f;
            game.PlacePlayer(position, Quaternion.LookRotation(forward));
            yield return Frames();
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x), Is.EqualTo(0f).Within(0.1f), "배치 직후 커서 delta 억제");
            Component hints = game.Player.GetComponent(RuntimeType("Nomad.WorldHud.InteractionHints"));
            Assert.That(hints.GetType().GetProperty("Selected").GetValue(hints), Is.SameAs(shop), "몸을 바라보면 상점 선택");
            Transform head = shop.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            RectTransform key = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .First(t => t.name == "E Key" && t.gameObject.activeInHierarchy);
            Assert.That(RectTransformUtility.WorldToScreenPoint(null, key.position).y,
                Is.GreaterThan(camera.WorldToScreenPoint(head.position).y), "E 거래 안내는 NPC 머리 위");
            Capture("npc-head-hint");
            yield return KeyPress(Key.E);
            Component panel = Object.FindAnyObjectByType(RuntimeType("Nomad.Shop.ShopPanel")) as Component;
            Assert.That(panel, Is.Not.Null, "화면 중앙의 실제 상점 E 거래");
            Assert.That(State("InputBlocked"), Is.True);
            before = camera.transform.rotation;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(300, 300));
            yield return Frames();
            Assert.That(Quaternion.Angle(before, camera.transform.rotation), Is.LessThan(0.1f), "상점 중 시선 고정");
            Capture("shop-first-person");
            panel.GetType().GetMethod("Close").Invoke(panel, null);
            yield return Frames();
            Assert.That(State("InputBlocked"), Is.False, "상점 닫기 후 복구");
            game.Restart();
            yield return null;
            yield return WaitRegion();
            Assert.That(State("IsActive"), Is.True, "재시작에서도 저장된 선택");
        }

        [UnityTest]
        public IEnumerator ArmsWeaponsAndLandmarksFollowFirstPersonCamera()
        {
            VillageViewSettings.SetFirstPerson(true);
            yield return Load("Game");
            yield return WaitRegion();
            Controller().SendMessage("OnApplicationFocus", true);
            yield return Frames();
            GameManager game = GameManager.Instance;
            Camera camera = game.GetComponentInChildren<Camera>();
            Component presenter = camera.GetComponent(RuntimeType("Nomad.PlayerVisual.FirstPersonWeaponView"));
            Assert.That((bool)presenter.GetType().GetProperty("HasArms").GetValue(presenter), Is.True);
            Camera renderCamera = (Camera)presenter.GetType().GetProperty("RenderCamera").GetValue(presenter);
            Assert.That(renderCamera.enabled, Is.True);
            Assert.That(renderCamera.fieldOfView, Is.EqualTo(60f));
            Assert.That(camera.cullingMask & (1 << LayerMask.NameToLayer("FirstPersonView")), Is.Zero);
            SkinnedMeshRenderer[] arms = camera.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(arms.Length, Is.GreaterThan(0));
            Assert.That(arms.All(r => r.sharedMesh.subMeshCount > 0 && r.sharedMesh.GetIndexCount(0) > 0), Is.True);

            Component equipment = game.Player.GetComponent("PlayerEquipment");
            Component inventory = game.Player.GetComponent("PlayerInventory");
            Component combat = game.Player.GetComponent("PlayerEquipmentCombat");
            Vector2 center = camera.pixelRect.center;
            foreach (string id in new[] { "demo-sword", "demo-bow", "weapon-apprentice-staff" })
            {
                int found = -1;
                for (int i = 0; i < 20; i++)
                {
                    object stack = inventory.GetType().GetMethod("GetSlot").Invoke(inventory, new object[] { i });
                    object item = stack.GetType().GetProperty("Item").GetValue(stack);
                    if (item != null && (string)item.GetType().GetProperty("ItemId").GetValue(item) == id) { found = i; break; }
                }
                // 무기는 가방에 둔 채 퀵바 1번에 등록해 손에 듭니다.
                Assert.That(found, Is.GreaterThanOrEqualTo(0), id);
                Assert.That((bool)equipment.GetType().GetMethod("EquipWeapon").Invoke(equipment, new object[] { found, 0 }), Is.True);
                yield return Frames();
                GameObject model = (GameObject)presenter.GetType().GetProperty("WeaponModel").GetValue(presenter);
                Assert.That(model != null && model.activeInHierarchy && model.name.Contains(id), Is.True, id + " 실제 장착 외형");
                Assert.That(model.GetComponentsInChildren<Collider>().All(c => !c.enabled), Is.True);
                Assert.That(model.transform.parent.name, Is.EqualTo("Palm Grip Socket"), "무기는 손 소켓의 자식");
                Capture(id + "-idle");
                if (id == "demo-sword")
                {
                    Assert.That((bool)combat.GetType().GetMethod("TryAttack").Invoke(combat, new object[] { center }), Is.True);
                    Quaternion idleRotation = model.transform.rotation;
                    yield return new WaitForSeconds(0.12f);
                    Capture(id + "-windup");
                    Assert.That(Quaternion.Angle(model.transform.rotation, idleRotation), Is.GreaterThan(1f), "준비 모션이 보이는 무기에 반영");
                    yield return new WaitForSeconds(0.1f);
                    Capture(id + "-attack");
                    Component slash = Object.FindAnyObjectByType(RuntimeType("Nomad.Equipment.SwordSlashVfx")) as Component;
                    if (slash != null) Assert.That(slash.GetComponent<MeshRenderer>().enabled, Is.False, "탑다운용 넓은 베기 면 숨김");
                    yield return new WaitForSeconds(0.9f);
                    if (Environment.GetEnvironmentVariable("NOMAD_CAPTURE_MOTION") == "1")
                    {
                        Assert.That((bool)combat.GetType().GetMethod("TryAttack").Invoke(combat, new object[] { center }), Is.True);
                        yield return Motion("sword", 36);
                    }
                }
                else
                {
                    int arrows = (int)combat.GetType().GetProperty("ArrowCount").GetValue(combat);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Left, true));
                    yield return Frames();
                    Assert.That((bool)combat.GetType().GetProperty("IsCharging").GetValue(combat), Is.True, "실제 좌클릭 유지 차징");
                    yield return new WaitForSeconds(0.6f);
                    Assert.That((bool)combat.GetType().GetProperty("IsCharging").GetValue(combat), Is.True, "차징 유지 중 조기 취소 없음");
                    if (id == "demo-bow")
                    {
                        LineRenderer bowstring = model.GetComponentsInChildren<LineRenderer>().First(p => p.name == "Drawn Bowstring");
                        Assert.That(Vector3.Distance(bowstring.GetPosition(1), (bowstring.GetPosition(0) + bowstring.GetPosition(2)) * 0.5f),
                            Is.GreaterThan(0.1f), "차징 진행만큼 시위가 당겨짐");
                        Assert.That(model.GetComponentsInChildren<Transform>().Any(p => p.name == "Nocked Arrow"), Is.True);
                        Animator rig = camera.GetComponentInChildren<Animator>();
                        Vector3 hand = renderCamera.WorldToViewportPoint(rig.GetBoneTransform(HumanBodyBones.RightHand).position);
                        Assert.That(hand.x, Is.InRange(0.35f, 0.85f), "당기는 오른손이 화면 안에 표시");
                        Assert.That(hand.y, Is.InRange(0.05f, 0.55f));
                    }
                    else
                    {
                        ParticleSystem handCharge = camera.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "Hand Charge");
                        Assert.That(handCharge.particleCount, Is.GreaterThan(0), "표시 무기 끝의 차징 입자");
                    }
                    Capture(id + "-charge");
                    if (Environment.GetEnvironmentVariable("NOMAD_CAPTURE_MOTION") == "1") yield return Motion(id, 12);
                    Transform tip = model.GetComponentsInChildren<Transform>().First(t => t.name == "View Weapon Tip");
                    Vector3 origin = (Vector3)combat.GetType().GetProperty("ProjectileOrigin").GetValue(combat);
                    Assert.That(Vector3.Distance(tip.position, origin), Is.LessThan(0.001f), "보이는 발사구와 실제 발사 위치");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                    yield return Frames();
                    Assert.That((bool)combat.GetType().GetProperty("IsCharging").GetValue(combat), Is.False);
                    if (Environment.GetEnvironmentVariable("NOMAD_CAPTURE_MOTION") == "1") yield return Motion(id, 18, 12);
                    Assert.That((float)combat.GetType().GetProperty("LastShotPower").GetValue(combat), Is.GreaterThan(1.5f), "실제 차징 발사");
                    if (id == "demo-bow") Assert.That((int)combat.GetType().GetProperty("ArrowCount").GetValue(combat), Is.EqualTo(arrows - 1));
                    yield return new WaitForSeconds(0.9f);
                }
            }

            TextMesh[] signs = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            TextMesh sign = signs.FirstOrDefault(t => t.name == "Field direction");
            bool legacyBoard = sign != null;
            sign ??= signs.Single(t => t.name == "Gateway Title");
            Transform label = legacyBoard
                ? Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                    .First(t => t.name == "Attached Field Sign" && t.parent.name == "Signboard")
                : sign.transform;
            if (legacyBoard)
            {
                Assert.That(label.localPosition.z, Is.LessThan(-0.5f), "판 앞면에 고정");
                Assert.That(((RectTransform)label).sizeDelta.x, Is.GreaterThan(1.8f), "판 너비에 맞춘 문구");
            }
            else
            {
                Assert.That(sign.text, Is.EqualTo("필 드"), "교체된 관문의 실제 월드 안내");
                Assert.That(sign.GetComponent<Renderer>().enabled, Is.True, "새 관문 문구를 숨기지 않음");
            }

            Vector3 stand = sign.transform.position + (legacyBoard ? Vector3.back * 3f : Vector3.forward * 6f);
            stand.y = 0.05f;
            game.PlacePlayer(stand, Quaternion.identity);
            yield return Frames();
            if (!legacyBoard)
            {
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(1500, 200));
                yield return Frames();
            }
            Quaternion signRotation = label.rotation;
            Vector3 signPosition = label.position;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(150, 70));
            yield return Frames();
            Assert.That(Quaternion.Angle(label.rotation, signRotation), Is.LessThan(0.01f), "시선 회전에도 안내는 월드에 고정");
            Assert.That(Vector3.Distance(label.position, signPosition), Is.LessThan(0.001f));
            Assert.That(sign.GetComponent<Renderer>().enabled, Is.EqualTo(!legacyBoard));
            Capture(legacyBoard ? "first-person-landmark" : "first-person-gateway");
            VillageViewSettings.SetFirstPerson(false);
            yield return Frames();
            Assert.That(sign.GetComponent<Renderer>().enabled, Is.True, "탑다운에서도 월드 안내 표시");
            Assert.That(renderCamera.enabled, Is.False, "탑다운에서 별도 렌더 중단");
            Assert.That(camera.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(0), "1인칭 전용 팔 숨김");
        }

        [UnityTest]
        public IEnumerator FirstPersonChestUsesVisibleTargetAndRestoresInput()
        {
            VillageViewSettings.SetFirstPerson(true);
            yield return Load("Game");
            yield return WaitRegion();
            Controller().SendMessage("OnApplicationFocus", true);
            yield return Frames();
            GameObject player = GameManager.Instance.Player;
            Camera camera = GameManager.Instance.GetComponentInChildren<Camera>();
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            var chestObject = new GameObject("First Person Chest Test");
            try
            {
                chestObject.transform.position = player.transform.position + forward * 1.2f + Vector3.up;
                Component chest = chestObject.AddComponent(RuntimeType("Nomad.Loot.LootChest"));
                Type stackType = RuntimeType("Nomad.Loot.LootStack");
                Array stacks = Array.CreateInstance(stackType, 1);
                stacks.SetValue(Activator.CreateInstance(stackType, new object[] { "scrap", 1 }), 0);
                chest.GetType().GetMethod("Initialize").Invoke(chest, new object[] { "first-person-chest", stacks });
                chestObject.AddComponent<BoxCollider>().isTrigger = true;
                Physics.SyncTransforms();
                // 눈높이 아래의 상자를 실제로 바라봅니다. 앞쪽의 기도 나무보다 중앙에 오도록 합니다.
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(0, -235));
                yield return Frames();
                Component hints = player.GetComponent(RuntimeType("Nomad.WorldHud.InteractionHints"));
                Assert.That(hints.GetType().GetProperty("Selected").GetValue(hints), Is.SameAs(chest));
                yield return KeyPress(Key.E);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                Component inventory = player.GetComponent("PlayerInventory");
                Component panel = (Component)inventory.GetType().GetProperty("Panel", flags).GetValue(inventory);
                Assert.That((bool)panel.GetType().GetProperty("HasOpenChest", flags).GetValue(panel), Is.True, "앞의 상자 E로 열기");
                Assert.That(State("InputBlocked"), Is.True);
                yield return KeyPress(Key.Escape);
                Assert.That((bool)panel.GetType().GetProperty("HasOpenChest", flags).GetValue(panel), Is.False);
                Assert.That(State("InputBlocked"), Is.False);

                chestObject.transform.position = player.transform.position - forward * 1.2f + Vector3.up;
                Physics.SyncTransforms();
                yield return Frames();
                yield return KeyPress(Key.E);
                Assert.That((bool)panel.GetType().GetProperty("HasOpenChest", flags).GetValue(panel), Is.False, "등 뒤 상자는 선택하지 않음");
            }
            finally { Object.Destroy(chestObject); }
        }

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Component Controller() => GameManager.Instance.GetComponentInChildren(RuntimeType("Nomad.Player.VillageFirstPersonCamera"));
        private static bool State(string name) => (bool)Controller().GetType().GetProperty(name).GetValue(Controller());
        private static Button ButtonNamed(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == name);
        private static IEnumerator Frames() { for (int i = 0; i < 4; i++) yield return null; }
        private IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
        }

        private static IEnumerator Load(string name)
        {
            string path = name == "MainMenu" ? "Assets/Nomad/MainMenu/MainMenu.unity" : "Assets/Nomad/Game/Game.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return Frames();
        }

        private static IEnumerator WaitRegion()
        {
            float end = Time.realtimeSinceStartup + 120f;
            while (GameManager.Instance == null || GameManager.Instance.IsLoading || !GameManager.Instance.CurrentRegion.IsValid() ||
                !GameManager.Instance.Player.activeInHierarchy)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "지역 준비 시간 초과");
                yield return null;
            }
            yield return Frames();
        }

        private static IEnumerator Motion(string name, int count, int offset = 0)
        {
            Directory.CreateDirectory(Path.GetFullPath("Artifacts/village-camera/" + name));
            for (int frame = 0; frame < count; frame++)
            {
                yield return new WaitForSeconds(1f / 30f);
                Capture(name + "/" + (frame + offset).ToString("D4"));
            }
        }

        private static void Capture(string name)
        {
            string folder = Path.GetFullPath("Artifacts/village-camera");
            Directory.CreateDirectory(folder);
            Camera camera = Camera.main != null ? Camera.main : GameObject.Find("MenuCamera")?.GetComponent<Camera>();
            bool temporary = camera == null;
            if (temporary) camera = new GameObject("Menu Capture Camera").AddComponent<Camera>();
            int previousMask = camera.cullingMask;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Canvas[] overlays = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            try
            {
                camera.targetTexture = texture;
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 0.01f;

                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                camera.cullingMask = previousMask;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(texture);
                Object.Destroy(pixels);
                if (temporary) Object.Destroy(camera.gameObject);
            }
        }
    }
}
#endif
