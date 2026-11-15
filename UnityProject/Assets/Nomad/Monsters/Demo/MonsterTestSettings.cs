using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [System.Serializable]
    public sealed class MonsterTestSettings
    {
        public float moveSpeed = 3.5f;
        public float sightRange = 8f;
        public float sightAngle = 120f;
        public float proximityRange = 2f;
        public float abandonRange = 12f;
        public float homeRange = 15f;
        public float attackSpeed = 1f;
        public float attackRange = 1.8f;
        public float heavyRange = 2.6f;
        public int health = 100;
        public int damage = 20;
        public int heavyDamage = 40;

        public static MonsterTestSettings Defaults(bool elite)
        {
            return new MonsterTestSettings
            {
                health = elite ? 250 : 100,
                sightRange = elite ? 10f : 8f,
                sightAngle = elite ? 140f : 120f,
                proximityRange = elite ? 3f : 2f,
                abandonRange = elite ? 16f : 12f,
                homeRange = elite ? 20f : 15f
            };
        }
    }
}
