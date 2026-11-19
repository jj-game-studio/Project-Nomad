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

            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            foreach (IInteractable candidate in candidates)
            {
                if (VisibleCount == markers.Count) markers.Add(CreateMarker());
                Marker marker = markers[VisibleCount];
                if (!WorldHudCanvas.Place(canvas, marker.Root, camera, candidate.InteractionPoint, markerOffset))
                    continue;

                marker.Root.gameObject.SetActive(true);
                bool selected = ReferenceEquals(candidate, Selected);
                marker.Outer.color = selected ? new Color32(255, 210, 138, 255) : Color.white;
                marker.Root.localScale = Vector3.one * (selected ? 1.15f : 1);
                marker.Key.gameObject.SetActive(selected);
                VisibleCount++;
            }

            for (int i = VisibleCount; i < markers.Count; i++) markers[i].Root.gameObject.SetActive(false);
        }

        private void Collect()
        {
            Vector3 body = transform.TransformPoint(controller.center);
            Vector3 feet = transform.TransformPoint(controller.center - Vector3.up * (controller.height * .5f));
            var context = new InteractionContext(transform, inventory);
            float nearest = 4f;
            foreach (Collider collider in Physics.OverlapSphere(feet, 2f, targetLayers, QueryTriggerInteraction.Collide))
            {
                IInteractable candidate = collider.GetComponentInParent<IInteractable>();
                if (candidate is not MonoBehaviour owner || !owner.isActiveAndEnabled ||
                    owner.transform.IsChildOf(transform) || !seen.Add(owner)) continue;

                float distance = (candidate.InteractionPoint - feet).sqrMagnitude;
                if (string.IsNullOrWhiteSpace(candidate.InteractionId) || !(distance <= 4f) ||
                    !candidate.CanInteract(context) || Obstructed(body, candidate.InteractionPoint, owner.transform)) continue;

                candidates.Add(candidate);
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
            key.font = Resources.Load<TMP_FontAsset>("NomadHudFont");
            key.text = "E";
            key.fontSize = 19;
            key.alignment = TextAlignmentOptions.Center;
            key.color = Color.white;
            key.raycastTarget = false;
            key.rectTransform.sizeDelta = new Vector2(32, 24);
            key.rectTransform.anchoredPosition = new Vector2(0, -28);
            outer.gameObject.SetActive(false);
            return new Marker { Root = outer.rectTransform, Outer = outer, Key = key };
        }

        private void LateUpdate() => Refresh();
        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        private void OnDisable()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
            Selected = null;
            VisibleCount = 0;
        }
    }
}
