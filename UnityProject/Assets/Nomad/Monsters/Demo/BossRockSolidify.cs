using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed class BossRockSolidify : MonoBehaviour
    {
        private BoxCollider solid;
        private NavMeshObstacle obstacle;
        private float age;

        private void Awake()
        {
            solid = transform.Find("Rock collision").GetComponent<BoxCollider>();
            obstacle = solid.GetComponent<NavMeshObstacle>();
            solid.enabled = false;
            obstacle.enabled = false;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < 0.4f) return;
            foreach (Collider hit in Physics.OverlapBox(solid.transform.position, solid.size * 0.55f, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.GetComponentInParent<Health>() is Health health && !health.IsDead) return;
            solid.enabled = true;
            obstacle.enabled = true;
            Destroy(this);
        }
    }
}
