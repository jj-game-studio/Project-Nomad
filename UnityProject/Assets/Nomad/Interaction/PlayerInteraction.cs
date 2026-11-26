using System;
using System.Collections.Generic;
using Nomad.Inventory;
using Nomad.Loot;
using Nomad.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerInventory), typeof(CharacterController))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private const float Reach = 2f;

        [SerializeField] private LayerMask targetLayers = Physics.AllLayers;
        [SerializeField] private LayerMask obstacleLayers = Physics.DefaultRaycastLayers;

        private PlayerMovement movement;
        private PlayerInventory inventory;
        private CharacterController controller;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            if (inventory != null && (inventory.BlocksTradeInput || inventory.Panel?.BlocksSplitInput == true))
                return;

            if (inventory != null && inventory.Panel != null && inventory.Panel.HasOpenChest)
            {
                if (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                    !CanAccessChest(inventory.Panel.OpenChest))
                    inventory.Panel.CloseChest();
                return;
            }

            // 이동 Update가 이번 프레임의 위치와 구르기 상태를 갱신한 뒤 입력을 처리합니다.
            if (Keyboard.current?.eKey.wasPressedThisFrame != true || !CanInteract())
                return;

            var context = new InteractionContext(transform, inventory);
            Vector3 body = transform.TransformPoint(controller.center);
            Vector3 feet = transform.TransformPoint(controller.center - Vector3.up * (controller.height * 0.5f));
            IInteractable target = FindTarget(context, body, feet);
            if (target is LootChest chest)
                InventoryPanel.GetOrCreate(inventory)?.ShowChest(chest, this);
            else
                target?.Interact(context);
        }

        private void OnDisable()
        {
            if (inventory != null && inventory.Panel != null)
                inventory.Panel.CloseChest();
        }

        internal bool CanAccessChest(LootChest chest)
        {
            if (!isActiveAndEnabled || chest == null || !chest.isActiveAndEnabled || !CanInteract())
                return false;

            Vector3 body = transform.TransformPoint(controller.center);
            Vector3 feet = transform.TransformPoint(controller.center - Vector3.up * (controller.height * 0.5f));
            Vector3 point = chest.InteractionPoint;
            return (point - feet).sqrMagnitude <= Reach * Reach && !IsObstructed(body, point, chest.transform);
        }

        private bool CanInteract()
        {
            return movement != null && movement.isActiveAndEnabled && !movement.IsRolling && !movement.ViewInputBlocked &&
                   inventory != null && inventory.isActiveAndEnabled && controller != null && controller.enabled;
        }

        private IInteractable FindTarget(InteractionContext context, Vector3 body, Vector3 feet)
        {
            IInteractable best = null;
            float bestDistance = float.PositiveInfinity;
            string bestId = null;
            var seen = new HashSet<MonoBehaviour>();

            foreach (Collider collider in Physics.OverlapSphere(feet, Reach, targetLayers, QueryTriggerInteraction.Collide))
            {
                IInteractable candidate = collider.GetComponentInParent<IInteractable>();
                if (candidate is not MonoBehaviour owner || !owner.isActiveAndEnabled ||
                    owner.transform.IsChildOf(transform) || !seen.Add(owner))
                    continue;

                string id = candidate.InteractionId;
                Vector3 point = candidate.InteractionPoint;
                // 대상별 판정 거리로 나눈 비율로 비교합니다. 기본 2m 대상끼리는 기존 순서와 같습니다.
                float range = InteractionRange.Of(candidate);
                float distance = (point - feet).sqrMagnitude / (range * range);
                float score = SelectionScore(candidate, distance);
                if (string.IsNullOrWhiteSpace(id) || distance > 1f || (float.IsPositiveInfinity(score) || !(score <= bestDistance)) ||
                    (best != null && score == bestDistance && string.CompareOrdinal(id, bestId) >= 0))
                    continue;

                if (!candidate.CanInteract(context) || IsObstructed(body, point, owner.transform))
                    continue;

                best = candidate;
                bestDistance = score;
                bestId = id;
            }

            return best;
        }

        internal float SelectionScore(IInteractable candidate, float distance)
        {
            if (movement?.FirstPersonView != true) return distance;
            Camera camera = Camera.main;
            if (camera == null) return float.PositiveInfinity;
            Vector3 screen = camera.WorldToViewportPoint(InteractionAnchors.Focus(candidate));
            if (screen.z <= 0f || screen.x < 0f || screen.x > 1f || screen.y < 0f || screen.y > 1f)
                return float.PositiveInfinity;

            // 화면 중앙을 우선하고 같은 방향의 후보에서는 거리도 고려합니다.
            return Vector2.SqrMagnitude(new Vector2(screen.x - 0.5f, screen.y - 0.5f)) + distance * 0.001f;
        }

        private bool IsObstructed(Vector3 body, Vector3 point, Transform target)
        {
            Vector3 offset = point - body;
            float distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
                return false;

            // 플레이어와 대상 본체의 충돌체는 시야를 막지 않습니다.
            foreach (RaycastHit hit in Physics.RaycastAll(body, offset / distance, distance,
                         obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = hit.collider.transform;
                if (!hitTransform.IsChildOf(transform) && !hitTransform.IsChildOf(target))
                    return true;
            }

            return false;
        }
    }
}
