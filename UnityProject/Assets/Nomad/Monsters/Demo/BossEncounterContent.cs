using UnityEngine;

namespace Nomad.Monsters.Demo
{
    // 실제 Field와 TerrainIntegration이 같은 보스 장식 프리팹을 명시적으로 참조합니다.
    public sealed class BossEncounterContent : MonoBehaviour
    {
        public GameObject Rock;
        [Min(1)] public int BossHealth = BossArmor.ReferenceHealth;
        public AnimationClip Knockdown;
    }
}
