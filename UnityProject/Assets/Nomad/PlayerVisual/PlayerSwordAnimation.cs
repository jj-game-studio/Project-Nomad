using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Combat;
using Nomad.Player;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // 이동·피해 판정은 기존 시스템이 담당하며 조준 중 몸 방향과 손·상체 자세를 맞춥니다.
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
        private PlayerEquipment equipment;
        private EquipmentCombatVisuals visuals;
        private Animator animator;
        private Health health;
        private PlayerInventory inventory;
        private Transform visualRoot;
        private Quaternion restingRotation;
        private Quaternion attackFacing;
        private Transform socket;
        private Transform bowSocket;
        private ItemDefinition animatedWeapon;
        private int layer = -1;
        private float until;
        private bool swinging;

        public Transform Socket => socket;
        public bool IsSwinging => swinging;
        public bool HoldingStaff => isActiveAndEnabled && combat.CurrentWeapon?.AttackMode == WeaponAttackMode.Staff && !movement.IsRolling && !health.IsDead;
        public string CurrentAttackState { get; private set; }
        public WeaponAttackMode AnimatedMode => animatedWeapon != null ? animatedWeapon.AttackMode : WeaponAttackMode.None;
        public Quaternion AttackFacing
        {
            get
            {
                return combat.IsCharging ? combat.AimFacingRotation : attackFacing;
            }
        }
        private bool RangedPose => AnimatedMode == WeaponAttackMode.Bow || AnimatedMode == WeaponAttackMode.Staff || AnimatedMode == WeaponAttackMode.Ranged;

        public Quaternion StaffGripRotation(Quaternion handRotation, Quaternion goalRotation, float castBlend)
        {
            Quaternion grip = socket.localRotation * combat.CurrentWeapon.EquippedVisual.transform.localRotation;
            Vector3 currentShaft = handRotation * grip * Vector3.up;
            Quaternion facing = IsSwinging ? AttackFacing : transform.rotation;
            Vector3 heldShaft = facing * Quaternion.Euler(Mathf.Lerp(15f, 65f, castBlend), 0, 0) * Vector3.up;
            return Quaternion.FromToRotation(currentShaft, heldShaft) * goalRotation;
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<Health>();
            inventory = GetComponent<PlayerInventory>();
            visualRoot = transform.Find("Visual");
            if (visualRoot != null) restingRotation = visualRoot.localRotation;
            combat = GetComponent<PlayerEquipmentCombat>();
            equipment = GetComponent<PlayerEquipment>();
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
            var handPose = animator.GetComponent<WeaponHandPose>() ?? animator.gameObject.AddComponent<WeaponHandPose>();
            handPose.Owner = this;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (left != null)
            {
                bowSocket = new GameObject("BowSocket").transform;
                bowSocket.SetParent(left, false);
                bowSocket.localPosition = socket.localPosition;
                bowSocket.localRotation = socket.localRotation;
            }
        }

        private void OnEnable()
        {
            if (equipment != null) equipment.Changed += SelectSocket;
            if (combat != null)
            {
                combat.MeleeStarted += Attack;
                combat.MeleeCancelled += Stop;
                combat.Attacked += OtherWeaponAttack;
                combat.ChargeStarted += ChargePose;
                combat.ChargeCancelled += Stop;
            }
            if (socket != null && visuals != null) visuals.WeaponSocket = socket;
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.Changed -= SelectSocket;
            if (combat != null)
            {
                combat.MeleeStarted -= Attack;
                combat.MeleeCancelled -= Stop;
                combat.Attacked -= OtherWeaponAttack;
                combat.ChargeStarted -= ChargePose;
                combat.ChargeCancelled -= Stop;
            }
            if (visuals != null && (visuals.WeaponSocket == socket || visuals.WeaponSocket == bowSocket)) visuals.WeaponSocket = null;
            Stop();
        }

        private void SelectSocket()
        {
            if (visuals != null)
                visuals.WeaponSocket = combat.CurrentWeapon?.AttackMode == WeaponAttackMode.Bow && bowSocket != null ? bowSocket : socket;
        }

        public float GetWindup(ItemDefinition weapon) =>
            isActiveAndEnabled && layer >= 0 && animator != null && animator.isActiveAndEnabled &&
            weapon != null && weapon.ItemId == SwordId ? weapon.AttackInterval * StrikeNormalizedTime : 0f;

        private void Attack(ItemDefinition weapon, Vector3 direction)
        {
            attackFacing = Quaternion.LookRotation(direction);
            animatedWeapon = weapon;
            CurrentAttackState = weapon.AttackMode == WeaponAttackMode.Bow ? "BowAttack" :
                weapon.AttackMode == WeaponAttackMode.Staff ? "StaffAttack" :
                weapon.AttackMode == WeaponAttackMode.Ranged ? "RangedAttack" : "SwordAttack";
            animator.SetFloat("SwordSpeed", 0.6f / weapon.AttackInterval);
            animator.Play(CurrentAttackState, layer, 0f);
            animator.SetLayerWeight(layer, 1f);
            until = Time.time + weapon.AttackInterval;
            swinging = true;
            if (RangedPose)
            {
                movement.AimFacing = direction;
                transform.rotation = attackFacing;
                if (visualRoot != null) visualRoot.localRotation = restingRotation;
            }
        }

        private void ChargePose(ItemDefinition weapon, Vector3 direction)
        {
            Attack(weapon, direction);
            until = float.PositiveInfinity;
        }

        private void OtherWeaponAttack(WeaponAttackMode mode)
        {
            ItemDefinition weapon = combat.CurrentWeapon;
            if (layer < 0 || weapon == null || weapon.ItemId == SwordId) return;
            Vector3 aim = combat.AimPoint ?? combat.AttackOrigin + transform.forward;
            Vector3 direction = mode == WeaponAttackMode.Melee
                ? Vector3.ProjectOnPlane(aim - combat.AttackOrigin, Vector3.up)
                : combat.AimFacingRotation * Vector3.forward;
            Attack(weapon, direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward);
        }

        private void Update()
        {
            // 몸을 IK 평가 전에 돌립니다. IK 뒤에 외형을 다시 회전시키지 않습니다.
            if (layer < 0 || !swinging || !RangedPose) return;
            if (!combat.IsCharging && Time.time >= until)
            {
                movement.AimFacing = null;
                return;
            }
            movement.AimFacing = AttackFacing * Vector3.forward;
            transform.rotation = AttackFacing;
            if (visualRoot != null) visualRoot.localRotation = restingRotation;
        }

        private void LateUpdate()
        {
            if (layer < 0) return;
            if (combat.IsCharging && combat.AimPoint.HasValue)
            {
                attackFacing = AttackFacing;
            }
            SelectSocket();
            if ((swinging && combat.CurrentWeapon != animatedWeapon) || movement.IsRolling ||
                !combat.isActiveAndEnabled || combat.InputBlocked || combat.EquipmentUiOpen || health.IsDead ||
                inventory?.BlocksTradeInput == true)
            {
                Stop();
                return;
            }

            if (swinging && Time.time >= until)
            {
                swinging = false;
                if (RangedPose) movement.AimFacing = null;
            }
            float weight = swinging ? 1f : Mathf.MoveTowards(animator.GetLayerWeight(layer), 0f, Time.deltaTime * 12f);
            animator.SetLayerWeight(layer, weight);
            if (visualRoot != null)
                visualRoot.localRotation = !RangedPose && weight > 0.01f ? Quaternion.Inverse(transform.rotation) * attackFacing : restingRotation;
            // 지팡이는 손목 IK와 소켓을 그대로 따라가야 손에서 분리돼 보이지 않습니다.
            if (visuals.WeaponModel != null && combat.CurrentWeapon?.AttackMode == WeaponAttackMode.Bow)
                visuals.WeaponModel.transform.rotation = (weight > 0.01f ? AttackFacing : transform.rotation) * combat.CurrentWeapon.EquippedVisual.transform.localRotation;
        }

        private void Stop()
        {
            movement.AimFacing = null;
            swinging = false;
            animatedWeapon = null;
            CurrentAttackState = null;
            if (visualRoot != null) visualRoot.localRotation = restingRotation;
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}
