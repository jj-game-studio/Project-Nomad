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
        public float rangedRange = 12f;
        public float projectileSpeed = 9f;
        public int rangedDamage = 15;
        public float rangedWindup = 1.1f;
        public float rangedRecovery = 1.8f;

        public static MonsterTestSettings Defaults(bool elite)
        {
            return new MonsterTestSettings
            {
                health = elite ? 250 : 100,
                rangedDamage = elite ? 25 : 15,
                projectileSpeed = elite ? 7f : 9f,
                rangedWindup = elite ? 1.4f : 1.1f,
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
                health = 1000, moveSpeed = 2.5f, sightRange = 10f,
                abandonRange = 18f, homeRange = 22f, attackRange = 2.6f,
                heavyRange = 3.5f, damage = 25, heavyDamage = 50, attackSpeed = 0.8f,
                rangedRange = 16f, rangedDamage = 30, rangedWindup = 1.3f, rangedRecovery = 2.2f
            };
        }
    }
}
