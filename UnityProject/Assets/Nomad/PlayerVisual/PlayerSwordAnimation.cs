using Nomad.Equipment;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // 무기 판정은 기존 전투가 담당하며 이 컴포넌트는 손과 상체의 표현만 담당합니다.
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAnimation))]
    public sealed class PlayerSwordAnimation : MonoBehaviour
    {
        public const string SwordId = "demo-sword";
        public const string LayerName = "Sword Upper Body";
        public const float StrikeNormalizedTime = 0.32f;

        private PlayerAnimation locomotion;
        private PlayerEquipmentCombat combat;
        private EquipmentCombatVisuals visuals;
        private Animator animator;
        private Transform socket;
        private int layer = -1;
        private float until;
        private bool swinging;

        public Transform Socket => socket;
        public bool IsSwinging => swinging;

        private void Awake()
        {
            locomotion = GetComponent<PlayerAnimation>();
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
            socket.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private void OnEnable()
        {
            if (combat != null) combat.Attacked += Attack;
            if (socket != null && visuals != null) visuals.WeaponSocket = socket;
        }

        private void OnDisable()
        {
            if (combat != null) combat.Attacked -= Attack;
            if (visuals != null && visuals.WeaponSocket == socket) visuals.WeaponSocket = null;
            Stop();
        }

        private void Attack(WeaponAttackMode mode)
        {
            if (layer < 0 || mode != WeaponAttackMode.Melee || combat.CurrentWeapon?.ItemId != SwordId)
                return;

            // 기존 즉시 타격에 맞춰 접촉 구간부터 재생합니다. 준비 동작의 지연 판정은 별도 조율 대상입니다.
            animator.Play("SwordAttack", layer, StrikeNormalizedTime);
            animator.SetLayerWeight(layer, 1f);
            until = Time.time + combat.CurrentWeapon.AttackInterval * (1f - StrikeNormalizedTime);
            swinging = true;
        }

        private void LateUpdate()
        {
            if (layer < 0) return;
            if (combat.CurrentWeapon?.ItemId != SwordId || locomotion.CurrentState is "Roll" or "Jump" or "Fall" ||
                !combat.isActiveAndEnabled || combat.InputBlocked || combat.EquipmentUiOpen)
            {
                Stop();
                return;
            }

            if (swinging && Time.time >= until) swinging = false;
            float weight = swinging ? 1f : Mathf.MoveTowards(animator.GetLayerWeight(layer), 0f, Time.deltaTime * 12f);
            animator.SetLayerWeight(layer, weight);
        }

        private void Stop()
        {
            swinging = false;
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}
