using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Monsters;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(110)]
    public sealed partial class BossEnrageTransition : MonoBehaviour
    {
        public const float Seconds = 4.2f;
        public bool Started { get; private set; }
        public bool Completed { get; private set; }
        public float Progress => Started ? Mathf.Clamp01((Time.time - since) / Seconds) : 0;
        public float VisualAmount => Mathf.SmoothStep(0, 1, Progress);
        private DragonBossCombat boss;
        private Health health, player;
        private PlayerBossReaction reaction;
        private Animator animator;
        private MonsterAnimationDriver driver;
        private DragonLegMotion feet;
        private bool immune, driverWasEnabled, feetWereEnabled, burst;
        private float since, animationSpeed;
        private ParticleSystem aura;
        private AudioClip roar;

        public void Configure(DragonBossCombat owner, Health victim)
        {
            boss = owner; player = victim; health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            driver = GetComponent<MonsterAnimationDriver>();
            feet = GetComponent<DragonLegMotion>();
            ConfigurePose();
            aura = WeaponVfx.CreateParticles(transform, "Enrage rising flame aura", WeaponVfx.Glow);
            var main = aura.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new Color(2, 0.15f, 0.015f);
            WeaponVfx.Fade(aura, 0);
        }

        private void Update()
        {
            RestorePose();
            if (!Started && boss.Introduced && boss.EnrageRequested) Begin();
            if (!Started || Completed) return;
            if (health.IsDead)
            {
                RestoreState();
                reaction?.Cancel();
                Completed = true;
                return;
            }
            if (!burst && Time.time - since >= 1.1f)
            {
                burst = true;
                Roar();
                HitCameraImpulse.Pulse(0.2f, 0.25f);
                for (int index = 0; index < 100; index++)
                    aura.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * 1.5f,
                        velocity = Quaternion.Euler(0, index * 137.5f, 0) * Vector3.forward * Random.Range(3, 8) + Vector3.up }, 1);
            }
            if (Progress < 1) return;
            Completed = true;
            RestoreState();
            boss.FinishEnrage();
        }

        private void Begin()
        {
            Started = true;
            since = Time.time;
            boss.BeginEnrage();
            immune = health.IsCombatImmune;
            health.IsCombatImmune = true;
            driverWasEnabled = driver != null && driver.enabled;
            feetWereEnabled = feet != null && feet.enabled;
            if (driver != null) driver.enabled = false;
            if (feet != null) feet.enabled = false;
            if (animator != null) { animationSpeed = animator.speed; animator.Play("Base Layer.Idle_A", 0, 0); animator.Update(0); animator.speed = 0; }
            reaction = PlayerBossReaction.Play(player, transform.position, 4.5f, Seconds, 1.1f);
        }

        private void RestoreState()
        {
            RestorePose();
            if (health != null) health.IsCombatImmune = immune;
            if (driver != null && !health.IsDead) driver.enabled = driverWasEnabled;
            if (feet != null && !health.IsDead) feet.enabled = feetWereEnabled;
            if (animator != null) animator.speed = animationSpeed;
        }

        private void OnDisable()
        {
            if (Started && !Completed) { RestoreState(); reaction?.Cancel(); }
            RestorePose();
        }

        private void OnDestroy() { if (roar != null) Destroy(roar); }
    }
}
