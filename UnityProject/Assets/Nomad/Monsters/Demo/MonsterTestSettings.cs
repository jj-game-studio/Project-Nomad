using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [System.Serializable]
    public sealed class MonsterTestSettings
    {
        public float moveSpeed = 3.5f;
        public float sightRange = 8f;
        public float abandonRange = 12f;
        public float homeRange = 15f;
        public float attackSpeed = 1f;
        public float attackRange = 1.8f;
        public float heavyRange = 2.6f;
        public int health = 100;
        public int damage = 20;
        public int heavyDamage = 40;
        public bool ranged = true;
        public int normalStyle = -1;
        public float rangedRange = 12f;
        public float projectileSpeed = 9f;
        public int rangedDamage = 15;
        public float rangedWindup = 1.1f;
        public float rangedRecovery = 1.8f;
        public float basicWindup = 0.35f;
        public float basicRecovery = 0.35f;
        public bool skills = true;
        public float skillWindup = 0.8f;
        public float skillRecovery = 0.45f;
        public float chargeMin = 4.5f;
        public float chargeMax = 12f;
        public float chargeSpeed = 12f;
        public float chargeCooldown = 5f;
        public float areaRadius = 3.8f;
        public float areaCooldown = 5f;

        public static MonsterTestSettings Defaults(bool elite)
        {
            return new MonsterTestSettings
            {
                health = elite ? 220 : 100,
                damage = elite ? 18 : 20,
                heavyDamage = elite ? 35 : 40,
                moveSpeed = elite ? 4.5f : 3.8f,
                attackSpeed = elite ? 1.25f : 0.9f,
                attackRange = elite ? 2.4f : 1.8f,
                rangedDamage = elite ? 22 : 15,
                projectileSpeed = elite ? 7f : 9f,
                rangedWindup = elite ? 0.8f : 0.7f,
                rangedRecovery = 0.65f,
                sightRange = elite ? 10f : 8f,
                abandonRange = elite ? 16f : 12f,
                homeRange = elite ? 20f : 15f
            };
        }

        public static MonsterTestSettings Defaults(MonsterRank rank)
        {
            if (rank != MonsterRank.Boss) return Defaults(rank == MonsterRank.Elite);
            // 전용 보스 패턴을 확정하기 전 세 등급 배치를 확인하기 위한 임시 값입니다.
            return new MonsterTestSettings
            {
                health = 850, moveSpeed = 3.8f, sightRange = 10f,
                abandonRange = 20f, homeRange = 24f, attackRange = 4.2f,
                heavyRange = 5.5f, damage = 22, heavyDamage = 40, attackSpeed = 1.15f,
                rangedRange = 16f, rangedDamage = 26, rangedWindup = 0.85f, rangedRecovery = 0.8f,
                chargeMin = 7f, chargeMax = 16f, chargeSpeed = 16f, chargeCooldown = 6f,
                areaRadius = 5.5f, areaCooldown = 7f, skillWindup = 0.9f
            };
        }
    }
}
