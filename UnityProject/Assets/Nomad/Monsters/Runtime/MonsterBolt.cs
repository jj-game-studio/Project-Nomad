using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    public sealed class MonsterBolt : MonoBehaviour
    {
        private Transform owner;
        private Health target;
        private float speed;
        private float remaining;
        private int damage;

        public static bool Launch(GameObject visual, Transform owner, Health target, Vector3 origin,
            Vector3 aim, float speed, float range, int damage)
        {
            if (visual == null || target == null || target.IsDead || speed <= 0f || range <= 0f) return false;
            Vector3 direction = aim - origin;
            if (direction.sqrMagnitude < 0.01f) return false;
            Vector3 body = new Vector3(owner.position.x, origin.y, owner.position.z);
            Vector3 barrel = origin - body;
            if (barrel.sqrMagnitude > 0.0001f && Projectile.TryGetFirstHit(new Ray(body, barrel.normalized),
                barrel.magnitude, Physics.DefaultRaycastLayers, owner, out _)) return false;

            var root = new GameObject("Monster bolt");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            Instantiate(visual, root.transform);
            MonsterBolt bolt = root.AddComponent<MonsterBolt>();
            bolt.owner = owner;
            bolt.target = target;
            bolt.speed = speed;
            bolt.remaining = range;
            bolt.damage = Mathf.Max(1, damage);
            return true;
        }

        private void FixedUpdate()
        {
            float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
            Ray ray = new Ray(transform.position, transform.forward);
            if (Projectile.TryGetFirstHit(ray, step, Physics.DefaultRaycastLayers, owner, out RaycastHit hit))
            {
                enabled = false;
                if (hit.collider.GetComponentInParent<Health>() == target && target != null)
                    target.TryApplyDamage(damage);
                Destroy(gameObject);
                return;
            }
            transform.position = ray.GetPoint(step);
            remaining -= step;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
