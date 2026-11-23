using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Player;
using TMPro;
using UnityEngine;

namespace Nomad.WorldHud
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInteraction))]
    public sealed class InteractionHints : MonoBehaviour
    {
        // PlayerInteraction의 레이어 설정과 일치해야 합니다. 기본값은 공용 E 판정과 동일합니다.
        [SerializeField] private LayerMask targetLayers = Physics.AllLayers;
        [SerializeField] private LayerMask obstacleLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector2 markerOffset = new Vector2(0, 28);
        [SerializeField, Min(2f)] private float displayRadius = 5f;
        private const string DefaultAction = "상호작용";
        private static TMP_FontAsset labelFont;
        private readonly List<Marker> markers = new();
        private readonly HashSet<MonoBehaviour> seen = new();
        private readonly List<IInteractable> candidates = new();
        private PlayerMovement movement;
        private PlayerInventory inventory;
        private CharacterController controller;
        private PlayerInteraction interaction;
        private Health health;
        private Canvas canvas;

        public IInteractable Selected { get; private set; }
        public int VisibleCount { get; private set; }

        private sealed class Marker
        {
            public RectTransform Root;
            public WorldHudGraphic Outer;
            public TMP_Text Key;
            public IInteractable Target;
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<CharacterController>();
            interaction = GetComponent<PlayerInteraction>();
            health = GetComponent<Health>();
            canvas = WorldHudCanvas.Create("Interaction Hints", transform);
        }

        public void Refresh()
        {
            Selected = null;
            VisibleCount = 0;
            candidates.Clear();
            seen.Clear();
            bool enabled = interaction.isActiveAndEnabled && movement != null && movement.isActiveAndEnabled &&
                !movement.IsRolling && controller != null && controller.enabled &&
                inventory != null && inventory.isActiveAndEnabled && (health == null || !health.IsDead) &&
                (inventory.Panel == null || !inventory.Panel.HasOpenChest);

            if (enabled) Collect();

            UpdatePositions();
        }

        private void UpdatePositions()
        {
            VisibleCount = 0;
            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            foreach (IInteractable candidate in candidates)
            {
                if (candidate is not MonoBehaviour owner || owner == null || !owner.isActiveAndEnabled) continue;
                if (VisibleCount == markers.Count) markers.Add(CreateMarker());
                Marker marker = markers[VisibleCount];
                if (!WorldHudCanvas.Place(canvas, marker.Root, camera, candidate.InteractionPoint, markerOffset))
                    continue;

                marker.Root.gameObject.SetActive(true);
                bool selected = ReferenceEquals(candidate, Selected);
                marker.Outer.color = selected ? new Color32(255, 210, 138, 255) : Color.white;
                marker.Root.localScale = Vector3.one * (selected ? 1.15f : 1);
                marker.Key.gameObject.SetActive(selected);
                if (selected && !ReferenceEquals(marker.Target, candidate))
                {
                    // 대상이 바뀔 때만 문구를 갱신해 매 프레임 텍스트 재구성을 피합니다.
                    marker.Target = candidate;
                    string action = (candidate as IInteractionAction)?.InteractionAction;
                    marker.Key.text = $"<b>E</b>  {(string.IsNullOrWhiteSpace(action) ? DefaultAction : action)}";
                }

                VisibleCount++;
            }

            for (int i = VisibleCount; i < markers.Count; i++) markers[i].Root.gameObject.SetActive(false);
        }

        private void Collect()
        {
            Vector3 body = transform.TransformPoint(controller.center);
            Vector3 feet = transform.TransformPoint(controller.center - Vector3.up * (controller.height * .5f));
            var context = new InteractionContext(transform, inventory);
            float nearest = 1f;
            float radius = Mathf.Max(2f, displayRadius);
            foreach (Collider collider in Physics.OverlapSphere(feet, radius, targetLayers, QueryTriggerInteraction.Collide))
            {
                IInteractable candidate = collider.GetComponentInParent<IInteractable>();
                if (candidate is not MonoBehaviour owner || !owner.isActiveAndEnabled ||
                    owner.transform.IsChildOf(transform) || !seen.Add(owner)) continue;

                // E 판정과 같게 대상별 범위로 나눈 비율을 씁니다. 표시 범위도 같은 비율만큼 넓힙니다.
                float range = InteractionRange.Of(candidate);
                float distance = (candidate.InteractionPoint - feet).sqrMagnitude / (range * range);
                float shown = radius / InteractionRange.Default;
                if (string.IsNullOrWhiteSpace(candidate.InteractionId) || !(distance <= shown * shown) ||
                    !candidate.CanInteract(context) || Obstructed(body, candidate.InteractionPoint, owner.transform)) continue;

                candidates.Add(candidate);
                // 멀리서는 원만 보여주고 실제 E 대상은 기존 2m 범위에서만 선택합니다.
                if (distance < nearest || (distance == nearest && (Selected == null ||
                    string.CompareOrdinal(candidate.InteractionId, Selected.InteractionId) < 0)))
                {
                    nearest = distance;
                    Selected = candidate;
                }
            }
        }

        private bool Obstructed(Vector3 body, Vector3 point, Transform target)
        {
            Vector3 offset = point - body;
            if (offset.magnitude <= Mathf.Epsilon) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(body, offset.normalized, offset.magnitude,
                obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return true;
            }
            return false;
        }

        private Marker CreateMarker()
        {
            var outer = WorldHudCanvas.Arc(canvas.transform, "Interaction Marker", 30, Color.white, thickness: 3);
            WorldHudCanvas.Arc(outer.transform, "Inner Ring", 15, Color.white, thickness: 3);
            var keyObject = new GameObject("E Key", typeof(RectTransform));
            keyObject.transform.SetParent(outer.transform, false);
            var key = keyObject.AddComponent<TextMeshProUGUI>();

            // 아이콘 위에 "E 행동"을 표시합니다. 행동 이름이 한글이라 한글 글꼴을 씁니다.
            key.font = LabelFont();
            key.text = "<b>E</b>  " + DefaultAction;
            key.fontSize = 18;
            key.alignment = TextAlignmentOptions.Center;
            key.textWrappingMode = TextWrappingModes.NoWrap;
            key.color = Color.white;
            key.outlineWidth = 0.2f;
            key.outlineColor = new Color32(0, 0, 0, 200);
            key.raycastTarget = false;
            key.rectTransform.sizeDelta = new Vector2(160, 26);
            key.rectTransform.anchoredPosition = new Vector2(0, 34);
            outer.gameObject.SetActive(false);
            return new Marker { Root = outer.rectTransform, Outer = outer, Key = key };
        }

        private static TMP_FontAsset LabelFont()
        {
            if (labelFont != null)
                return labelFont;

            var font = Resources.Load<Font>("ShopFonts/NanumGothic-Regular");
            labelFont = font != null ? TMP_FontAsset.CreateFontAsset(font) : null;
            return labelFont != null ? labelFont : Resources.Load<TMP_FontAsset>("NomadHudFont");
        }

        private void LateUpdate() => Refresh();
        private void OnEnable()
        {
            if (canvas != null) canvas.gameObject.SetActive(true);
            Canvas.willRenderCanvases += UpdatePositions;
        }
        private void OnDisable()
        {
            Canvas.willRenderCanvases -= UpdatePositions;
            if (canvas != null) canvas.gameObject.SetActive(false);
            Selected = null;
            VisibleCount = 0;
        }

        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
