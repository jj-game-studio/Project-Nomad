using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Nomad.MainMenu.Editor
{
    public static class MainMenuAuthoring
    {
        public const string Root = "Assets/Nomad/MainMenu";
        public const string ScenePath = Root + "/MainMenu.unity";
        public const string ContentPath = Root + "/MainMenuContent.asset";
        private static readonly Color Cream = new Color32(249, 237, 202, 255);
        private static readonly Color Gold = new Color32(229, 166, 63, 255);
        private static TMP_FontAsset bodyFont;
        private static TMP_FontAsset titleFont;

        [MenuItem("Nomad/Main Menu/Open Main Menu")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/Main Menu/Register Startup Scene")]
        public static void RegisterStartup()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath)).ToArray();
        }

        // 처음 한 번만 생성합니다. 기존 콘텐츠와 프리팹은 사용자의 편집을 보존합니다.
        public static void Create()
        {
            Directory.CreateDirectory(Root + "/Fonts");
            bodyFont = EnsureFont("Body", "Assets/Nomad/Shop/Resources/ShopFonts/NanumGothic-Regular.ttf");
            titleFont = EnsureFont("Title", "Assets/Nomad/Shop/Resources/ShopFonts/LilitaOne-Regular.ttf");
            var content = AssetDatabase.LoadAssetAtPath<MainMenuContent>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<MainMenuContent>();
                content.Background = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/CampBackground.png");
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            string prefabPath = Root + "/MainMenu.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject root = Build(content);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
            }

            if (!File.Exists(ScenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PrefabUtility.InstantiatePrefab(prefab, scene);
                var camera = new GameObject("MenuCamera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                camera.transform.position = new Vector3(0, 0, -10);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            RegisterStartup();
            AssetDatabase.SaveAssets();
            Debug.Log("MAIN_MENU_CREATED");
        }

        private static TMP_FontAsset EnsureFont(string name, string source)
        {
            string path = Root + "/Fonts/" + name + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
                return font;

            font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(source));
            font.name = "MainMenu" + name;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures)
                AssetDatabase.AddObjectToAsset(atlas, font);

            EditorUtility.SetDirty(font);
            return font;
        }

        private static GameObject Build(MainMenuContent content)
        {
            var root = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var screen = root.AddComponent<MainMenuScreen>();
            screen.Content = content;

            RectTransform crop = Rect("BackgroundCrop", root.transform, 0, 0, 0, 0);
            Stretch(crop);
            crop.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            RectTransform background = Rect("Background", crop, 0, 0, 1920, 1080);
            screen.Background = background.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            screen.Background.raycastTarget = false;
            background.anchorMin = background.anchorMax = background.pivot = Vector2.one * 0.5f;
            var fitter = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            RectTransform shade = Rect("ReadabilityShade", root.transform, 0, 0, 0, 0);
            Stretch(shade);
            shade.gameObject.AddComponent<MainMenuShade>().raycastTarget = false;
            RectTransform left = Rect("Content", root.transform, 72, 72, 570, 900);
            screen.ProjectName = Label("ProjectName", left, 0, 0, 550, 42, 22);
            screen.ProjectName.characterSpacing = 12;
            screen.Title = Label("Title", left, -4, 48, 550, 206, 190);
            screen.Title.font = titleFont;
            screen.Title.fontSizeMin = 62;
            screen.Title.fontSizeMax = 190;
            screen.Title.enableAutoSizing = true;
            screen.Title.verticalAlignment = VerticalAlignmentOptions.Middle;
            screen.Title.font.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            screen.Title.font.fallbackFontAssetTable.Add(bodyFont);
            screen.Title.textWrappingMode = TextWrappingModes.Normal;

            screen.Logo = Image("Logo", left, 0, 48, 550, 206, Color.white);
            screen.Logo.preserveAspect = true;
            Image("Divider", left, 0, 268, 505, 3, Gold);
            screen.Description = Label("Description", left, 0, 292, 520, 105, 32);
            screen.Description.enableAutoSizing = true;
            screen.Description.fontSizeMin = 23;
            screen.Description.fontSizeMax = 32;

            RectTransform menu = Rect("MenuPanel", left, 0, 420, 510, 300);
            screen.MenuPanel = menu.gameObject;
            screen.NewGameButton = Button("NewGame", menu, 0, true);
            screen.SettingsButton = Button("Settings", menu, 102, false);
            screen.QuitButton = Button("Quit", menu, 204, false);
            Navigation(screen.NewGameButton, screen.QuitButton, screen.SettingsButton);
            Navigation(screen.SettingsButton, screen.NewGameButton, screen.QuitButton);
            Navigation(screen.QuitButton, screen.SettingsButton, screen.NewGameButton);

            RectTransform settings = Rect("SettingsPanel", left, 0, 420, 510, 375);
            screen.SettingsPanel = settings.gameObject;
            Image("Surface", settings, -20, -12, 550, 390, new Color(0.02f, 0.045f, 0.03f, 0.87f));
            screen.SettingsTitle = Label("Heading", settings, 16, 0, 470, 54, 36);
            screen.VolumeLabel = Label("VolumeLabel", settings, 16, 78, 340, 38, 25);
            screen.VolumeValue = Label("VolumeValue", settings, 382, 78, 100, 38, 24);
            screen.VolumeSlider = Slider(settings);
            screen.FullscreenToggle = Toggle(settings);
            screen.FullscreenLabel = Label("FullscreenLabel", screen.FullscreenToggle.transform, 56, 0, 360, 42, 25);
            screen.BackButton = Button("Back", settings, 292, true);
            Navigation(screen.VolumeSlider, screen.BackButton, screen.FullscreenToggle);
            Navigation(screen.FullscreenToggle, screen.VolumeSlider, screen.BackButton);
            Navigation(screen.BackButton, screen.FullscreenToggle, screen.VolumeSlider);
            settings.gameObject.SetActive(false);

            screen.Footer = Label("Footer", root.transform, 52, 0, 680, 36, 17);
            var footerRect = (RectTransform)screen.Footer.transform;
            footerRect.anchorMin = footerRect.anchorMax = footerRect.pivot = new Vector2(0, 0);
            footerRect.anchoredPosition = new Vector2(52, 43);
            screen.Footer.characterSpacing = 6;
            screen.Footer.color = new Color32(205, 193, 157, 255);

            var fade = Image("Fade", root.transform, 0, 0, 0, 0, Color.black);
            Stretch((RectTransform)fade.transform);
            fade.raycastTarget = true;
            screen.Fade = fade.gameObject.AddComponent<CanvasGroup>();
            screen.Fade.alpha = 0;
            screen.Fade.blocksRaycasts = false;
            screen.Status = Label("Status", root.transform, 0, 0, 1200, 70, 25);
            var statusRect = (RectTransform)screen.Status.transform;
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.anchoredPosition = new Vector2(0, 70);
            screen.Status.alignment = TextAlignmentOptions.Center;

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            screen.RefreshContent();
            return root;
        }

        private static UnityEngine.UI.Slider Slider(Transform parent)
        {
            RectTransform rect = Rect("VolumeSlider", parent, 16, 124, 466, 40);
            Image("Track", rect, 0, 15, 466, 10, new Color(0.3f, 0.33f, 0.25f));
            var fill = Image("Fill", rect, 0, 15, 466, 10, Gold);
            Stretch((RectTransform)fill.transform);
            fill.rectTransform.offsetMin = new Vector2(0, 15);
            fill.rectTransform.offsetMax = new Vector2(0, -15);
            RectTransform handleArea = Rect("HandleArea", rect, 0, 0, 466, 40);
            var handle = Image("Handle", handleArea, 0, 0, 16, 32, Cream);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(1, 0.5f);
            handle.rectTransform.pivot = Vector2.one * 0.5f;
            handle.rectTransform.anchoredPosition = Vector2.zero;
            handle.raycastTarget = true;

            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;
            return slider;
        }

        private static UnityEngine.UI.Toggle Toggle(Transform parent)
        {
            var box = Image("FullscreenToggle", parent, 16, 207, 466, 42, Color.clear);
            box.raycastTarget = true;
            Image("Box", box.transform, 0, 3, 36, 36, new Color(0.25f, 0.29f, 0.22f));
            var mark = Image("Checkmark", box.transform, 6, 9, 24, 24, Gold);
            var toggle = box.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = box.transform.Find("Box").GetComponent<UnityEngine.UI.Image>();
            toggle.graphic = mark;
            return toggle;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, float y, bool primary)
        {
            var image = Image(name, parent, 0, y, 505, 82, Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Color normal = primary ? Gold : new Color(0.04f, 0.07f, 0.045f, 0.08f);
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = primary ? new Color32(249, 194, 94, 255) : new Color(0.34f, 0.38f, 0.24f, 0.7f);
            colors.selectedColor = primary ? Gold : new Color(0.34f, 0.38f, 0.24f, 0.7f);
            colors.pressedColor = new Color32(190, 131, 48, 255);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = Label("Label", image.transform, 34, 7, 435, 68, 40);
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.color = primary ? new Color32(24, 35, 24, 255) : Cream;
            text.enableAutoSizing = true;
            text.fontSizeMin = 25;
            text.fontSizeMax = 40;
            return button;
        }

        private static void Navigation(UnityEngine.UI.Selectable item, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down)
        {
            var navigation = item.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            item.navigation = navigation;
        }

        private static TMP_Text Label(string name, Transform parent, float x, float y, float w, float h, float size)
        {
            var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = bodyFont;
            label.text = "";
            label.fontSize = size;
            label.color = Cream;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Image Image(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
