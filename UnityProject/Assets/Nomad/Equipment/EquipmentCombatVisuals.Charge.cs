using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class EquipmentCombatVisuals
    {
        private ChargeGatherVfx chargeGather;
        private Transform staffMuzzle;

        private void LateUpdate()
        {
            // IK와 상체 방향 보정 후 실제 그려질 지팡이 위치를 보관합니다.
            // 다음 프레임 애니메이터가 자세를 갱신하는 도중의 임시 위치에서 발사하지 않습니다.
            if (staffMuzzle != null && combat.CurrentWeapon?.AttackMode == WeaponAttackMode.Staff)
                combat.PosedStaffOffset = staffMuzzle.position - transform.position;
        }

        private static WeaponVfxStyle RangedStyle(ItemDefinition weapon, float power)
        {
            if (!WeaponVfx.TryGetStyle(weapon, out WeaponVfxStyle style))
                style = new WeaponVfxStyle(new Color(1f, 0.96f, 0.7f), new Color(1f, 0.6f, 0.12f), 1f);
            float charge = Mathf.Clamp01((power - PlayerEquipmentCombat.QuickShotPower) / (2.5f - PlayerEquipmentCombat.QuickShotPower));
            return new WeaponVfxStyle(Color.Lerp(style.Core, Color.white, charge * 0.6f), style.Glow,
                style.Size * Mathf.Lerp(0.55f, 1.75f, charge));
        }

        private void ConfigureStaffMuzzle(ItemDefinition weapon)
        {
            if (weapon.AttackMode != WeaponAttackMode.Staff) return;
            MeshFilter mesh = weaponModel.GetComponentInChildren<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return;

            // 장착한 지팡이의 실제 끝을 따릅니다. 손 모션·무기 회전에 맞춰 함께 움직입니다.
            Bounds bounds = mesh.sharedMesh.bounds;
            staffMuzzle = new GameObject("Staff Tip Muzzle").transform;
            staffMuzzle.SetParent(mesh.transform, false);
            staffMuzzle.localPosition = new Vector3(bounds.center.x, bounds.max.y - 0.03f, bounds.center.z);
        }

        private void RangedImpact(Vector3 point, ItemDefinition weapon, float power)
        {
            WeaponVfx.Burst(point, RangedStyle(weapon, power), power > 1.1f ? 0.9f : 0.35f);
        }
    }
}
