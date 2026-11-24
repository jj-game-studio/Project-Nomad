using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class EquipmentCombatVisuals
    {
        private LineRenderer chargeRing;

        private static WeaponVfxStyle RangedStyle(ItemDefinition weapon, float power)
        {
            if (!WeaponVfx.TryGetStyle(weapon, out WeaponVfxStyle style))
                style = new WeaponVfxStyle(new Color(1f, 0.96f, 0.7f), new Color(1f, 0.6f, 0.12f), 1f);
            float charge = Mathf.Clamp01((power - PlayerEquipmentCombat.QuickShotPower) / (2.5f - PlayerEquipmentCombat.QuickShotPower));
            return new WeaponVfxStyle(Color.Lerp(style.Core, Color.white, charge * 0.6f), style.Glow,
                style.Size * Mathf.Lerp(0.55f, 1.75f, charge));
        }

        private void UpdateChargeVisual(ItemDefinition weapon)
        {
            if (chargeRing == null)
            {
                chargeRing = Line("Charge Focus Ring", Color.white, 0.04f);
                chargeRing.sharedMaterial = WeaponVfx.Beam;
                chargeRing.positionCount = 33;
            }
            chargeRing.enabled = combat.IsCharging;
            if (!combat.IsCharging) return;

            float progress = combat.ChargeProgress;
            WeaponVfxStyle style = RangedStyle(weapon, 1f + progress * 1.5f);
            chargeRing.startColor = chargeRing.endColor = style.Glow;
            chargeRing.widthMultiplier = 0.03f + progress * 0.07f;
            Quaternion facing = Quaternion.LookRotation(direction);
            float radius = 0.18f + progress * 0.28f + Mathf.Sin(Time.time * 18f) * progress * 0.03f;

            for (int index = 0; index <= 32; index++)
            {
                float angle = index / 32f * Mathf.PI * 2 * Mathf.Max(0.05f, progress) + Time.time * 3f;
                chargeRing.SetPosition(index, combat.ProjectileOrigin + facing * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
        }

        private void RangedImpact(Vector3 point, ItemDefinition weapon, float power)
        {
            WeaponVfx.Burst(point, RangedStyle(weapon, power), power > 1.1f ? 0.9f : 0.35f);
        }
    }
}
