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
            return movement != null && movement.isActiveAndEnabled && !movement.IsRolling &&
                   inventory != null && inventory.isActiveAndEnabled && controller != null && controller.enabled;
        }

        private IInteractable FindTarget(InteractionContext context, Vector3 body, Vector3 feet)
        {
            IInteractable best = null;
            float bestDistance = Reach * Reach;
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
                float distance = (point - feet).sqrMagnitude;
                if (string.IsNullOrWhiteSpace(id) || !(distance <= bestDistance) ||
                    (best != null && distance == bestDistance && string.CompareOrdinal(id, bestId) >= 0))
                    continue;

                if (!candidate.CanInteract(context) || IsObstructed(body, point, owner.transform))
                    continue;

                best = candidate;
                bestDistance = distance;
                bestId = id;
            }

            return best;
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
