using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.Monsters;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(280)]
    public sealed class BossArrival : MonoBehaviour
    {
        public const float WarningSeconds = 1.2f;
        public const float FallSeconds = 0.65f;
        public bool Started { get; private set; }
        public bool Landed { get; private set; }
        public bool BodyVisible => body != null && body.gameObject.activeSelf;
        private DragonBossCombat boss;
        private BossArena arena;
        private Health health, player;
        private Transform body;
        private Vector3 resting;
        private Collider collision;
        private SegmentedHealthBar bar;
        private MonsterAnimationDriver driver;
        private DragonLegMotion feet;
        private bool driverWasEnabled;
        private BossHazard warning;
        private float started;
        private bool wasImmune;

        public void Configure(DragonBossCombat owner, BossArena room, Health target)
        {
            boss = owner;
            arena = room;
            player = target;
            health = GetComponent<Health>();
            body = transform.Find("Body");
            resting = body.localPosition;
            collision = GetComponent<Collider>();
            bar = GetComponent<SegmentedHealthBar>();
            wasImmune = health.IsCombatImmune;
            driver = GetComponent<MonsterAnimationDriver>();
            feet = GetComponent<DragonLegMotion>();
            driverWasEnabled = driver.enabled;
            driver.enabled = false;
            if (feet != null) feet.enabled = false;

            health.IsCombatImmune = true;
            collision.enabled = false;
            bar.enabled = false;
            body.gameObject.SetActive(false);
        }

        public void Begin()
        {
            if (Started || Landed) return;
            Started = true;
            started = Time.time;
            warning = BossHazard.Create(boss.Effects, transform, arena.GroundPoint(transform.position), Quaternion.identity,
                player, 6, 360, WarningSeconds + FallSeconds, 0.12f, 24);
            warning.EmitsFlame = false;
            warning.WarningSeconds = WarningSeconds;
            warning.JumpClearance = 0.65f;
            warning.GroundHeight = arena.GroundHeight;
        }

        private void Update()
        {
            if (!Started || Landed || health.IsDead) return;
            float elapsed = Time.time - started;
            if (elapsed < WarningSeconds) return;
            body.gameObject.SetActive(true);
            float progress = Mathf.Clamp01((elapsed - WarningSeconds) / FallSeconds);
            body.localPosition = resting + Vector3.up * (12 * (1 - progress * progress));
            if (progress < 1) return;
            Land(true);
        }

        // 전용 테스트/디버그에서 패턴 하나를 바로 검사할 때도 같은 활성화 경로를 씁니다.
        public void Skip() => Land(false);

        private void Land(bool impact)
        {
            if (Landed) return;
            Landed = true;
            body.gameObject.SetActive(true);
            body.localPosition = resting;
            collision.enabled = true;
            bar.enabled = true;
            driver.enabled = driverWasEnabled;
            if (feet != null) feet.enabled = true;
            health.IsCombatImmune = wasImmune;
            boss.FinishArrival();
            if (!impact)
            {
                if (warning != null) { warning.gameObject.SetActive(false); Destroy(warning.gameObject); }
                return;
            }
            BossGroundCracks.Create(boss.Effects, arena, transform.position, 6);
            HitCameraImpulse.Pulse(0.42f, 0.25f);
            CombatImpactAudio.Play(GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>(), true, 0.5f);
        }
    }
}
