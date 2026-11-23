using System;
using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Inventory;
using Nomad.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerEquipment), typeof(PlayerMovement), typeof(Health))]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerEquipmentCombat : MonoBehaviour
    {
        public const string ArrowItemId = "weapon-arrow";
        public const float MeleeHalfAngle = 60f;

        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Projectile projectile;
        [SerializeField] private Projectile arrowProjectile;
        [SerializeField] private ItemDefinition unarmedDefinition;
        [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;

        private readonly List<RaycastResult> uiHits = new();
        private PlayerEquipment equipment;
        private PlayerMovement movement;
        private PlayerInventory inventory;
        private Health health;
        private PlayerMana mana;
        private PlayerCombat legacyCombat;
        private bool restoreLegacy;
        private bool holdingAttack;
        private float nextAttackTime;
        private bool inputBlocked;
        private bool equipmentUiOpen;
        private ItemDefinition pendingWeapon;
        private EquipmentSlot pendingSlot;
        private Vector3 pendingDirection;
        private float strikeTime;
        private IMeleeAttackTiming pendingTiming;
        private float walkableNormalY = Mathf.Cos(50f * Mathf.Deg2Rad);

        public event Action<WeaponAttackMode> Attacked;
        public event Action<ItemDefinition, Vector3> MeleeStarted;
        public event Action MeleeCancelled;
        public event Action<Vector3> Impacted;
        // 시각 효과가 무기별 연출을 고를 수 있도록 발사체와 명중 지점을 무기와 함께 알립니다.
        public event Action<Projectile, ItemDefinition> Fired;
        public event Action<Vector3, ItemDefinition> WeaponImpacted;
        public Vector3? AimPoint { get; private set; }
        public bool InputBlocked
        {
            get => inputBlocked;
            set { inputBlocked = value; if (value) CancelMelee(); }
        }
        public bool EquipmentUiOpen
        {
            get => equipmentUiOpen;
            internal set { equipmentUiOpen = value; if (value) CancelMelee(); }
        }
        public bool IsMeleePending => pendingWeapon != null;
        public Vector3 AttackOrigin => transform.position + Vector3.up;
        public Vector3 ProjectileOrigin => muzzle != null ? muzzle.position : AttackOrigin;
        public LayerMask CollisionLayers => collisionLayers;
        public ItemDefinition CurrentWeapon => equipment.ActiveWeapon != null ? equipment.ActiveWeapon : unarmedDefinition;
        public int ArrowCount => inventory != null ? inventory.GetCount(ArrowItemId) : 0;

        private void Awake()
        {
            equipment = GetComponent<PlayerEquipment>();
            movement = GetComponent<PlayerMovement>();
            TryGetComponent(out inventory);
            health = GetComponent<Health>();
            mana = GetComponent<PlayerMana>();
            legacyCombat = GetComponent<PlayerCombat>();
            if (TryGetComponent(out CharacterController controller))
                walkableNormalY = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
        }

        private void OnEnable()
        {
            equipment.Changed += EquipmentChanged;
            health.Died += OnDied;
            // 기존 전투 컴포넌트가 있는 프리팹에서도 한 클릭이 두 번 공격하지 않습니다.
            restoreLegacy = legacyCombat != null && legacyCombat.enabled;
            if (restoreLegacy)
                legacyCombat.enabled = false;
        }

        private void OnDisable()
        {
            equipment.Changed -= EquipmentChanged;
            health.Died -= OnDied;
            CancelMelee();
            if (restoreLegacy && legacyCombat != null)
                legacyCombat.enabled = true;
        }

        private void LateUpdate()
        {
            ResolveMelee();
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed)
            {
                holdingAttack = false;
                return;
            }

            // UI 조작으로 시작한 누르기는 손을 뗄 때까지 공격으로 이어지지 않습니다.
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                holdingAttack = !InputBlocked && !EquipmentUiOpen && inventory?.BlocksTradeInput != true && !IsOverUi(pointer);

            // 모든 무기는 누르고 있는 동안 공격 간격마다 계속 공격합니다.
            if (holdingAttack)
                TryAttack(pointer);
        }

        public bool TryAttack(Vector2 pointer)
        {
            if (IsMeleePending) return false;
            AimPoint = null;
            ItemDefinition weapon = CurrentWeapon;
            if (!isActiveAndEnabled || !equipment.isActiveAndEnabled || InputBlocked || EquipmentUiOpen ||
                inventory?.BlocksTradeInput == true || Time.timeScale <= 0 || health.IsDead || movement.BlocksAttack || Time.time < nextAttackTime ||
                weapon == null || weapon.EquipmentType != EquipmentType.Weapon || weapon.AttackPower <= 0 ||
                weapon.AttackRange <= 0 || weapon.AttackInterval <= 0)
                return false;

            if (IsOverUi(pointer) || !TryGetAim(pointer, out Vector3 aim))
                return false;

            WeaponAttackMode mode = weapon.AttackMode;
            if (mode == WeaponAttackMode.Melee)
            {
                Vector3 origin = AttackOrigin;
                Vector3 direction = Vector3.ProjectOnPlane(aim - origin, Vector3.up).normalized;
                if (direction.sqrMagnitude < 0.001f)
                    return false;

                nextAttackTime = Time.time + weapon.AttackInterval;
                var timing = GetComponent<IMeleeAttackTiming>();
                float windup = timing is Behaviour presenter && presenter.isActiveAndEnabled
                    ? timing.GetWindup(weapon) : 0f;

                if (windup > 0f && !float.IsInfinity(windup))
                {
                    pendingWeapon = weapon;
                    pendingSlot = equipment.ActiveWeaponSlot;
                    pendingDirection = direction;
                    pendingTiming = timing;
                    strikeTime = Time.time + Mathf.Min(windup, weapon.AttackInterval);

                    AimPoint = aim;
                    MeleeStarted?.Invoke(weapon, direction);
                    return true;
                }

                Melee(origin, direction, weapon);
            }
            else if (!TryFire(weapon, aim))
                return false;

            AimPoint = aim;
            Attacked?.Invoke(mode);
            return true;
        }

        private void ResolveMelee()
        {
            if (!IsMeleePending) return;
            if (!equipment.isActiveAndEnabled || CurrentWeapon != pendingWeapon ||
                equipment.ActiveWeaponSlot != pendingSlot || InputBlocked || EquipmentUiOpen ||
                inventory?.BlocksTradeInput == true || health.IsDead || movement.BlocksAttack || movement.IsRolling ||
                pendingTiming is not Behaviour presenter || presenter == null || !presenter.isActiveAndEnabled ||
                pendingTiming.GetWindup(pendingWeapon) <= 0f)
            {
                CancelMelee();
                return;
            }

            // 일시정지 중에는 취소 조건만 확인하고 준비 시간이 진행되지 않습니다.
            if (Time.timeScale <= 0f || Time.time < strikeTime) return;
            ItemDefinition weapon = pendingWeapon;
            Vector3 direction = pendingDirection;
            pendingWeapon = null;
            pendingTiming = null;
            AimPoint = AttackOrigin + direction * weapon.AttackRange;

            // 준비 중 이동한 현재 위치에서, 시작할 때 고정한 방향으로 한 번만 판정합니다.
            Melee(AttackOrigin, direction, weapon);
            Attacked?.Invoke(WeaponAttackMode.Melee);
        }

        private void CancelMelee()
        {
            if (!IsMeleePending) return;
            pendingWeapon = null;
            pendingTiming = null;
            MeleeCancelled?.Invoke();
        }

        private void EquipmentChanged()
        {
            if (IsMeleePending && (CurrentWeapon != pendingWeapon || equipment.ActiveWeaponSlot != pendingSlot))
                CancelMelee();
        }

        private void OnDied(Health _) => CancelMelee();

        private bool TryFire(ItemDefinition weapon, Vector3 aim)
        {
            WeaponAttackMode mode = weapon.AttackMode;
            bool bow = mode == WeaponAttackMode.Bow;
            bool staff = mode == WeaponAttackMode.Staff;
            if (muzzle == null || (!bow && !staff && mode != WeaponAttackMode.Ranged) ||
                (bow && ArrowCount == 0) || (staff && (mana == null || !mana.CanSpend(weapon.ManaCost))))
                return false;

            Projectile shot = bow ? arrowProjectile : projectile;
            if (!Projectile.TryLaunch(shot, transform, muzzle.position, aim, collisionLayers, 45f,
                    weapon.AttackRange, weapon.AttackPower, point => NotifyImpact(point, weapon), out Projectile launched))
                return false;

            // 막힌 발사구나 유효하지 않은 조준에는 재화나 공격 간격을 적용하지 않습니다.
            nextAttackTime = Time.time + weapon.AttackInterval;
            if (bow)
                inventory.Remove(ArrowItemId, 1);
            else if (staff)
                mana.TrySpend(weapon.ManaCost);

            Fired?.Invoke(launched, weapon);
            return true;
        }

        public bool TryGetAim(Vector2 pointer, out Vector3 aim)
        {
            aim = default;
            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            if (camera == null || !camera.pixelRect.Contains(pointer))
                return false;

            Ray aimRay = camera.ScreenPointToRay(pointer);
            if (Projectile.TryGetFirstHit(aimRay, 1000f, collisionLayers, transform, out RaycastHit hit))
                aim = hit.point;
            else if (new Plane(Vector3.up, AttackOrigin).Raycast(aimRay, out float distance))
                aim = aimRay.GetPoint(distance);
            else
                return false;
            return true;
        }

        private void NotifyImpact(Vector3 point, ItemDefinition weapon)
        {
            if (this == null)
                return;

            Impacted?.Invoke(point);
            WeaponImpacted?.Invoke(point, weapon);
        }

        private void Melee(Vector3 origin, Vector3 direction, ItemDefinition weapon)
        {
            var targets = new HashSet<Health>();
            foreach (Collider collider in Physics.OverlapSphere(origin, weapon.AttackRange, collisionLayers,
                         QueryTriggerInteraction.Ignore))
            {
                Health target = collider.GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || targets.Contains(target))
                    continue;

                Vector3 point = collider.ClosestPoint(origin);
                Vector3 path = point - origin;
                Vector3 planar = Vector3.ProjectOnPlane(path, Vector3.up);
                // 첫 구현은 조준 방향의 전방 120도 부채꼴입니다. 사거리는 CSV에서 읽습니다.
                if (planar.sqrMagnitude > 0.001f && Vector3.Dot(direction, planar.normalized) < Mathf.Cos(MeleeHalfAngle * Mathf.Deg2Rad))
                    continue;

                if (HasWallBetween(origin, path))
                    continue;

                targets.Add(target);
            }

            // 피해 이벤트가 다른 대상을 삭제하더라도 한 번의 공격에서 중복 판정하지 않습니다.
            foreach (Health target in targets)
                if (target != null && !target.IsDead)
                {
                    Collider collider = target.GetComponentInChildren<Collider>();
                    Vector3 point = collider != null ? collider.bounds.center + Vector3.up * (collider.bounds.extents.y + 0.08f)
                        : target.transform.position + Vector3.up;
                    if (target.TryApplyDamage(weapon.AttackPower)) NotifyImpact(point, weapon);
                }
        }

        public float MeleeReach(Vector3 direction, float range)
        {
            float reach = range;
            foreach (RaycastHit hit in Physics.RaycastAll(AttackOrigin, direction, range, collisionLayers, QueryTriggerInteraction.Ignore))
                if (BlocksMelee(hit))
                    reach = Mathf.Min(reach, hit.distance);
            return reach;
        }

        private bool HasWallBetween(Vector3 origin, Vector3 path)
        {
            if (path.sqrMagnitude < 0.000001f)
                return false;

            foreach (RaycastHit hit in Physics.RaycastAll(origin, path.normalized, path.magnitude,
                         collisionLayers, QueryTriggerInteraction.Ignore))
                if (BlocksMelee(hit))
                    return true;
            return false;
        }

        // 플레이어가 오를 수 있는 경사면은 벽이 아니라 지면이므로 근접 범위를 자르지 않습니다.
        private bool BlocksMelee(RaycastHit hit) =>
            hit.normal.y < walkableNormalY && !hit.transform.IsChildOf(transform) &&
            hit.collider.GetComponentInParent<Health>() == null;

        private bool IsOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null)
                return false;

            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, uiHits);
            foreach (RaycastResult hit in uiHits)
                if (hit.module is not PhysicsRaycaster)
                    return true;
            return false;
        }
    }
}
