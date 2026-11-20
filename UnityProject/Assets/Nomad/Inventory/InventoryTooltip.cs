using UnityEngine;

namespace Nomad.Inventory
{
    // 기존 Canvas/글꼴을 재사용하며 포인터 입력을 가로채지 않는 표시 전용 창입니다.
    internal sealed class InventoryTooltip
    {
        private const float Padding = 14f;
        private const float Margin = 8f;
        private const float CursorGap = 18f;
        private readonly RectTransform canvas;
        private readonly RectTransform root;
        private readonly UnityEngine.UI.Text title;
        private readonly UnityEngine.UI.Text description;
        private ItemDefinition shownItem;
        private int shownCount;
        private Vector2 canvasSize;
        private float textScale;
        private string shownName;
        private string shownDescription;

        public InventoryTooltip(RectTransform canvas, Font font)
        {
            this.canvas = canvas;
            root = CreateRect("InventoryTooltip", canvas);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(19, 20, 22, 248);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            title = CreateText("Title", font, 20, new Color32(224, 182, 104, 255));
            title.fontStyle = FontStyle.Bold;
            description = CreateText("Description", font, 16, new Color32(233, 234, 228, 255));
            Hide();
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
        }

        public void Show(ItemDefinition item, int count, Vector2 screenPosition, Camera eventCamera)
        {
            if (item == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas, screenPosition, eventCamera, out Vector2 pointer))
            {
                Hide();
                return;
            }

            if (shownItem != item || shownCount != count || canvasSize != canvas.rect.size ||
                shownName != item.DisplayName || shownDescription != item.Description ||
                !Mathf.Approximately(textScale, title.pixelsPerUnit))
            {
                shownItem = item;
                shownCount = count;
                shownName = item.DisplayName;
                shownDescription = item.Description;
                canvasSize = canvas.rect.size;
                textScale = title.pixelsPerUnit;
                Layout(item, count);
            }

            // 오른쪽/아래 공간이 없으면 커서 반대편으로 옮긴 뒤 Canvas 안으로 보정합니다.
            Rect bounds = canvas.rect;
            Vector2 size = root.sizeDelta;
            float x = pointer.x + CursorGap;
            float y = pointer.y - CursorGap;
            if (x + size.x > bounds.xMax - Margin)
                x = pointer.x - CursorGap - size.x;
            if (y - size.y < bounds.yMin + Margin)
                y = pointer.y + CursorGap + size.y;

            x = Mathf.Clamp(x, bounds.xMin + Margin, bounds.xMax - Margin - size.x);
            y = Mathf.Clamp(y, bounds.yMin + Margin + size.y, bounds.yMax - Margin);
            root.localPosition = new Vector3(x, y, 0);
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
        }

        private void Layout(ItemDefinition item, int count)
        {
            float width = Mathf.Min(320f, Mathf.Max(1f, canvasSize.x - Margin * 2));
            float textWidth = Mathf.Max(1f, width - Padding * 2);
            title.text = count > 1 ? $"{item.DisplayName}  ×{count}" : item.DisplayName;
            description.text = item.Description ?? string.Empty;
            title.rectTransform.sizeDelta = new Vector2(textWidth, 0);
            description.rectTransform.sizeDelta = new Vector2(textWidth, 0);

            // Canvas 배율에서 소수점 반올림으로 마지막 줄이 잘리지 않게 여유를 둡니다.
            float titleHeight = Mathf.Ceil(title.preferredHeight) + 2f;
            float gap = string.IsNullOrWhiteSpace(description.text) ? 0 : 8f;
            float bodyHeight = gap > 0 ? Mathf.Ceil(description.preferredHeight) + 2f : 0;
            float height = Mathf.Min(Padding * 2 + titleHeight + gap + bodyHeight,
                Mathf.Max(1f, canvasSize.y - Margin * 2));

            root.sizeDelta = new Vector2(width, height);
            title.rectTransform.anchoredPosition = new Vector2(Padding, -Padding);
            title.rectTransform.sizeDelta = new Vector2(textWidth, titleHeight);
            description.rectTransform.anchoredPosition = new Vector2(Padding, -Padding - titleHeight - gap);
            description.rectTransform.sizeDelta = new Vector2(textWidth,
                Mathf.Max(0, height - Padding * 2 - titleHeight - gap));
            description.gameObject.SetActive(gap > 0);
        }

        private UnityEngine.UI.Text CreateText(string name, Font font, int size, Color color)
        {
            RectTransform rect = CreateRect(name, root);
            rect.anchorMin = rect.anchorMax = Vector2.up;
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = parent.pivot;
            rect.pivot = Vector2.up;
            return rect;
        }
    }
}
