using Nomad.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.WorldHud
{
    [DisallowMultipleComponent]
    public sealed class CharacterHealthHud : MonoBehaviour
    {
        private Health health;
        private Canvas canvas;
        private RectTransform anchor;
        private Image fill;
        private CanvasGroup group;
        public float Fraction => health != null ? health.CurrentHealth / (float)health.MaxHealth : 0;
        public bool Visible => group != null && group.alpha > 0;

        private void Awake()
        {
            health = GetComponent<Health>();
            canvas = WorldHudCanvas.Create("Character Health HUD", transform);

            var track = new GameObject("Health above head", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            track.transform.SetParent(canvas.transform, false);
            anchor = track.GetComponent<RectTransform>();
            anchor.sizeDelta = new Vector2(94, 12);
            track.GetComponent<Image>().color = new Color(0.07f, 0.04f, 0.04f, 0.95f);
            track.GetComponent<Image>().raycastTarget = false;
            group = track.GetComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;

            var inner = new GameObject("Health fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(anchor, false);
            fill = inner.GetComponent<Image>();
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(2, 0);
        }

        private void LateUpdate()
        {
            fill.rectTransform.sizeDelta = new Vector2(90 * Fraction, -4);
            fill.color = Color.Lerp(new Color(1, 0.2f, 0.1f), new Color(0.3f, 0.95f, 0.4f), Fraction);
            Position();
        }

        private void Position()
        {
            if (canvas == null || health == null) return;
            group.alpha = !health.IsDead && WorldHudCanvas.Place(canvas, anchor, Camera.main,
                transform.position + Vector3.up * 2.3f, new Vector2(0, 12)) ? 1 : 0;
        }

        private void OnEnable() { Canvas.willRenderCanvases += Position; if (canvas != null) canvas.enabled = true; }
        private void OnDisable() { Canvas.willRenderCanvases -= Position; if (canvas != null) canvas.enabled = false; }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
