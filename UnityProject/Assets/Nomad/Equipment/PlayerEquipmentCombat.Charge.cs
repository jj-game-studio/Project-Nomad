using System;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipmentCombat
    {
        public const float FullChargeSeconds = 1f;
        public const float ChargingMoveSpeed = 0.45f;
        private ItemDefinition chargeWeapon;
        private float chargeStarted;
        public bool IsCharging => chargeWeapon != null;
        public float ChargeProgress => IsCharging ? Mathf.Clamp01((Time.time - chargeStarted) / FullChargeSeconds) : 0;
        public float LastShotPower { get; private set; } = 1f;
        public Quaternion AimFacingRotation { get; private set; } = Quaternion.identity;
        public event Action<ItemDefinition, Vector3> ChargeStarted;
        public event Action ChargeCancelled;
        public event Action<Vector3, ItemDefinition, float> RangedImpacted;

        private bool ChargeAllowed => isActiveAndEnabled && equipment.isActiveAndEnabled && !InputBlocked && !EquipmentUiOpen &&
            inventory?.BlocksTradeInput != true && Time.timeScale > 0 && !health.IsDead && !movement.BlocksAttack &&
            Time.time >= nextAttackTime;

        public bool TryBeginCharge(Vector2 pointer)
        {
            ItemDefinition weapon = CurrentWeapon;
            if (IsCharging || !ChargeAllowed || weapon == null ||
                (weapon.AttackMode != WeaponAttackMode.Bow && weapon.AttackMode != WeaponAttackMode.Staff) ||
                (weapon.AttackMode == WeaponAttackMode.Bow && ArrowCount == 0) ||
                (weapon.AttackMode == WeaponAttackMode.Staff && mana == null) ||
                IsOverUi(pointer) || !TryGetAim(pointer, out Vector3 aim)) return false;

            chargeWeapon = weapon;
            chargeStarted = Time.time;
            movement.ChargeSpeedMultiplier = ChargingMoveSpeed;
            SetChargeAim(aim);
            ChargeStarted?.Invoke(weapon, transform.forward);
            return true;
        }

        public bool TryReleaseCharge(Vector2 pointer)
        {
            if (!IsCharging) return false;
            ItemDefinition weapon = chargeWeapon;
            float power = 1f + ChargeProgress * 1.5f;
            float charge = ChargeProgress;
            bool allowed = ChargeAllowed && CurrentWeapon == weapon && !IsOverUi(pointer) && AimPoint.HasValue;
            if (!allowed) { CancelCharge(); return false; }
            Vector3 aim = AimPoint.Value;

            // 발사 전에 자세를 취소하면 지팡이 손이 원위치로 돌아가 다른 쪽에서 탄이 나가 보입니다.
            bool fired = weapon.AttackMode == WeaponAttackMode.Staff ? TryFireChargedStaff(weapon, aim, charge) : TryFire(weapon, aim, power);
            CancelCharge();
            if (!fired) return false;
            Attacked?.Invoke(weapon.AttackMode);
            return true;
        }

        public void CancelCharge()
        {
            if (!IsCharging) return;
            chargeWeapon = null;
            movement.ChargeSpeedMultiplier = 1f;
            movement.AimFacing = null;
            ChargeCancelled?.Invoke();
        }

        private void SetChargeAim(Vector3 aim)
        {
            AimPoint = aim;
            FaceRangedAim(aim);
        }

        private void FaceRangedAim(Vector3 aim)
        {
            Vector3 facing = Vector3.ProjectOnPlane(aim - AttackOrigin, Vector3.up);
            if (facing.magnitude >= 0.5f)
            {
                // 몸 중앙과 손의 좌우 차이를 보정해 몸의 정면과 실제 탄의 수평 방향을 맞춥니다.
                float side = transform.InverseTransformPoint(ProjectileOrigin).x;
                float correction = Mathf.Asin(Mathf.Clamp(side / facing.magnitude, -0.95f, 0.95f)) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0, -correction, 0);
            }
            AimFacingRotation = transform.rotation;
            if (IsCharging) movement.AimFacing = transform.forward;
        }

        private void NotifyRangedImpact(Vector3 point, ItemDefinition weapon, float power)
        {
            if (this == null) return;
            NotifyImpact(point, weapon);
            // 발사 당시 위력을 보관하므로 이후 발사나 무기 교체가 명중 연출을 바꾸지 않습니다.
            RangedImpacted?.Invoke(point, weapon, power);
        }

        private bool UpdateCharge(Mouse mouse)
        {
            if (IsCharging && (!ChargeAllowed || CurrentWeapon != chargeWeapon || mouse == null)) CancelCharge();
            if (mouse == null) return false;
            ItemDefinition weapon = CurrentWeapon;
            bool chargeInput = weapon != null &&
                (weapon.AttackMode == WeaponAttackMode.Bow || weapon.AttackMode == WeaponAttackMode.Staff);
            if (!chargeInput) return false;

            // 활·지팡이는 누를 때 발사하지 않고, 놓을 때 유지 시간에 맞춰 한 발만 발사합니다.
            if (mouse.leftButton.wasPressedThisFrame) TryBeginCharge(mouse.position.ReadValue());
            if (!IsCharging) return true;
            if (mouse.leftButton.wasReleasedThisFrame) TryReleaseCharge(mouse.position.ReadValue());
            else if (!mouse.leftButton.isPressed) CancelCharge();
            return true;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelCharge(); }
    }
}
