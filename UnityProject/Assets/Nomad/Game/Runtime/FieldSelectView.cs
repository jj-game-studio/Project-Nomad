using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Game
{
    // 마을 출구에서 띄우는 필드 선택창입니다(#194). 카드를 누르면 출발하고, 취소하면 닫힙니다.
    // 색감·폰트는 마을 상점(ShopUi)과 맞춥니다(#196). 상점은 다른 어셈블리라 팔레트를 옮겨 둡니다.
    internal sealed class FieldSelectView
    {
        private static readonly Color32 Backdrop = new Color32(10, 18, 13, 170);
        private static readonly Color Canvas = new Color(0.975f, 0.947f, 0.88f);
        private static readonly Color Surface = new Color(1f, 0.98f, 0.93f);
        private static readonly Color Ink = new Color(0.23f, 0.22f, 0.15f);
        private static readonly Color Muted = new Color(0.49f, 0.46f, 0.36f);
        private static readonly Color Accent = new Color(0.95f, 0.69f, 0.22f);
        private static readonly Color GoldInk = new Color(0.50f, 0.35f, 0.13f);
        private static readonly Color Forest = new Color(0.14f, 0.25f, 0.17f);
        private static readonly Color Border = new Color(0.75f, 0.68f, 0.52f);

        private readonly GameObject root;
        private readonly RectTransform cardRow;
        private readonly TMP_FontAsset font;
        private readonly TMP_FontAsset headingFont;
        private readonly List<TMP_FontAsset> createdFonts = new();
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
            root.GetComponent<Image>().color = Backdrop;

            // 상점과 같은 Pretendard(본문)·Jua(제목)를 쓰고, 별 기호처럼 없는 글자는 맑은 고딕으로 채웁니다.
            TMP_FontAsset fallback = Track(TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular"));
            if (fallback == null)
                fallback = Resources.Load<TMP_FontAsset>("NomadHudFont");
            font = WithFallback(LoadFont("ShopFonts/Pretendard-Regular"), fallback);
            headingFont = WithFallback(LoadFont("ShopFonts/Jua-Regular"), fallback);

            // 크림색 패널 + 짙은 녹색 헤더(상점 창 구성)
            RectTransform panel = Box(root.transform, "Panel", Canvas, Vector2.zero, new Vector2(1600f, 820f), 3f);

            RectTransform header = Box(panel, "Header", Forest, new Vector2(0f, 350f), new Vector2(1600f, 120f), 0f);
            TextMeshProUGUI title = Label(header, "출격할 필드를 선택하세요", 50f, Surface, headingFont, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(-180f, 0f), new Vector2(1100f, 100f));

            Button cancelButton = ButtonBox(header, Surface, cancel);
            Place((RectTransform)cancelButton.transform, new Vector2(660f, 0f), new Vector2(200f, 66f));
            Place(Label(cancelButton.transform, "취소 (Esc)", 28f, Ink, headingFont).rectTransform, Vector2.zero, new Vector2(200f, 66f));

            TextMeshProUGUI hint = Label(panel, "카드를 누르거나 숫자 키로 고르세요", 24f, Muted);
            Place(hint.rectTransform, new Vector2(0f, 245f), new Vector2(1400f, 40f));

            cardRow = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            cardRow.SetParent(panel, false);
            Place(cardRow, new Vector2(0f, -60f), new Vector2(1520f, 520f));
            var layout = cardRow.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

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

        // 카드: 번호 배지·이름, 난이도 별, 컨셉, 구분선, 파밍 요약 태그 순서로 위에서부터 놓습니다.
        private GameObject CreateCard(FieldPreset preset, int number, Action choose)
        {
            Button button = ButtonBox(cardRow, Surface, choose);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(360f, 480f);

            // 번호 배지는 상점 수량 배지처럼 녹색 바탕에 흰 글자로 둡니다.
            RectTransform badge = Box(rect, "Number", Forest, new Vector2(-135f, 200f), new Vector2(54f, 44f), 0f);
            Place(Label(badge, number.ToString(), 28f, Surface, headingFont).rectTransform, Vector2.zero, new Vector2(54f, 44f));

            Place(Label(rect, preset.Name, 38f, Ink, headingFont).rectTransform,
                new Vector2(0f, 145f), new Vector2(330f, 60f));

            string stars = new string('★', preset.Difficulty) + new string('☆', 4 - preset.Difficulty);
            Place(Label(rect, $"난이도 {stars}", 26f, GoldInk).rectTransform,
                new Vector2(0f, 95f), new Vector2(330f, 40f));

            Place(Label(rect, preset.Concept, 22f, Muted).rectTransform,
                new Vector2(0f, 45f), new Vector2(320f, 56f));

            Box(rect, "Divider", Border, new Vector2(0f, 5f), new Vector2(300f, 2f), 0f);

            Place(Label(rect, "· " + string.Join("\n· ", preset.Tags()), 25f, Ink, null, TextAlignmentOptions.TopLeft).rectTransform,
                new Vector2(0f, -110f), new Vector2(300f, 210f));

            return button.gameObject;
        }

        private static Button ButtonBox(Transform parent, Color color, Action onClick)
        {
            var box = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            box.transform.SetParent(parent, false);
            box.GetComponent<Image>().color = color;
            Edge(box, 2f);

            // 상점 버튼처럼 마우스를 올리면 노란빛(Accent)으로 강조합니다.
            var button = box.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(Color.white, Accent, 0.35f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private static RectTransform Box(Transform parent, string name, Color color, Vector2 position, Vector2 size, float edge)
        {
            var box = new GameObject(name, typeof(RectTransform), typeof(Image));
            box.transform.SetParent(parent, false);

            Image image = box.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = edge > 0f;
            if (edge > 0f)
                Edge(box, edge);

            var rect = (RectTransform)box.transform;
            Place(rect, position, size);
            return rect;
        }

        // 상점의 베이지 테두리와 아래쪽 옅은 그림자를 흉내 냅니다.
        private static void Edge(GameObject box, float width)
        {
            var outline = box.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(width, -width);

            var shadow = box.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.15f, 0.11f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -6f);
        }

        private TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TMP_FontAsset labelFont = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = labelFont != null ? labelFont : font;
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

        private TMP_FontAsset LoadFont(string path)
        {
            var source = Resources.Load<Font>(path);
            return source != null ? Track(TMP_FontAsset.CreateFontAsset(source)) : null;
        }

        private TMP_FontAsset Track(TMP_FontAsset asset)
        {
            if (asset != null)
                createdFonts.Add(asset);
            return asset;
        }

        private static TMP_FontAsset WithFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
        {
            if (asset == null)
                return fallback;

            if (fallback != null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            return asset;
        }

        // 런타임에 만든 폰트 에셋과 아틀라스를 정리합니다.
        public void ReleaseFont()
        {
            foreach (TMP_FontAsset created in createdFonts)
            {
                foreach (Texture2D atlas in created.atlasTextures)
                    UnityEngine.Object.Destroy(atlas);

                UnityEngine.Object.Destroy(created.material);
                UnityEngine.Object.Destroy(created);
            }
            createdFonts.Clear();
        }
    }
}
