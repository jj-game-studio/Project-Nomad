using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Nomad.Game.Modules
{
    internal sealed class PauseMenuView
    {
        private static readonly Color32 PanelColor = new(25, 30, 28, 252);
        private static readonly Color32 BorderColor = new(140, 117, 77, 255);
        private static readonly Color32 TextColor = new(248, 239, 218, 255);
        private static readonly Color32 ButtonColor = new(51, 59, 51, 255);
        private static readonly Color32 PrimaryColor = new(111, 83, 43, 255);
        private static readonly Color32 FocusColor = new(136, 102, 51, 255);

        private readonly GameObject overlay;
        private readonly RectTransform menu;
        private readonly RectTransform settings;
        private readonly UnityEngine.UI.Button resumeButton;
        private readonly UnityEngine.UI.Button backButton;
        private readonly TMP_FontAsset koreanFont;
        private readonly TMP_FontAsset font;

        public PauseMenuView(Transform parent, UnityAction resume, UnityAction restart, UnityAction openSettings,
            UnityAction quit, UnityAction back)
        {
            koreanFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            font = koreanFont != null ? koreanFont : Resources.Load<TMP_FontAsset>("NomadHudFont");

            overlay = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            overlay.SetActive(false);
            overlay.transform.SetParent(parent, false);

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

            RectTransform backdrop = AddImage("Backdrop", overlay.transform, Vector2.zero, 0f,
                new Color(0f, 0f, 0f, 0.66f), true);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;

            menu = AddPanel("PausePanel", backdrop, 418f);
            AddLabel("Title", menu, "일시정지", "Paused", new Vector2(384f, 52f), 145f, 38f);

            resumeButton = AddButton("Resume", menu, "재개", "Resume", 62f, resume, PrimaryColor);
            var restartButton = AddButton("Restart", menu, "재시작", "Restart", -6f, restart, ButtonColor);
            var settingsButton = AddButton("Settings", menu, "설정", "Settings", -74f, openSettings, ButtonColor);
            var quitButton = AddButton("Quit", menu, "종료", "Quit", -142f, quit, ButtonColor);
            SetNavigation(resumeButton, quitButton, restartButton);
            SetNavigation(restartButton, resumeButton, settingsButton);
            SetNavigation(settingsButton, restartButton, quitButton);
            SetNavigation(quitButton, settingsButton, resumeButton);

            settings = AddPanel("SettingsPanel", backdrop, 266f);
            AddLabel("Title", settings, "설정", "Settings", new Vector2(384f, 52f), 69f, 38f);
            backButton = AddButton("Back", settings, "돌아가기", "Back", -74f, back, PrimaryColor);
            var villageView = VillageViewSettingControl.Create(settings, font,
                new Vector2(40, -98), new Vector2(352, 48));
            SetNavigation(backButton, villageView, villageView);
            SetNavigation(villageView, backButton, backButton);
        }

        public void Show(bool showingSettings)
        {
            menu.gameObject.SetActive(!showingSettings);
            settings.gameObject.SetActive(showingSettings);
            overlay.SetActive(true);
            EventSystem.current?.SetSelectedGameObject((showingSettings ? backButton : resumeButton).gameObject);
        }

        public void Hide() => overlay.SetActive(false);

        public void ReleaseFont()
        {
            if (koreanFont == null)
                return;

            foreach (Texture2D atlas in koreanFont.atlasTextures)
                Object.Destroy(atlas);

            Object.Destroy(koreanFont.material);
            Object.Destroy(koreanFont);
        }

        private static RectTransform AddPanel(string name, Transform parent, float height)
        {
            RectTransform border = AddImage(name, parent, new Vector2(432f, height), 0f, BorderColor, false);
            AddImage("Surface", border, new Vector2(428f, height - 4f), 0f, PanelColor, false);
            return border;
        }

        private UnityEngine.UI.Button AddButton(string name, Transform parent, string korean, string english,
            float y, UnityAction click, Color baseColor)
        {
            Vector2 size = new(352f, 54f);
            RectTransform rect = AddImage(name, parent, size, y, Color.white, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.colors = new UnityEngine.UI.ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = FocusColor,
                selectedColor = FocusColor,
                pressedColor = PrimaryColor,
                disabledColor = Color.gray,
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };

            button.onClick.AddListener(click);
            AddLabel("Label", rect, korean, english, size, 0f, 24f);
            return button;
        }

        private static void SetNavigation(UnityEngine.UI.Button button, UnityEngine.UI.Button up, UnityEngine.UI.Button down)
        {
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down
            };
        }

        private void AddLabel(string name, Transform parent, string korean, string english,
            Vector2 size, float y, float fontSize)
        {
            RectTransform rect = AddRect(name, parent, size, y);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = koreanFont != null ? korean : english;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private static RectTransform AddImage(string name, Transform parent, Vector2 size, float y, Color color, bool raycast)
        {
            RectTransform rect = AddRect(name, parent, size, y);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static RectTransform AddRect(string name, Transform parent, Vector2 size, float y)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, y);
            return rect;
        }
    }
}
