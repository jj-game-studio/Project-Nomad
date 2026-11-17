using Nomad.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.Hud
{
    // 미리보기 전용 입력입니다. 실제 마나/스태미나 게임 규칙을 변경하지 않습니다.
    public sealed class HudPreview : MonoBehaviour
    {
        [SerializeField] private bool enableStyleComparison;

        private RectTransform optionsPanel;
        private TMP_Text comparisonTitle;
        private TMP_Text instructions;
        private TMP_Text dataHint;
        private PlayerHud hud;

        public bool ComparisonEnabled => enableStyleComparison;
        private Health health;
        private static PlayerHud.HudStyle selectedStyle = PlayerHud.HudStyle.Medieval;
        private GameObject controls;
        private UnityEngine.UI.Image originalButtonFill;
        private UnityEngine.UI.Image medievalButtonFill;

        public UnityEngine.UI.Button OriginalButton { get; private set; }
        public UnityEngine.UI.Button MedievalButton { get; private set; }

        private int mana = 75;
        private int stamina = 60;

        private void Start()
        {
            hud = GetComponent<PlayerHud>();
            health = GetComponent<Health>();
            CreateControls();
            SetComparisonEnabled(enableStyleComparison);
            Refresh();
            Debug.Log("HUD_PREVIEW_READY: Health is live; mana, stamina and weapon use preview data.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (enableStyleComparison && keyboard.f6Key.wasPressedThisFrame)
                SelectStyle(hud.Style == PlayerHud.HudStyle.Medieval ? PlayerHud.HudStyle.Original : PlayerHud.HudStyle.Medieval);
            if (keyboard.hKey.wasPressedThisFrame)
                health.TryApplyDamage(25);
            if (keyboard.mKey.wasPressedThisFrame)
                mana = (mana + 25) % 125;
            if (keyboard.tKey.wasPressedThisFrame)
                stamina = (stamina + 20) % 120;
            if (keyboard.rKey.wasPressedThisFrame)
                ResetPreview();

            Refresh();
        }

        public static void ResetPreview()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Nomad/Hud/HudPreview.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
        }

        private void Refresh()
        {
            hud.SetResources(mana, 100, stamina, 100);
            hud.SetWeapon("FIELD BLASTER");
        }

        public void SetComparisonEnabled(bool enabled)
        {
            enableStyleComparison = enabled;
            OriginalButton.gameObject.SetActive(enabled);
            MedievalButton.gameObject.SetActive(enabled);
            OriginalButton.interactable = MedievalButton.interactable = enabled;
            optionsPanel.sizeDelta = new Vector2(600, enabled ? 144 : 100);
            comparisonTitle.text = enabled ? "HUD STYLE COMPARISON  /  F6: switch" : "HUD PREVIEW";
            comparisonTitle.rectTransform.anchoredPosition = new Vector2(0, enabled ? 48 : 26);
            instructions.rectTransform.anchoredPosition = new Vector2(0, enabled ? -27 : -2);
            dataHint.rectTransform.anchoredPosition = new Vector2(0, enabled ? -51 : -28);
            SelectStyle(enabled ? selectedStyle : PlayerHud.HudStyle.Medieval);
        }

        private void SelectStyle(PlayerHud.HudStyle next)
        {
            if (!enableStyleComparison)
                next = PlayerHud.HudStyle.Medieval;
            selectedStyle = next;
            hud.SetStyle(next);
            originalButtonFill.color = next == PlayerHud.HudStyle.Original ? new Color32(120, 95, 53, 255) : new Color32(43, 48, 58, 255);
            medievalButtonFill.color = next == PlayerHud.HudStyle.Medieval ? new Color32(120, 95, 53, 255) : new Color32(43, 48, 58, 255);
        }

        private void CreateControls()
        {
            controls = new GameObject("HUD Preview Controls", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = controls.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = controls.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            RectTransform panel = UiRect("Style Options", controls.transform, new Vector2(600, 144), new Vector2(20, -20));
            optionsPanel = panel;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(15, 19, 25, 235);
            // 옵션 패널만 클릭을 받으며, 패널 클릭이 뒤쪽 전투 사격으로 전달되지 않습니다.
            background.raycastTarget = true;
            comparisonTitle = Text(panel, "HUD STYLE COMPARISON  /  F6: switch", new Vector2(560, 24), new Vector2(0, 48), 17);
            OriginalButton = StyleButton(panel, "Original", -145, PlayerHud.HudStyle.Original, out originalButtonFill);
            MedievalButton = StyleButton(panel, "Medieval fantasy", 145, PlayerHud.HudStyle.Medieval, out medievalButtonFill);
            instructions = Text(panel, "H: damage    M: mana    T: stamina    R: reset", new Vector2(560, 22), new Vector2(0, -27), 15);
            dataHint = Text(panel, "Health live / resources & weapon preview only", new Vector2(560, 22), new Vector2(0, -51), 14);
        }

        private UnityEngine.UI.Button StyleButton(Transform parent, string label, float x,
            PlayerHud.HudStyle value, out UnityEngine.UI.Image fill)
        {
            RectTransform rect = UiRect(label, parent, new Vector2(270, 35), new Vector2(x, 12));
            fill = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = fill;
            button.onClick.AddListener(() => SelectStyle(value));
            Text(rect, label, new Vector2(260, 30), Vector2.zero, 16);
            return button;
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string label, Vector2 size, Vector2 position, int fontSize)
        {
            var text = UiRect(label, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("NomadHudFont");
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void OnDestroy()
        {
            if (controls != null)
                Destroy(controls);
        }
    }
}
