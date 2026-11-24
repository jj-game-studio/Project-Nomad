using System;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipmentCombat
    {
        public const float FullChargeSeconds = 1f;
        private ItemDefinition chargeWeapon;
        private float chargeStarted;
        public bool IsCharging => chargeWeapon != null;
        public float ChargeProgress => IsCharging ? Mathf.Clamp01((Time.time - chargeStarted) / FullChargeSeconds) : 0;
        public float LastShotPower { get; private set; } = 1f;
        public event Action<ItemDefinition, Vector3> ChargeStarted;
        public event Action ChargeCancelled;

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
            ChargeCancelled?.Invoke();
        }

        private bool UpdateCharge(Mouse mouse)
        {
            if (IsCharging && (!ChargeAllowed || CurrentWeapon != chargeWeapon || mouse == null)) CancelCharge();
            if (mouse == null) return false;
            if (mouse.rightButton.wasPressedThisFrame) TryBeginCharge(mouse.position.ReadValue());
            if (!IsCharging) return false;
            if (mouse.rightButton.wasReleasedThisFrame) TryReleaseCharge(mouse.position.ReadValue());
            else if (!mouse.rightButton.isPressed) CancelCharge();
            else if (TryGetAim(mouse.position.ReadValue(), out Vector3 aim)) AimPoint = aim;
            return true;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelCharge(); }
    }
}
