using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossRockEruption : MonoBehaviour
    {
        private Vector3 resting;
        private float started;
        private Behaviour obstacle;
        private Collider collision;

        public void Configure()
        {
            resting = transform.position;
            started = Time.time;
            Transform solid = transform.Find("Rock collision");
            collision = solid.GetComponent<Collider>();
            obstacle = solid.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            collision.enabled = false;
            obstacle.enabled = false;
            transform.position = resting - Vector3.up * 2;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - started) / 0.65f);
            transform.position = resting + Vector3.up * (-2 * (1 - t) + Mathf.Sin(t * Mathf.PI) * 1.5f);
            if (t < 1) return;
            // 착지한 뒤에만 통행을 막아 플레이어/보스 위로 즉시 충돌체가 솟지 않게 합니다.
            collision.enabled = true;
            obstacle.enabled = true;
            Destroy(this);
        }
    }
}
