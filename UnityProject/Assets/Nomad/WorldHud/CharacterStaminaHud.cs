using Nomad.Combat;
using UnityEngine;

namespace Nomad.WorldHud
{
    [DisallowMultipleComponent]
    public sealed class CharacterStaminaHud : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector3 worldOffset = Vector3.up;
        [SerializeField] private Vector2 screenOffset = new Vector2(60, -40);
        [SerializeField, Min(0)] private float fullHoldSeconds = .7f;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform anchor;
        private WorldHudGraphic fill;
        private Health health;
        private float fullSince = float.NegativeInfinity;
        private float insufficientUntil;
        private bool available;
        private float opacity;

        public float Fraction { get; private set; }
        public bool Visible => canvas != null && canvas.isActiveAndEnabled && group != null && group.alpha > 0;

        private void Awake()
        {
            health = GetComponent<Health>();
            canvas = WorldHudCanvas.Create("Character Stamina HUD", transform);
            var track = WorldHudCanvas.Arc(canvas.transform, "Stamina Arc", 80,
                new Color32(25, 22, 19, 225), -65, 130, 7);
            anchor = track.rectTransform;
            group = track.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            group.alpha = 0;
            fill = WorldHudCanvas.Arc(anchor, "Fill", 78, new Color32(255, 193, 104, 255), -65, 130, 4);
        }

        // 게임에서 전달한 값만 표시하고 소비·회복 규칙은 소유하지 않습니다.
        public void SetStamina(float current, float maximum)
        {
            available = float.IsFinite(current) && float.IsFinite(maximum) && maximum > 0;
            float next = available ? Mathf.Clamp01(current / maximum) : 0;
            if (next < 1) fullSince = float.NegativeInfinity;
            else if (Fraction < 1) fullSince = Time.unscaledTime;
            Fraction = next;
            if (fill != null) fill.Fraction = next;
        }

        public void NotifyInsufficient() => insufficientUntil = Time.unscaledTime + .6f;

        private void LateUpdate()
        {
            bool alert = Time.unscaledTime < insufficientUntil;
            bool show = available && (health == null || !health.IsDead) &&
                (Fraction < 1 || Time.unscaledTime - fullSince < fullHoldSeconds || alert);
            opacity = show ? Mathf.MoveTowards(opacity, 1, Time.unscaledDeltaTime * 8) : 0;
            UpdatePosition();
            fill.color = alert && Mathf.Sin(Time.unscaledTime * 35) > 0
                ? new Color32(255, 95, 75, 255) : new Color32(255, 193, 104, 255);
        }

        private void UpdatePosition()
        {
            if (canvas == null) return;
            bool onscreen = WorldHudCanvas.Place(canvas, anchor, viewCamera != null ? viewCamera : Camera.main,
                transform.position + worldOffset, screenOffset);
            group.alpha = onscreen ? opacity : 0;
        }

        private void OnEnable()
        {
            if (canvas != null) canvas.gameObject.SetActive(true);
            // 추적 카메라의 LateUpdate가 끝난 뒤, 실제 렌더에 쓸 위치로 다시 투영합니다.
            Canvas.willRenderCanvases += UpdatePosition;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= UpdatePosition;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
