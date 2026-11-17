using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    // 근접 공격의 상태 기계를 공유해 추적 정지/재개와 공격 공속이 서로 경쟁하지 않게 합니다.
    public sealed class MonsterRangedAttack : MonoBehaviour
    {
        public GameObject Visual { get; private set; }
        public float Range { get; private set; }
        public float Windup { get; private set; }
        public float Recovery { get; private set; }
        public float Speed { get; private set; }
        public int Damage { get; private set; }
        public int ShotsFired { get; private set; }
        private Vector3 muzzle;

        public void Configure(GameObject visual, float range, float speed, int damage,
            float windup, float recovery, Vector3 localMuzzle)
        {
            Visual = visual;
            Range = Mathf.Max(1f, range);
            Speed = Mathf.Max(1f, speed);
            Damage = Mathf.Max(1, damage);
            Windup = Mathf.Max(0.1f, windup);
            Recovery = Mathf.Max(0.1f, recovery);
            muzzle = localMuzzle;
        }

        public bool CanBegin(Health target, float meleeRange)
        {
            if (!enabled || Visual == null || target == null || target.IsDead) return false;
            float distance = Vector3.Distance(target.transform.position, transform.position);
            if (distance <= meleeRange + 0.3f || distance > Range) return false;
            Vector3 origin = transform.TransformPoint(muzzle);
            Vector3 path = target.transform.position + Vector3.up - origin;
            return Projectile.TryGetFirstHit(new Ray(origin, path.normalized), path.magnitude,
                Physics.DefaultRaycastLayers, transform, out RaycastHit hit) && hit.collider.GetComponentInParent<Health>() == target;
        }

        public void Release(Health target, Vector3 lockedAim)
        {
            if (GetComponent<Health>().IsDead || !enabled) return;
            if (MonsterBolt.Launch(Visual, transform, target, transform.TransformPoint(muzzle),
                lockedAim, Speed, Range, Damage)) ShotsFired++;
        }
    }
}
