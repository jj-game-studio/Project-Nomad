using UnityEngine;

namespace Nomad.Monsters
{
    public sealed class MonsterSkillPulse : MonoBehaviour
    {
        private LineRenderer ring;
        private float radius;
        private float elapsed;

        public void Configure(Material material, float range)
        {
            radius = range;
            ring = gameObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.widthMultiplier = 0.12f;
            ring.positionCount = 65;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i <= 64; i++)
                ring.SetPosition(i, Vector3.up * 0.08f + Quaternion.AngleAxis(i * 360f / 64f, Vector3.up) *
                    Vector3.forward * radius * Mathf.Clamp01(elapsed / 0.25f));
            ring.widthMultiplier = Mathf.Lerp(0.15f, 0f, elapsed / 0.4f);
            if (elapsed >= 0.4f) Destroy(gameObject);
        }
    }
}
