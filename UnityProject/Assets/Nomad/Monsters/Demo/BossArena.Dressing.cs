using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossArena
    {
        private void Dress(Quaternion rotation, Material stone, Material trim)
        {
            Material pale = Material(new Color(0.31f, 0.34f, 0.31f));
            Material dark = Material(new Color(0.28f, 0.31f, 0.29f));
            for (int x = -5; x <= 5; x++)
            for (int z = -5; z <= 5; z++)
            {
                if (uneven) continue;
                Decor("Weathered floor tile", Center + rotation * new Vector3(x * 4, 0.012f, z * 4),
                    new Vector3(3.92f, 0.025f, 3.92f), rotation, (x + z) % 2 == 0 ? pale : dark);
            }

            for (int side = 0; side < 4; side++)
            for (int section = -5; section <= 5; section++)
            {
                if (side == 0 && section == 0 && !standalone) continue;
                Quaternion turn = rotation * Quaternion.Euler(0, side * 90, 0);
                Vector3 position = Center + turn * new Vector3(section * 4, 0.7f, -HalfSize + 0.4f);
                position.y = GroundHeight(position) + 0.7f;
                // 입구를 제외한 낮은 난간은 실제 낙하도 막습니다.
                Floor("Ruined arena parapet", position, new Vector3(3.9f, 0.7f, 0.65f), turn, stone);
                Decor("Parapet cap", position + Vector3.up * 0.06f, new Vector3(4, 0.12f, 0.8f), turn, pale);
            }

            Material flame = Material(new Color(0.9f, 0.58f, 0.24f));
            flame.EnableKeyword("_EMISSION");
            flame.SetColor("_EmissionColor", new Color(0.55f, 0.22f, 0.05f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = Center + rotation * new Vector3(side * 3, 0, -HalfSize + 1.5f);
                position = GroundPoint(position);
                Decor("Brazier base", position + Vector3.up * 0.5f, new Vector3(0.55f, 1, 0.55f), rotation, dark);
                GameObject ember = Decor("Brazier ember", position + Vector3.up * 1.05f,
                    new Vector3(0.35f, 0.24f, 0.35f), rotation, flame);
                Light glow = ember.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1, 0.65f, 0.3f);
                glow.range = 4;
                glow.intensity = 0.65f;
                glow.shadows = LightShadows.None;
            }
        }
    }
}
