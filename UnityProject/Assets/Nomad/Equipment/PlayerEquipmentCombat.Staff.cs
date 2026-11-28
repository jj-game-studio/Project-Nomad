using Nomad.Combat;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipmentCombat
    {
        public const float StaffTapLimit = 0.15f;
        public static int StaffManaCost(ItemDefinition weapon, float charge) => charge <= StaffTapLimit ? 0 : Mathf.CeilToInt(weapon.ManaCost * 2 * Mathf.Clamp01(charge));

        // 스크립트/디버그의 즉시 공격도 실제 클릭과 같은 무료 지팡이 평타를 사용합니다.
        private bool TryFire(ItemDefinition weapon, Vector3 aim) => weapon.AttackMode == WeaponAttackMode.Staff
            ? TryFireChargedStaff(weapon, aim, 0) : TryFire(weapon, aim, 1);

        private bool TryFireChargedStaff(ItemDefinition weapon, Vector3 aim, float charge)
        {
            if (muzzle == null || mana == null) return false;
            // 마나가 부족하면 보유량까지만 차징 위력을 사용하고, 0이면 무료 평타로 발사합니다.
            if (!mana.Unlimited) charge = Mathf.Min(charge, Mathf.Floor(mana.Current) / Mathf.Max(1, weapon.ManaCost * 2));
            int cost = StaffManaCost(weapon, charge);
            if (cost > 0 && !mana.CanSpend(cost)) return false;
            float power = cost == 0 ? 0.65f : Mathf.Lerp(0.9f, 3, charge);
            FaceRangedAim(aim);
            Vector3 origin = ProjectileOrigin;
            if (Vector3.Dot(Vector3.ProjectOnPlane(aim-origin,Vector3.up),transform.forward) < 0.25f)
                aim = origin + transform.forward * weapon.AttackRange;
            int damage = Damage(weapon,out bool critical);
            if (!Projectile.TryLaunch(projectile,transform,origin,aim,collisionLayers,45,weapon.AttackRange,
                Mathf.Max(1,Mathf.RoundToInt(damage*power)), point=>NotifyRangedImpact(point,weapon,power),out Projectile launched)) return false;

            launched.Critical = critical;
            if (cost > 0) mana.TrySpend(cost);
            nextAttackTime = Time.time + weapon.AttackInterval;
            LastShotPower = power;
            launched.transform.localScale *= Mathf.Lerp(0.8f,1.65f,charge);
            AimPoint = aim;
            Fired?.Invoke(launched,weapon);
            return true;
        }
    }
}
