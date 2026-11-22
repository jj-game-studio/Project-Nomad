using System;
using System.Collections;
using System.Reflection;
using Nomad.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nomad.Camera.PlayModeTests
{
    public sealed class CursorCameraTests
    {
        private static readonly Vector3 BaseOffset = new(0, 12, -8);
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private UnityEngine.Camera view;
        private Component follow;
        private Transform target;
        private Mouse mouse;
        private Mouse previousMouse;
        private RenderTexture texture;
        private Scene scene;
        private Scene original;
        private Scene terrainScene;
        private Keyboard keyboard;
        private Keyboard previousKeyboard;
        private float captureDelta;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Fields).SetValue(owner, value);
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Fields).GetValue(owner);
        private Vector3 Shift => view.transform.position - target.position - BaseOffset;
        private void Snap() => follow.GetType().GetMethod("ResetTracking").Invoke(follow, null);
        private void Focus(bool focused) => follow.SendMessage("OnApplicationFocus", focused);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            original = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Cursor Camera Test");
            SceneManager.SetActiveScene(scene);
            captureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousMouse = Mouse.current;
            mouse = InputSystem.AddDevice<Mouse>();

            target = new GameObject("Target").transform;
            GameObject cameraObject = new("Camera");
            cameraObject.SetActive(false);
            view = cameraObject.AddComponent<UnityEngine.Camera>();
            texture = new RenderTexture(960, 540, 16);
            view.targetTexture = texture;
            follow = cameraObject.AddComponent(RuntimeType("Nomad.Player.TopDownCamera"));
            Set(follow, "target", target);
            Set(follow, "smoothTime", 0f);
            cameraObject.SetActive(true);
            Focus(true);
            Point(Vector2.zero);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = captureDelta;
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            previousMouse?.MakeCurrent();
            SceneManager.SetActiveScene(original);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
            if (terrainScene.IsValid() && terrainScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(terrainScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            UnityEngine.Object.Destroy(texture);
        }

        private void Point(Vector2 normalized)
        {
            Rect rect = view.pixelRect;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = rect.center + normalized * rect.size * .5f });
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectionsDeadZoneClampAndStableCursor()
        {
            foreach (Vector2 direction in new[] { Vector2.zero, new Vector2(.05f, .05f), Vector2.right * .99f,
                Vector2.left * .99f, Vector2.up * .99f, Vector2.down * .99f, new Vector2(.99f, .99f) })
            {
                Point(direction);
                yield return Frames();
                if (direction.magnitude < .1f) Assert.That(Shift.magnitude, Is.LessThan(.001f));
                else
                {
                    Assert.That(Shift.magnitude, Is.InRange(1.97f, 2.001f));
                    Assert.That(Vector3.Dot(Shift.normalized, new Vector3(direction.x, 0, direction.y).normalized), Is.GreaterThan(.999f));
                }
            }

            Vector3 held = Shift;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(held, Shift), Is.LessThan(.001f), "고정 커서 드리프트 없음");
            target.position += Vector3.right * .1f;
            yield return Frames();
            Assert.That(Vector3.Distance(held, Shift), Is.LessThan(.001f), "플레이어 즉시 추적");
            Assert.That(view.fieldOfView, Is.EqualTo(50f));
            Assert.That(Quaternion.Angle(view.transform.rotation, Quaternion.LookRotation(-BaseOffset)), Is.LessThan(.001f));
            Assert.That(Shift.y, Is.EqualTo(0f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator SmoothingAndMaximumZero()
        {
            Set(follow, "smoothTime", .15f);
            Point(Vector2.right * .9f);
            yield return Frames();
            Assert.That(Shift.x, Is.InRange(.01f, 1.7f), "첫 프레임 보간");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Shift.x, Is.EqualTo(1.7778f).Within(.02f));
            Set(follow, "maxLookAhead", 0f);
            yield return Frames();
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator UiFocusAndPauseReturnToCenterWithOutsidePointer()
        {
            Point(Vector2.right * 1.1f);
            yield return Frames();
            Assert.That(Shift.x, Is.GreaterThan(1f));
            var window = new GameObject("Window", typeof(Canvas), typeof(GraphicRaycaster));
            window.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var image = new GameObject("Input", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(window.transform, false);
            Canvas.ForceUpdateCanvases();
            yield return Frames();
            Assert.That(Shift.magnitude, Is.LessThan(.001f), "창 밖 커서도 복귀");
            var group = window.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            yield return Frames();
            Assert.That(Shift.x, Is.GreaterThan(1f), "입력하지 않는 HUD 제외");
            window.SetActive(false);

            Focus(false);
            yield return Frames();
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
            Focus(true);
            Point(new Vector2(1.1f, 0));
            yield return Frames();
            Assert.That(Shift.x, Is.EqualTo(2f).Within(.001f), "포커스 복귀 시 화면 밖 커서는 가장자리 유지");
            Point(Vector2.right * .9f);
            Time.timeScale = 0f;
            yield return Frames();
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator OutsideViewportClampsEveryEdgeAndCornerWithoutRecentering()
        {
            foreach (Rect viewport in new[] { new Rect(0, 0, 1, 1), new Rect(.1f, .15f, .6f, .55f) })
            {
                view.rect = viewport;
                foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                    new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1) })
                {
                    Point(direction * .9999f);
                    yield return Frames();
                    Vector3 inside = Shift;
                    Point(direction * 1.0001f);
                    yield return Frames();
                    Assert.That(Vector3.Distance(Shift, inside), Is.LessThan(.001f), "경계 왕복 연속성");
                    Point(direction * 3f);
                    yield return Frames();
                    Assert.That(Shift.magnitude, Is.EqualTo(2f).Within(.001f));
                    Assert.That(Vector3.Dot(Shift.normalized, new Vector3(direction.x, 0, direction.y).normalized),
                        Is.GreaterThan(.999f), "경계 밖에서도 같은 방향 유지");
                }

                Point(new Vector2(1, .3f));
                yield return Frames();
                Vector3 edge = Shift;
                Point(new Vector2(4, .3f));
                yield return Frames();
                Assert.That(Vector3.Distance(Shift, edge), Is.LessThan(.001f), "축별 제한으로 화면 밖 거리와 무관한 방향");
            }
        }

        [UnityTest]
        public IEnumerator ActualInventoryOpenClose()
        {
            Component inventory = target.gameObject.AddComponent(RuntimeType("Nomad.Inventory.PlayerInventory"));
            Type panelType = RuntimeType("Nomad.Inventory.InventoryPanel");
            Component panel = (Component)panelType.GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { inventory });
            GameObject content = Get<GameObject>(panel, "panel");
            Point(Vector2.right * .9f);
            yield return Frames();
            Assert.That(Shift.x, Is.GreaterThan(1f));
            content.SetActive(true);
            Canvas.ForceUpdateCanvases();
            yield return Frames();
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
            content.SetActive(false);
            yield return Frames();
            Assert.That(Shift.x, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator TeleportTargetReplacementReenableAndViewport()
        {
            Point(Vector2.right * .9f);
            yield return Frames();
            target.position += Vector3.right * 10f;
            follow.SendMessage("LateUpdate");
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
            yield return Frames();
            Assert.That(Shift.x, Is.GreaterThan(1f));
            target = new GameObject("New Target").transform;
            Set(follow, "target", target);
            follow.SendMessage("LateUpdate");
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
            yield return Frames();
            target.gameObject.SetActive(false);
            follow.SendMessage("LateUpdate");
            target.gameObject.SetActive(true);
            follow.SendMessage("LateUpdate");
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
            yield return Frames();
            ((Behaviour)follow).enabled = false;
            ((Behaviour)follow).enabled = true;
            Assert.That(Shift.magnitude, Is.LessThan(.001f));
            target.position += Vector3.right * .1f;
            Snap();
            Assert.That(Shift.magnitude, Is.LessThan(.001f), "작은 순간이동 명시 초기화");
            view.rect = new Rect(.1f, .1f, .65f, .65f);
            Point(new Vector2(0, .9f));
            yield return Frames();
            Assert.That(Shift.z, Is.EqualTo(1.7778f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator ShootingAndWorldHudUseMovedCamera()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -.5f, 0);
            ground.transform.localScale = new Vector3(30, 1, 30);
            target.gameObject.AddComponent<CharacterController>();
            target.gameObject.AddComponent<Health>();
            Component movement = target.gameObject.AddComponent(RuntimeType("Nomad.Player.PlayerMovement"));
            ((Behaviour)movement).enabled = false;
            Component combat = target.gameObject.AddComponent(RuntimeType("Nomad.Player.PlayerCombat"));
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(target, false);
            muzzle.localPosition = new Vector3(0, 1.2f, .5f);
            Set(combat, "viewCamera", view);
            Set(combat, "muzzle", muzzle);
            var projectileObject = new GameObject("Projectile Template");
            projectileObject.transform.position = Vector3.down * 100f;
            Projectile template = projectileObject.AddComponent<Projectile>();
            template.enabled = false;
            Set(combat, "projectile", template);
            Component hud = target.gameObject.AddComponent(RuntimeType("Nomad.WorldHud.CharacterStaminaHud"));
            Set(hud, "viewCamera", view);
            hud.GetType().GetMethod("SetStamina").Invoke(hud, new object[] { 50f, 100f });
            Point(new Vector2(.75f, .2f));
            yield return Frames();
            Assert.That(Shift.magnitude, Is.GreaterThan(1f));

            Vector2 screen = view.WorldToScreenPoint(new Vector3(3, 0, 4));
            template.enabled = true;
            bool launched = (bool)combat.GetType().GetMethod("TryShoot").Invoke(combat, new object[] { screen });
            Assert.That(launched, Is.True);
            Vector3 aim = (Vector3)combat.GetType().GetProperty("AimPoint").GetValue(combat);
            Assert.That(Vector3.Distance(aim, new Vector3(3, 0, 4)), Is.LessThan(.01f));
            Canvas.ForceUpdateCanvases();
            Canvas hudCanvas = Get<Canvas>(hud, "canvas");
            RectTransform anchor = Get<RectTransform>(hud, "anchor");
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hudCanvas.transform,
                view.WorldToScreenPoint(target.position + Vector3.up), null, out Vector2 expected);
            Assert.That(Vector2.Distance(anchor.anchoredPosition, expected + new Vector2(100, -60)), Is.LessThan(.01f));
            Debug.Log("CURSOR_CAMERA_AIM_HUD_PASS");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator SavedTerrainIntegrationMovesAndReturnsFromUiAndOverview()
        {
            ((Behaviour)follow).enabled = false;
            view.enabled = false;
            const string path = "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity";
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Additive));
            terrainScene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(terrainScene);
            yield return Frames();
            Component controls = null;
            Type controlType = RuntimeType("Nomad.Terrain.TerrainIntegrationControls");
            foreach (GameObject root in terrainScene.GetRootGameObjects())
                if (root.GetComponentInChildren(controlType, true) is Component found) controls = found;
            Assert.That(controls, Is.Not.Null);
            target = ((GameObject)controlType.GetProperty("Player").GetValue(controls)).transform;
            Assert.That(target.gameObject.activeSelf, Is.True, "지형 생성·경로 준비 후 플레이어 공개");
            follow = (Component)UnityEngine.Object.FindAnyObjectByType(RuntimeType("Nomad.Player.TopDownCamera"));
            foreach (GameObject root in terrainScene.GetRootGameObjects())
                if (root.GetComponentInChildren(RuntimeType("Nomad.Player.TopDownCamera"), true) is Component found) follow = found;
            view = follow.GetComponent<UnityEngine.Camera>();
            Assert.That(Get<float>(follow, "maxLookAhead"), Is.EqualTo(2f));
            Assert.That(Get<float>(follow, "smoothTime"), Is.EqualTo(.15f));
            Assert.That(Get<float>(follow, "deadZone"), Is.EqualTo(.1f));
            Focus(true);
            previousKeyboard = Keyboard.current;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Point(Vector2.zero);
            Vector3 start = target.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(Vector3.Distance(start, target.position), Is.GreaterThan(.01f), "실제 지형에서 WASD 이동");
            ((Behaviour)target.GetComponent(RuntimeType("Nomad.Player.PlayerMovement"))).enabled = false;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Shift.magnitude, Is.LessThan(.01f));
            SaveCamera("camera-integration-center.png");
            Point(Vector2.right * .9f);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Shift.x, Is.InRange(1.7f, 2f));
            SaveCamera("camera-integration-right.png");
            Component interaction = target.GetComponent(RuntimeType("Nomad.Interaction.PlayerInteraction"));
            ((Behaviour)target.GetComponent(RuntimeType("Nomad.Player.PlayerMovement"))).enabled = true;
            CharacterController controller = target.GetComponent<CharacterController>();
            bool accessible = false;
            Type chestType = RuntimeType("Nomad.Loot.LootChest");
            MethodInfo canAccess = interaction.GetType().GetMethod("CanAccessChest", Fields);

            // 상자는 생성 지점에만 놓이므로, 시작 seed에 상자가 없으면 상자가 있는 seed로 다시 생성합니다.
            for (int seed = 0; UnityEngine.Object.FindAnyObjectByType(chestType) == null && seed < 20; seed++)
            {
                Set(controls, "seed", seed);
                controlType.GetMethod("Regenerate").Invoke(controls, new object[] { false });
                yield return Frames();
            }

            // 상자 단계에서는 몬스터 공격이 상자 창을 닫지 않도록 생성된 몬스터를 끕니다.
            Type gameplayType = RuntimeType("Nomad.Gameplay.GameplayIntegration");
            UnityEngine.Object gameplay = UnityEngine.Object.FindAnyObjectByType(gameplayType);
            if (gameplay != null)
                foreach (Health enemy in (IEnumerable)gameplayType.GetProperty("Enemies").GetValue(gameplay))
                    enemy.gameObject.SetActive(false);

            foreach (GameObject root in terrainScene.GetRootGameObjects())
            {
                foreach (Component chest in root.GetComponentsInChildren(chestType))
                {
                    foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                    {
                        // 상자 옆 1.2m 지점의 실제 지면에 발을 놓습니다.
                        Vector3 sample = chest.transform.position + direction * 1.2f;
                        if (!Physics.Raycast(sample + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 8f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                            continue;
                        controller.enabled = false;
                        target.position = hit.point + Vector3.up * .06f;
                        controller.enabled = true;
                        Physics.SyncTransforms();
                        accessible = (bool)canAccess.Invoke(interaction, new object[] { chest });
                        if (accessible) break;
                    }
                    if (accessible) break;
                }
                if (accessible) break;
            }
            Assert.That(accessible, Is.True, "실제 생성 상자 접근 위치");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Shift.magnitude, Is.LessThan(.01f), "저장 씬의 실제 E 상자 복귀");
            Type panelType = RuntimeType("Nomad.Inventory.InventoryPanel");
            Component panel = (Component)UnityEngine.Object.FindAnyObjectByType(panelType);
            Assert.That(panel, Is.Not.Null);
            Assert.That((bool)panelType.GetProperty("HasOpenChest", Fields).GetValue(panel), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Shift.x, Is.GreaterThan(1.7f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.O));
            yield return Frames();
            Assert.That(((Behaviour)follow).enabled, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.O));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(((Behaviour)follow).enabled, Is.True);
            Assert.That(Shift.x, Is.InRange(1.7f, 2f));
            Debug.Log("CURSOR_CAMERA_TERRAIN_PLAY_PASS");
        }

        private void SaveCamera(string name)
        {
            RenderTexture previous = view.targetTexture;
            RenderTexture active = RenderTexture.active;
            var capture = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            view.targetTexture = capture;
            view.Render();
            RenderTexture.active = capture;
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("Artifacts");
            System.IO.File.WriteAllBytes("Artifacts/" + name, image.EncodeToPNG());
            RenderTexture.active = active;
            view.targetTexture = previous;
            UnityEngine.Object.Destroy(capture);
            UnityEngine.Object.Destroy(image);
        }
#endif
    }
}
