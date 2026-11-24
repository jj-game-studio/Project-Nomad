using System;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipmentCombat
    {
        public const float FullChargeSeconds = 1f;
        public const float QuickShotPower = 0.65f;
        public const float ChargingMoveSpeed = 0.45f;
        private ItemDefinition chargeWeapon;
        private float chargeStarted;
        public bool IsCharging => chargeWeapon != null;
        public float ChargeProgress => IsCharging ? Mathf.Clamp01((Time.time - chargeStarted) / FullChargeSeconds) : 0;
        public float LastShotPower { get; private set; } = 1f;
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
                (weapon.AttackMode == WeaponAttackMode.Staff && (mana == null || !mana.CanSpend(weapon.ManaCost))) ||
                IsOverUi(pointer) || !TryGetAim(pointer, out Vector3 aim)) return false;

            chargeWeapon = weapon;
            chargeStarted = Time.time;
            movement.ChargeSpeedMultiplier = ChargingMoveSpeed;
            AimPoint = aim;
            Vector3 direction = Vector3.ProjectOnPlane(aim - AttackOrigin, Vector3.up);
            ChargeStarted?.Invoke(weapon, direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward);
            return true;
        }

        public bool TryReleaseCharge(Vector2 pointer)
        {
            if (!IsCharging) return false;
            ItemDefinition weapon = chargeWeapon;
            float power = 1f + ChargeProgress * 1.5f;
            bool allowed = ChargeAllowed && CurrentWeapon == weapon && !IsOverUi(pointer) && TryGetAim(pointer, out _);
            CancelCharge();
            if (!allowed || !TryGetAim(pointer, out Vector3 aim) || !TryFire(weapon, aim, power)) return false;
            AimPoint = aim;
            Attacked?.Invoke(weapon.AttackMode);
            return true;
        }

        public void CancelCharge()
        {
            if (!IsCharging) return;
            chargeWeapon = null;
            movement.ChargeSpeedMultiplier = 1f;
            ChargeCancelled?.Invoke();
        }

        public bool TryQuickShot(Vector2 pointer)
        {
            ItemDefinition weapon = CurrentWeapon;
            if (IsCharging || !ChargeAllowed || weapon == null ||
                (weapon.AttackMode != WeaponAttackMode.Bow && weapon.AttackMode != WeaponAttackMode.Staff) ||
                IsOverUi(pointer) || !TryGetAim(pointer, out Vector3 aim) || !TryFire(weapon, aim, QuickShotPower)) return false;
            AimPoint = aim;
            Attacked?.Invoke(weapon.AttackMode);
            return true;
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

            if (mouse.rightButton.wasPressedThisFrame && !mouse.leftButton.isPressed)
                TryQuickShot(mouse.position.ReadValue());

            // 활·지팡이는 누를 때 발사하지 않고, 놓을 때 유지 시간에 맞춰 한 발만 발사합니다.
            if (mouse.leftButton.wasPressedThisFrame) TryBeginCharge(mouse.position.ReadValue());
            if (!IsCharging) return true;
            if (mouse.leftButton.wasReleasedThisFrame) TryReleaseCharge(mouse.position.ReadValue());
            else if (!mouse.leftButton.isPressed) CancelCharge();
            else if (TryGetAim(mouse.position.ReadValue(), out Vector3 aim)) AimPoint = aim;
            return true;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelCharge(); }
    }
}
