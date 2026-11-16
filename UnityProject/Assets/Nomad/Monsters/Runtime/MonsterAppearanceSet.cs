using UnityEngine;

namespace Nomad.Monsters
{
    public sealed class MonsterAppearanceSet : ScriptableObject
    {
        public GameObject[] normal = System.Array.Empty<GameObject>();
        public GameObject elite;
        public GameObject boss;
    }
}
