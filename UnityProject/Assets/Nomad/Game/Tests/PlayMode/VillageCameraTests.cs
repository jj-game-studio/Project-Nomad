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
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private Scene previousScene;
        private bool oldCursorVisible;
        private CursorLockMode oldCursorLock;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadPreference = PlayerPrefs.HasKey(VillageViewSettings.PreferenceKey);
            oldPreference = PlayerPrefs.GetInt(VillageViewSettings.PreferenceKey);
            PlayerPrefs.DeleteKey(VillageViewSettings.PreferenceKey);

            previousScene = SceneManager.GetActiveScene();
            oldCursorVisible = Cursor.visible;
            oldCursorLock = Cursor.lockState;

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

            yield return KeyPress(Key.Space);
            Assert.That(camera.transform.position.y - game.Player.transform.position.y, Is.EqualTo(1.65f).Within(0.01f));
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
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(0, -375));
            yield return Frames();
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x), Is.EqualTo(45f).Within(0.1f));
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

        private static void Capture(string name)
        {
            string folder = Path.GetFullPath("Artifacts/village-camera");
            Directory.CreateDirectory(folder);
            Camera camera = Camera.main != null ? Camera.main : GameObject.Find("MenuCamera")?.GetComponent<Camera>();
            bool temporary = camera == null;
            if (temporary) camera = new GameObject("Menu Capture Camera").AddComponent<Camera>();
            int previousMask = camera.cullingMask;
            camera.cullingMask = ~0;
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
                    canvas.planeDistance = 1f;
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
