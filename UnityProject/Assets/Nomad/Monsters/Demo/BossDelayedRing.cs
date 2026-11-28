using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossDelayedRing : MonoBehaviour
    {
        private BossArena arena;
        private Vector3 center;
        private float inner, outer, at;
        public void Configure(BossArena room, Vector3 point, float min, float max, float delay)
        { arena = room; center = point; inner = min; outer = max; at = Time.time + delay; }

        private void Update()
        {
            if (Time.time < at) return;
            BossRingBurst.Create(transform.parent, arena, center, inner, outer, 0.45f);
            enabled = false;
        }
    }
}
