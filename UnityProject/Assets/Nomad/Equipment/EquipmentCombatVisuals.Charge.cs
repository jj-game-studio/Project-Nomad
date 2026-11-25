using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class EquipmentCombatVisuals
    {
        private ChargeGatherVfx chargeGather;
        private Transform weaponMuzzle;

        private static WeaponVfxStyle RangedStyle(ItemDefinition weapon, float power)
        {
            if (!WeaponVfx.TryGetStyle(weapon, out WeaponVfxStyle style))
                style = new WeaponVfxStyle(new Color(1f, 0.96f, 0.7f), new Color(1f, 0.6f, 0.12f), 1f);
            float charge = Mathf.InverseLerp(1f, 2.5f, power);
            return new WeaponVfxStyle(Color.Lerp(style.Core, Color.white, charge * 0.6f), style.Glow,
                style.Size * Mathf.Lerp(0.55f, 1.75f, charge));
        }

        private void ConfigureWeaponMuzzle(ItemDefinition weapon)
        {
            if (weapon.AttackMode != WeaponAttackMode.Staff && weapon.AttackMode != WeaponAttackMode.Bow) return;
            MeshFilter mesh = weaponModel.GetComponentInChildren<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return;

            // 지팡이 끝·활 중앙을 따릅니다. 손 모션·무기 회전에 맞춰 함께 움직입니다.
            Bounds bounds = mesh.sharedMesh.bounds;
            weaponMuzzle = new GameObject(weapon.AttackMode == WeaponAttackMode.Staff ? "Staff Tip Muzzle" : "Bow Muzzle").transform;
            weaponMuzzle.SetParent(mesh.transform, false);
            weaponMuzzle.localPosition = weapon.AttackMode == WeaponAttackMode.Staff
                ? new Vector3(bounds.center.x, bounds.max.y - 0.03f, bounds.center.z) : bounds.center;
        }

        private void RangedImpact(Vector3 point, ItemDefinition weapon, float power)
        {
            WeaponVfx.Burst(point, RangedStyle(weapon, power), power > 1.1f ? 0.9f : 0.35f);
        }
    }
}
