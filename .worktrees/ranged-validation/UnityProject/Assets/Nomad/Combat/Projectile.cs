using UnityEngine;

namespace Nomad.Combat
{
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        private const float MuzzleClearance = 0.01f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[16];

        private Transform owner;
        private LayerMask collisionLayers;
        private float speed;
        private float remainingDistance;
        private int damage;

        public static bool TryLaunch(Projectile prefab, Transform owner, Vector3 origin, Vector3 aimPoint,
            LayerMask layers, float speed, float range, int damage)
        {
            Vector3 path = aimPoint - origin;
            if (prefab == null || owner == null || speed <= 0f || range <= 0f || damage <= 0 || path.sqrMagnitude < 0.000001f)
                return false;

            if (IsMuzzleBlocked(origin, layers, owner))
                return false;

            // 발사구가 얇은 벽의 반대편까지 들어간 경우도 몸통에서 발사구까지 검사합니다.
            Vector3 body = new Vector3(owner.position.x, origin.y, owner.position.z);
            Vector3 barrel = origin - body;
            if (barrel.sqrMagnitude > 0f && TryGetFirstHit(new Ray(body, barrel.normalized),
                    barrel.magnitude, layers, owner, out _))
                return false;

            Projectile projectile = Instantiate(prefab, origin, Quaternion.LookRotation(path.normalized));
            projectile.owner = owner;
            projectile.collisionLayers = layers;
            projectile.speed = speed;
            projectile.remainingDistance = range;
            projectile.damage = damage;
            return true;
        }

        private void FixedUpdate()
        {
            float distance = Mathf.Min(speed * Time.fixedDeltaTime, remainingDistance);
            Ray path = new Ray(transform.position, transform.forward);
            if (TryGetFirstHit(path, distance, collisionLayers, owner, out RaycastHit hit))
            {
                // 사망 이벤트에서 다른 게임 로직을 실행해도 이 발사체는 한 번만 피해를 줍니다.
                enabled = false;
                Destroy(gameObject);
                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                    health.TryApplyDamage(damage);
                return;
            }

            transform.position = path.GetPoint(distance);
            remainingDistance -= distance;
            if (remainingDistance <= 0f)
                Destroy(gameObject);
        }

        public static bool TryGetFirstHit(Ray ray, float distance, LayerMask layers, Transform owner,
            out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(ray, Hits, distance, layers, QueryTriggerInteraction.Ignore);
            RaycastHit[] candidates = Hits;
            if (count == Hits.Length)
            {
                // 결과가 잘렸으면 가장 가까운 충돌이 배열에 포함된다는 보장이 없습니다.
                candidates = Physics.RaycastAll(ray, distance, layers, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }

            closest = default;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = candidates[index];
                if ((owner != null && hit.collider.transform.IsChildOf(owner)) || hit.distance >= nearestDistance)
                    continue;

                closest = hit;
                nearestDistance = hit.distance;
            }

            return closest.collider != null;
        }

        private static bool IsMuzzleBlocked(Vector3 origin, LayerMask layers, Transform owner)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, MuzzleClearance, Overlaps, layers,
                QueryTriggerInteraction.Ignore);
            Collider[] candidates = Overlaps;
            if (count == Overlaps.Length)
            {
                candidates = Physics.OverlapSphere(origin, MuzzleClearance, layers, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }

            for (int index = 0; index < count; index++)
            {
                if (!candidates[index].transform.IsChildOf(owner))
                    return true;
            }

            return false;
        }
    }
}
