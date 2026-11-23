using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Game
{
    // 마을 출구에서 띄우는 필드 선택창입니다(#194). 카드를 누르면 출발하고, 취소하면 닫힙니다.
    internal sealed class FieldSelectView
    {
        private static readonly Color32 Background = new Color32(12, 15, 14, 225);
        private static readonly Color32 CardColor = new Color32(38, 44, 40, 255);
        private static readonly Color32 TextColor = new Color32(248, 239, 218, 255);
        private static readonly Color32 SubColor = new Color32(190, 182, 165, 255);
        private static readonly Color32 DangerColor = new Color32(214, 96, 72, 255);

        private readonly GameObject root;
        private readonly RectTransform cardRow;
        private readonly TMP_FontAsset font;
        private readonly TMP_FontAsset koreanFont;
        private readonly List<GameObject> cards = new();

        public bool IsOpen => root.activeSelf;

        public FieldSelectView(Transform parent, Action cancel)
        {
            root = new GameObject("FieldSelect", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(Image));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.GetComponent<Image>().color = Background;

            koreanFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            font = koreanFont != null ? koreanFont : Resources.Load<TMP_FontAsset>("NomadHudFont");

            TextMeshProUGUI title = Label(root.transform, "출격할 필드를 선택하세요", 46f, TextColor);
            Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1400f, 80f));

            cardRow = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            cardRow.SetParent(root.transform, false);
            Place(cardRow, new Vector2(0f, 10f), new Vector2(1760f, 520f));
            var layout = cardRow.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            Button cancelButton = ButtonBox(root.transform, new Color32(70, 70, 66, 255), cancel);
            Place((RectTransform)cancelButton.transform, new Vector2(0f, -360f), new Vector2(280f, 72f));
            Place(Label(cancelButton.transform, "취소 (Esc)", 30f, TextColor).rectTransform, Vector2.zero, new Vector2(280f, 72f));

            root.SetActive(false);
        }

        public void Show(IReadOnlyList<FieldPreset> presets, Action<FieldPreset> choose)
        {
            foreach (GameObject card in cards)
                UnityEngine.Object.Destroy(card);
            cards.Clear();

            for (int i = 0; i < presets.Count; i++)
            {
                FieldPreset preset = presets[i];
                cards.Add(CreateCard(preset, i + 1, () => choose(preset)));
            }

            root.SetActive(true);
        }

        public void Hide() => root.SetActive(false);

        // 카드: 번호·이름, 난이도 별, 컨셉, 파밍 요약 태그 순서로 위에서부터 놓습니다.
        private GameObject CreateCard(FieldPreset preset, int number, Action choose)
        {
            Button button = ButtonBox(cardRow, CardColor, choose);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(400f, 500f);

            Place(Label(rect, $"{number}. {preset.Name}", 34f, TextColor).rectTransform,
                new Vector2(0f, 200f), new Vector2(360f, 60f));

            string stars = new string('★', preset.Difficulty) + new string('☆', 4 - preset.Difficulty);
            Place(Label(rect, $"난이도 {stars}", 28f, DangerColor).rectTransform,
                new Vector2(0f, 145f), new Vector2(360f, 44f));

            Place(Label(rect, preset.Concept, 24f, SubColor).rectTransform,
                new Vector2(0f, 85f), new Vector2(360f, 70f));

            Place(Label(rect, "· " + string.Join("\n· ", preset.Tags()), 26f, TextColor, TextAlignmentOptions.TopLeft).rectTransform,
                new Vector2(0f, -110f), new Vector2(330f, 260f));

            return button.gameObject;
        }

        private static Button ButtonBox(Transform parent, Color32 color, Action onClick)
        {
            var box = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            box.transform.SetParent(parent, false);
            box.GetComponent<Image>().color = color;

            var button = box.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private TextMeshProUGUI Label(Transform parent, string text, float size, Color32 color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public void ReleaseFont()
        {
            if (koreanFont == null)
                return;

            foreach (Texture2D atlas in koreanFont.atlasTextures)
                UnityEngine.Object.Destroy(atlas);

            UnityEngine.Object.Destroy(koreanFont.material);
            UnityEngine.Object.Destroy(koreanFont);
        }
    }
}
