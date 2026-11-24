using UnityEngine;

namespace Nomad.Equipment
{
    // 주변의 작은 입자가 무기 끝으로 모이고 중심에 쌓이는 차징 효과입니다.
    [DefaultExecutionOrder(250)]
    public sealed class ChargeGatherVfx : MonoBehaviour
    {
        private readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[128];
        private PlayerEquipmentCombat combat;
        private ParticleSystem particles;
        private float pending;
        public int ParticleCount => particles == null ? 0 : particles.particleCount;

        public void Configure(PlayerEquipmentCombat source) => combat = source;

        private void Awake()
        {
            particles = WeaponVfx.CreateParticles(transform, "Gathering Motes", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            var main = particles.main;
            main.maxParticles = buffer.Length;
            main.startLifetime = 1.3f;
            main.startSize = 0.05f;
            WeaponVfx.Fade(particles, 0.65f);
        }

        private void LateUpdate()
        {
            if (combat == null || !combat.IsCharging)
            {
                Clear();
                return;
            }
            transform.SetPositionAndRotation(combat.ProjectileOrigin, Quaternion.identity);
            float progress = combat.ChargeProgress;
            if (!WeaponVfx.TryGetStyle(combat.CurrentWeapon, out WeaponVfxStyle style))
                style = new WeaponVfxStyle(new Color(1f, 0.96f, 0.7f), new Color(1f, 0.6f, 0.12f), 1f);

            int count = particles.GetParticles(buffer);
            for (int index = 0; index < count; index++)
            {
                Vector3 point = buffer[index].position;
                // 중심 근처 입자는 바로 지우지 않아 충전량이 늘수록 밝은 입자가 쌓입니다.
                if (point.sqrMagnitude > 0.003f)
                    buffer[index].position = Vector3.MoveTowards(point, Vector3.zero, Time.deltaTime * (1.1f + progress));
                buffer[index].velocity = Vector3.zero;
                buffer[index].startColor = Color.Lerp(style.Core, style.Glow, Mathf.Clamp01(point.magnitude));
            }
            particles.SetParticles(buffer, count);

            pending += Time.deltaTime * Mathf.Lerp(20, 75, progress);
            while (pending >= 1)
            {
                pending--;
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = Random.onUnitSphere * Random.Range(0.5f, 0.95f),
                    velocity = Vector3.zero,
                    startSize = Random.Range(0.035f, 0.075f),
                    startColor = style.Glow,
                    startLifetime = 1.3f
                }, 1);
            }
        }

        private void OnDisable() => Clear();

        private void Clear()
        {
            pending = 0;
            if (particles != null && particles.particleCount > 0) particles.Clear();
        }
    }
}
