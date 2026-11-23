using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Combat;
using Nomad.Player;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // 무기 판정은 기존 전투가 담당하며 이 컴포넌트는 손과 상체의 표현만 담당합니다.
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAnimation))]
    public sealed class PlayerSwordAnimation : MonoBehaviour, IMeleeAttackTiming
    {
        public const string SwordId = "demo-sword";
        public const string LayerName = "Sword Upper Body";
        public const float StrikeNormalizedTime = 0.32f;

        private PlayerMovement movement;
        private PlayerEquipmentCombat combat;
        private EquipmentCombatVisuals visuals;
        private Animator animator;
        private Health health;
        private PlayerInventory inventory;
        private Transform visualRoot;
        private Quaternion restingRotation;
        private Quaternion attackFacing;
        private Transform socket;
        private int layer = -1;
        private float until;
        private bool swinging;

        public Transform Socket => socket;
        public bool IsSwinging => swinging;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<Health>();
            inventory = GetComponent<PlayerInventory>();
            visualRoot = transform.Find("Visual");
            if (visualRoot != null) restingRotation = visualRoot.localRotation;
            combat = GetComponent<PlayerEquipmentCombat>();
            visuals = GetComponent<EquipmentCombatVisuals>();
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || visuals == null || combat == null)
                return;

            layer = animator.GetLayerIndex(LayerName);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null || layer < 0) return;

            socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(hand, false);
            socket.localPosition = new Vector3(0.02f, -0.015f, 0.015f);
            socket.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.MeleeStarted += Attack;
                combat.MeleeCancelled += Stop;
            }
            if (socket != null && visuals != null) visuals.WeaponSocket = socket;
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.MeleeStarted -= Attack;
                combat.MeleeCancelled -= Stop;
            }
            if (visuals != null && visuals.WeaponSocket == socket) visuals.WeaponSocket = null;
            Stop();
        }

        public float GetWindup(ItemDefinition weapon) =>
            isActiveAndEnabled && layer >= 0 && animator != null && animator.isActiveAndEnabled &&
            weapon != null && weapon.ItemId == SwordId ? weapon.AttackInterval * StrikeNormalizedTime : 0f;

        private void Attack(ItemDefinition weapon, Vector3 direction)
        {
            attackFacing = Quaternion.LookRotation(direction);
            animator.SetFloat("SwordSpeed", 0.6f / weapon.AttackInterval);
            animator.Play("SwordAttack", layer, 0f);
            animator.SetLayerWeight(layer, 1f);
            until = Time.time + weapon.AttackInterval;
            swinging = true;
        }

        private void LateUpdate()
        {
            if (layer < 0) return;
            if (combat.CurrentWeapon?.ItemId != SwordId || movement.IsRolling ||
                !combat.isActiveAndEnabled || combat.InputBlocked || combat.EquipmentUiOpen || health.IsDead ||
                inventory?.BlocksTradeInput == true)
            {
                Stop();
                return;
            }

            if (swinging && Time.time >= until) swinging = false;
            float weight = swinging ? 1f : Mathf.MoveTowards(animator.GetLayerWeight(layer), 0f, Time.deltaTime * 12f);
            animator.SetLayerWeight(layer, weight);
            if (visualRoot != null)
                visualRoot.localRotation = weight > 0.01f ? Quaternion.Inverse(transform.rotation) * attackFacing : restingRotation;
        }

        private void Stop()
        {
            swinging = false;
            if (visualRoot != null) visualRoot.localRotation = restingRotation;
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}
