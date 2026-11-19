using UnityEngine;

namespace Nomad.WorldHud
{
    internal static class WorldHudCanvas
    {
        public static Canvas Create(string name, Transform owner)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(owner, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        public static WorldHudGraphic Arc(Transform parent, string name, float size, Color color,
            float start = 0, float sweep = 360, float thickness = 3)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            root.transform.SetParent(parent, false);
            var graphic = root.AddComponent<WorldHudGraphic>();
            graphic.rectTransform.sizeDelta = Vector2.one * size;
            graphic.color = color;
            graphic.StartAngle = start;
            graphic.Sweep = sweep;
            graphic.Thickness = thickness;
            graphic.raycastTarget = false;
            return graphic;
        }

        public static bool Place(Canvas canvas, RectTransform rect, Camera camera, Vector3 point, Vector2 offset)
        {
            if (camera == null) return false;
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= camera.nearClipPlane || screen.x < 0 || screen.y < 0 ||
                screen.x > Screen.width || screen.y > Screen.height) return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
                screen, null, out Vector2 local);
            rect.anchoredPosition = local + offset;
            return true;
        }
    }
}
