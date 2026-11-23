#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Nomad.Game;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.MainMenu.Tests
{
    public sealed class MainMenuTests
    {
        private const string MenuPath = "Assets/Nomad/MainMenu/MainMenu.unity";
        private MainMenuScreen screen;
        private MainMenuContent original;
        private Scene previousScene;
        private float oldVolume;
        private bool hadVolume;
        private float savedVolume;
        private bool hadFullscreen;
        private int savedFullscreen;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private bool oldRunInBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            oldVolume = AudioListener.volume;
            hadVolume = PlayerPrefs.HasKey(MainMenuSettings.VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(MainMenuSettings.VolumeKey);
            hadFullscreen = PlayerPrefs.HasKey(MainMenuSettings.FullscreenKey);
            savedFullscreen = PlayerPrefs.GetInt(MainMenuSettings.FullscreenKey);
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MenuPath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(MenuPath));
            screen = Object.FindFirstObjectByType<MainMenuScreen>();
            original = screen.Content;
            screen.Content = Object.Instantiate(original);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (screen != null)
            {
                Object.Destroy(screen.Content);
                screen.Content = original;
            }

            if (GameManager.Instance != null)
                Object.Destroy(GameManager.Instance.gameObject);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            Application.runInBackground = oldRunInBackground;
            AudioListener.volume = oldVolume;
            if (hadVolume)
                PlayerPrefs.SetFloat(MainMenuSettings.VolumeKey, savedVolume);
            else
                PlayerPrefs.DeleteKey(MainMenuSettings.VolumeKey);
            if (hadFullscreen)
                PlayerPrefs.SetInt(MainMenuSettings.FullscreenKey, savedFullscreen);
            else
                PlayerPrefs.DeleteKey(MainMenuSettings.FullscreenKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
            yield return null;

            SceneManager.SetActiveScene(previousScene.IsValid() && previousScene.isLoaded
                ? previousScene : SceneManager.CreateScene("Menu Test Host"));
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path == MenuPath || scene.path == "Assets/Nomad/Game/Game.unity" ||
                    scene.path == "Assets/Nomad/Village/Village.unity")
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator ContentAndLayouts_RenderAtCommonAspectRatios()
        {
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(MenuPath));
            Assert.That(screen.MenuPanel.GetComponentsInChildren<UnityEngine.UI.Button>().Length, Is.EqualTo(3));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(screen.NewGameButton.gameObject));
            Assert.That(GameManager.Instance, Is.Null, "메뉴에서는 게임 모듈을 생성하지 않음");
            Capture("menu-1920x1080", 1920, 1080);
            Capture("menu-1280x720", 1280, 720);
            Capture("menu-2560x1080", 2560, 1080);
            Capture("menu-1440x1080", 1440, 1080);

            screen.Content.Title = "황혼의 유랑자";
            screen.Content.Description = "살아남아, 가치를 옮기는 여정\n낯선 길 위에서 나만의 이야기를 만드세요.";
            screen.Content.NewGame = "여정 시작";
            screen.RefreshContent();
            Capture("menu-long-korean", 1920, 1080);
            screen.Title.ForceMeshUpdate();
            screen.Description.ForceMeshUpdate();
            Assert.That(screen.Title.isTextOverflowing, Is.False, "긴 한글 제목 잘림 없음");
            Assert.That(screen.Title.textInfo.lineCount, Is.LessThanOrEqualTo(2));
            Assert.That(screen.Description.isTextOverflowing, Is.False, "설명 잘림 없음");
            Assert.That(screen.NewGameButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("여정 시작"));

            var replacement = new Texture2D(32, 16);
            var logo = Sprite.Create(replacement, new Rect(0, 0, 32, 16), Vector2.one * 0.5f);
            screen.Content.Background = replacement;
            screen.Content.Logo = logo;
            screen.RefreshContent();
            Assert.That(screen.Background.texture, Is.SameAs(replacement));
            Assert.That(screen.Background.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio, Is.EqualTo(2f));
            Assert.That(screen.Title.gameObject.activeSelf, Is.False);
            Assert.That(screen.Logo.gameObject.activeSelf, Is.True);
            screen.Content.Background = original.Background;
            screen.Content.Logo = null;
            Object.Destroy(logo);
            Object.Destroy(replacement);

            screen.Content.Title = original.Title;
            screen.Content.Description = original.Description;
            screen.Content.NewGame = original.NewGame;
            screen.RefreshContent();
            screen.OpenSettings();
            Capture("menu-settings", 1920, 1080);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerKeyboardAndVolume_RespondAndReturnFocus()
        {
            yield return Click((RectTransform)screen.SettingsButton.transform);
            Assert.That(screen.IsShowingSettings, Is.True, "실제 포인터 클릭으로 설정 열림");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(screen.VolumeSlider.gameObject));

            screen.VolumeSlider.value = 0.35f;
            Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(0.01f));
            Assert.That(PlayerPrefs.GetFloat(MainMenuSettings.VolumeKey), Is.EqualTo(0.35f).Within(0.01f));
            yield return Click((RectTransform)screen.VolumeSlider.transform);
            Assert.That(screen.VolumeSlider.value, Is.InRange(0.45f, 0.55f), "슬라이더 중앙 실제 클릭");

            bool previousFullscreen = Screen.fullScreen;
            screen.FullscreenToggle.isOn = !previousFullscreen;
            Assert.That(PlayerPrefs.GetInt(MainMenuSettings.FullscreenKey), Is.EqualTo(previousFullscreen ? 0 : 1));
            screen.FullscreenToggle.isOn = previousFullscreen;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(screen.IsShowingSettings, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(screen.NewGameButton.gameObject));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(screen.SettingsButton.gameObject));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(screen.IsShowingSettings, Is.True, "키보드 선택·실행");
        }

        [UnityTest]
        public IEnumerator NewGame_LoadsExistingVillageAndIgnoresRepeatedStart()
        {
            MainMenuContent content = screen.Content;
            screen.NewGameButton.onClick.Invoke();
            screen.StartNewGame();
            Assert.That(screen.IsStarting, Is.True);
            Assert.That(screen.NewGameButton.interactable, Is.False);

            float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline &&
                (GameManager.Instance == null || GameManager.Instance.IsLoading || !GameManager.Instance.CurrentRegion.IsValid()))
                yield return null;

            Assert.That(GameManager.Instance, Is.Not.Null);
            Assert.That(GameManager.Instance.IsLoading, Is.False);
            Assert.That(GameManager.Instance.CurrentRegion.name, Is.EqualTo("Village"));
            Assert.That(GameManager.Instance.Player.activeSelf, Is.True);
            Assert.That(Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(SceneManager.GetSceneByPath(MenuPath).isLoaded, Is.False);
            Object.Destroy(content);
        }

        private IEnumerator Click(RectTransform rect)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }

        private void Capture(string name, int width, int height)
        {
            Canvas canvas = screen.GetComponent<Canvas>();
            Camera camera = GameObject.Find("MenuCamera").GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.cullingMask = ~0;
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                foreach (var label in screen.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.name + " overflow");
                }

                foreach (var button in screen.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var corners = new Vector3[4];
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 point = camera.WorldToScreenPoint(corner);
                        Assert.That(point.x, Is.InRange(-1f, width + 1f), button.name + " horizontal");
                        Assert.That(point.y, Is.InRange(-1f, height + 1f), button.name + " vertical");
                    }
                }

                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/main-menu");
                File.WriteAllBytes("Artifacts/main-menu/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(target);
                Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
