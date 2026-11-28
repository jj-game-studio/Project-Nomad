using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossArena
    {
        private void Dress(Quaternion rotation, Material stone, Material trim)
        {
            for (int index = 0; index < 72; index++)
            {
                if (!standalone && Mathf.Abs(index - 36) < 3) continue;
                Quaternion turn = rotation * Quaternion.Euler(0, index * 5, 0);
                Vector3 point = GroundPoint(Center + turn * Vector3.forward * (HalfSize - 0.25f));
                Floor("Ruined circular parapet", point + Vector3.up * 1.2f, new Vector3(1.95f, 1.2f, 0.8f), turn, stone);
            }
            gameObject.AddComponent<BossRuinsDressing>().Build(this);
        }
    }
}
