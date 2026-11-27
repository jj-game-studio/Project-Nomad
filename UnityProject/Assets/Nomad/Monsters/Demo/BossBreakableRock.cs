using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossBreakableRock : MonoBehaviour
    {
        public bool Broken { get; private set; }
        public bool Shatter()
        {
            if (Broken) return false;
            Broken = true;
            BossStoneDebris.Create(transform.parent, transform.position + Vector3.up * 0.7f);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
