using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipmentCombat
    {
        public const float SkillCooldown = 8;
        private float nextSkill;
        public float SkillRemaining => Mathf.Max(0, nextSkill - Time.time);

        public bool TryUseSkill(Vector2 pointer)
        {
            ItemDefinition weapon = CurrentWeapon;
            if (!ChargeAllowed || inventory?.Panel?.IsOpen == true || IsCharging || IsMeleePending || weapon == null || weapon.EnhancementLevel < 1 ||
                weapon.EquipmentType != EquipmentType.Weapon || SkillRemaining > 0 || IsOverUi(pointer) ||
                !TryGetAim(pointer, out Vector3 aim)) return false;
            bool bow = weapon.AttackMode == WeaponAttackMode.Bow;
            bool staff = weapon.AttackMode == WeaponAttackMode.Staff;
            if ((bow && ArrowCount < 1) || (staff && (mana == null || !mana.CanSpend(weapon.ManaCost * 2)))) return false;

            FaceRangedAim(aim);
            int power = Mathf.RoundToInt(Damage(weapon, out bool critical) * 2.5f);
            if (weapon.AttackMode == WeaponAttackMode.Melee)
            {
                SkillProjectile.DamageArea(transform.position, transform.forward, 6, 100, power, transform, critical);
                WeaponVfx.Burst(transform.position + transform.forward * 3 + Vector3.up * 0.3f,
                    new WeaponVfxStyle(new Color(1, 0.9f, 0.6f), new Color(1, 0.55f, 0.1f), 2));
                AimPoint = transform.position + transform.forward * 6;
                MeleeStarted?.Invoke(weapon, transform.forward);
            }
            else
            {
                Vector3 origin = ProjectileOrigin;
                Vector3 direction = transform.forward;
                // 빛나는 강화 탄은 발사구의 현재 위치와 몸의 정면을 사용합니다.
                foreach (Collider obstruction in Physics.OverlapSphere(origin, 0.08f, collisionLayers, QueryTriggerInteraction.Ignore))
                    if (!obstruction.transform.IsChildOf(transform)) return false;
                Vector3 body = new Vector3(transform.position.x, origin.y, transform.position.z);
                Vector3 barrel = origin - body;
                if (Projectile.TryGetFirstHit(new Ray(body, barrel.normalized), barrel.magnitude, collisionLayers, transform, out _)) return false;
                SkillProjectile.Launch(transform, origin, direction, power, staff, critical);
                AimPoint = origin + direction * 24;
                Attacked?.Invoke(weapon.AttackMode);
            }
            if (bow) inventory.Remove(ArrowItemId, 1);
            if (staff) mana.TrySpend(weapon.ManaCost * 2);
            nextSkill = Time.time + SkillCooldown;
            nextAttackTime = Time.time + Mathf.Max(0.6f, weapon.AttackInterval);
            return true;
        }
    }
}
