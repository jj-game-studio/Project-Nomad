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
    [DefaultExecutionOrder(300)]
    public sealed partial class PlayerEquipmentCombat : MonoBehaviour
    {
        public const string ArrowItemId = "weapon-arrow";
        public const float MeleeHalfAngle = 60f;
        // 커서 레이 주변에서 적을 찾는 에임 보정 반경(m)입니다.
        public const float AimAssistRadius = 0.6f;
        public bool CanParry => isActiveAndEnabled && equipment.isActiveAndEnabled && !InputBlocked && !EquipmentUiOpen &&
            inventory?.BlocksTradeInput != true && Time.timeScale > 0 && !health.IsDead && !movement.BlocksAttack;

        public void InterruptForParry()
        {
            CancelMelee();
            CancelCharge();
            holdingAttack = false;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + PlayerBossParry.Window);
        }

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
        private PlayerStats stats;
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
            get => inputBlocked || GetComponent<Nomad.Monsters.Demo.PlayerBossReaction>()?.Locked == true;
            set { inputBlocked = value; if (value) { CancelMelee(); CancelCharge(); } }
        }
        public bool EquipmentUiOpen
        {
            get => equipmentUiOpen || inventory?.Panel?.ConsumedCloseClick == true;
            internal set { equipmentUiOpen = value; if (value) { CancelMelee(); CancelCharge(); } }
        }
        public bool IsMeleePending => pendingWeapon != null;
        public Vector3 AttackOrigin => transform.position + Vector3.up;
        internal Transform WeaponMuzzle { get; set; }
        public Vector3 ProjectileOrigin => WeaponMuzzle != null ? WeaponMuzzle.position : muzzle != null ? muzzle.position : AttackOrigin;
        public LayerMask CollisionLayers => collisionLayers;
        public ItemDefinition CurrentWeapon => equipment.ActiveWeapon != null ? equipment.ActiveWeapon : unarmedDefinition;
        public int ArrowCount => inventory != null ? inventory.GetCount(ArrowItemId) : 0;

        private void Awake()
        {
            equipment = GetComponent<PlayerEquipment>();
            gameObject.AddComponent<Nomad.Monsters.Demo.CartoonSurfaceStyle>();
            movement = GetComponent<PlayerMovement>();
            TryGetComponent(out inventory);
            health = GetComponent<Health>();
            mana = GetComponent<PlayerMana>();
            TryGetComponent(out stats);
            legacyCombat = GetComponent<PlayerCombat>();
            if (TryGetComponent(out CharacterController controller))
                walkableNormalY = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
            if (GetComponent<PlayerWeaponSkill>() == null) gameObject.AddComponent<PlayerWeaponSkill>();
            if (GetComponent<Nomad.CombatFeedback.PlayerDamageFeedback>() == null) gameObject.AddComponent<Nomad.CombatFeedback.PlayerDamageFeedback>();
            if (GetComponent<PlayerBossParry>() == null) gameObject.AddComponent<PlayerBossParry>();
            if (GetComponent<Nomad.WorldHud.CharacterHealthHud>() == null) gameObject.AddComponent<Nomad.WorldHud.CharacterHealthHud>();
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
            CancelCharge();
            if (restoreLegacy && legacyCombat != null)
                legacyCombat.enabled = true;
        }

        private void Update()
        {
            // Animator IK 전에 조준·몸 방향을 갱신하고, LateUpdate에서는 완성된 포즈에서 발사합니다.
            if (IsCharging && Mouse.current != null && TryGetAim(Mouse.current.position.ReadValue(), out Vector3 aim))
                SetChargeAim(aim);
        }

        private void LateUpdate()
        {
            ResolveMelee();
            Mouse mouse = Mouse.current;
            if (UpdateCharge(mouse)) { holdingAttack = false; return; }
            if (mouse == null || !mouse.leftButton.isPressed)
            {
                holdingAttack = false;
                return;
            }

            // UI 조작으로 시작한 누르기는 손을 뗄 때까지 공격으로 이어지지 않습니다.
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                holdingAttack = !InputBlocked && !EquipmentUiOpen && inventory?.BlocksTradeInput != true && !IsOverUi(pointer);

            // 차징을 사용하지 않는 무기는 누르고 있는 동안 공격 간격마다 계속 공격합니다.
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

            if (mode == WeaponAttackMode.Melee) AimPoint = aim;
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
            if (IsCharging && CurrentWeapon != chargeWeapon) CancelCharge();
            if (IsMeleePending && (CurrentWeapon != pendingWeapon || equipment.ActiveWeaponSlot != pendingSlot))
                CancelMelee();
        }

        private void OnDied(Health _) { CancelMelee(); CancelCharge(); }

        private bool TryFire(ItemDefinition weapon, Vector3 aim, float power = 1f)
        {
            WeaponAttackMode mode = weapon.AttackMode;
            bool bow = mode == WeaponAttackMode.Bow;
            bool staff = mode == WeaponAttackMode.Staff;
            if (muzzle == null || (!bow && !staff && mode != WeaponAttackMode.Ranged) ||
                (bow && ArrowCount == 0) || (staff && (mana == null || !mana.CanSpend(weapon.ManaCost))))
                return false;

            Projectile shot = bow ? arrowProjectile : projectile;
            FaceRangedAim(aim);
            Vector3 origin = ProjectileOrigin;
            // 커서가 몸통·무기 안쪽이면 뒤집어 쏘지 않고 현재 보는 방향을 유지합니다.
            if (Vector3.Dot(Vector3.ProjectOnPlane(aim - origin, Vector3.up), transform.forward) < 0.25f)
                aim = origin + transform.forward * weapon.AttackRange;
            int damage = Damage(weapon, out bool critical);
            if (!Projectile.TryLaunch(shot, transform, origin, aim, collisionLayers, 45f,
                    weapon.AttackRange, Mathf.Max(1, Mathf.RoundToInt(damage * power)), point => NotifyRangedImpact(point, weapon, power), out Projectile launched))
                return false;
            launched.Critical = critical;

            // 막힌 발사구나 유효하지 않은 조준에는 재화나 공격 간격을 적용하지 않습니다.
            nextAttackTime = Time.time + weapon.AttackInterval;
            LastShotPower = power;
            launched.transform.localScale *= Mathf.Lerp(1f, 1.5f, (power - 1f) / 1.5f);
            if (bow)
                inventory.Remove(ArrowItemId, 1);
            else if (staff)
                mana.TrySpend(weapon.ManaCost);

            AimPoint = aim;
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
            bool hasHit = Projectile.TryGetFirstHit(aimRay, 1000f, collisionLayers, transform, out RaycastHit hit);

            // 커서 아래나 근처의 적은 지형보다 우선해 몸 중심을 조준합니다.
            if (TryGetSnapTarget(aimRay, hasHit ? hit.collider : null, out aim))
                return true;

            if (hasHit)
                aim = hit.point;
            else if (new Plane(Vector3.up, AttackOrigin).Raycast(aimRay, out float distance))
                aim = aimRay.GetPoint(distance);
            else
                return false;

            // 적이 없으면 발사구 높이로 평탄화해 바닥에 꽂히지 않고 수평으로 날아가게 합니다.
            aim.y = ProjectileOrigin.y;
            return true;
        }

        private bool TryGetSnapTarget(Ray aimRay, Collider direct, out Vector3 center)
        {
            center = default;
            Collider best = IsSnapTarget(direct) ? direct : null;
            float bestDistance = float.PositiveInfinity;

            // 직접 맞은 적이 없으면 보정 반경 안에서 레이를 따라 가장 가까운 적을 고릅니다.
            if (best == null)
                foreach (RaycastHit hit in Physics.SphereCastAll(aimRay, AimAssistRadius, 1000f, collisionLayers,
                             QueryTriggerInteraction.Ignore))
                {
                    if (hit.distance >= bestDistance || !IsSnapTarget(hit.collider))
                        continue;

                    best = hit.collider;
                    bestDistance = hit.distance;
                }

            if (best == null)
                return false;

            // 발사구에서 적 중심까지 지형에 막히면 스냅하지 않습니다.
            center = best.bounds.center;
            Vector3 path = center - ProjectileOrigin;
            Health target = best.GetComponentInParent<Health>();
            return !Projectile.TryGetFirstHit(new Ray(ProjectileOrigin, path.normalized), path.magnitude,
                       collisionLayers, transform, out RaycastHit blocker) ||
                   blocker.collider.GetComponentInParent<Health>() == target;
        }

        private bool IsSnapTarget(Collider collider)
        {
            if (collider == null || collider.transform.IsChildOf(transform))
                return false;

            Health target = collider.GetComponentInParent<Health>();
            return target != null && !target.IsDead;
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
                    int damage = Damage(weapon, out bool critical);
                    if (target.TryApplyCombatDamage(damage, critical)) NotifyImpact(point, weapon);
                }
        }

        // 스탯이 있으면 장비 공격력과 치명타를 반영합니다. 근접은 대상마다 따로 판정합니다.
        private int Damage(ItemDefinition weapon, out bool critical)
        {
            critical = false;
            if (stats == null || !stats.isActiveAndEnabled)
                return weapon.AttackPower;

            return stats.RollOutgoingDamage(weapon, out critical);
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
