using UnityEngine;

namespace Nomad.Monsters
{
    public sealed class MonsterAppearanceSet : ScriptableObject
    {
        public GameObject[] normal = System.Array.Empty<GameObject>();
        public GameObject elite;
        public GameObject boss;
        public GameObject arrow;
        public GameObject magicBolt;
        public GameObject rockBolt;
        public GameObject fireBolt;
    }
}
