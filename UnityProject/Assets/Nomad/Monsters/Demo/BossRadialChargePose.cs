using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(365)]
    public sealed class BossRadialChargePose : MonoBehaviour
    {
        private DragonBossCombat boss;
        private Transform neck;
        private Quaternion original, applied;
        private bool posed;
        private ParticleSystem gathering;
        public float Weight { get; private set; }

        public void Configure(DragonBossCombat owner)
        {
            boss = owner;
            neck = System.Array.Find(GetComponentsInChildren<Transform>(true), bone => bone.name == "neck1");
            gathering = WeaponVfx.CreateParticles(transform, "Gathering flame energy", WeaponVfx.Glow);
            var main = gathering.main;
            main.startLifetime = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(2, 0.45f, 0.02f);
            WeaponVfx.Fade(gathering, 0);
        }

        private void Update()
        {
            if (posed && neck != null && Quaternion.Angle(neck.localRotation, applied) < 0.05f) neck.localRotation = original;
            posed = false;
        }

        private void LateUpdate()
        {
            if (boss == null || neck == null) return;
            if (boss.Phase == DragonPhase.Dead) { Weight = 0; gathering.Clear(); return; }
            bool preparing = boss.Phase == DragonPhase.Windup && (boss.Pattern == DragonPattern.RadialFireballs || boss.Pattern == DragonPattern.Breath || boss.Pattern == DragonPattern.FirePool);
            float desired = preparing ? Mathf.SmoothStep(0, 1, 1 - boss.Remaining / 1.8f) : 0;
            Weight = Mathf.MoveTowards(Weight, desired, Time.deltaTime * 5);
            if (Weight > 0)
            {
                original = neck.localRotation;
                float lift = boss.Pattern == DragonPattern.Breath ? -12 : -32;
                float recoil = boss.Phase == DragonPhase.Active && boss.Pattern == DragonPattern.Breath ? Mathf.Sin(Time.time * 24) * 2.5f : 0;
                neck.rotation = Quaternion.AngleAxis(lift * Weight + recoil, transform.right) * neck.rotation;
                applied = neck.localRotation;
                posed = true;
            }
            if (!preparing) return;
            for (int index = 0; index < Mathf.CeilToInt(100 * Time.deltaTime); index++)
            {
                Vector3 point = boss.Mouth.position + Random.onUnitSphere * Mathf.Lerp(1.5f, 0.7f, Weight);
                gathering.Emit(new ParticleSystem.EmitParams { position = point, velocity = (boss.Mouth.position - point) * 5 }, 1);
            }
        }

        private void OnDisable()
        {
            if (posed && neck != null && Quaternion.Angle(neck.localRotation, applied) < 0.05f) neck.localRotation = original;
            Weight = 0;
            posed = false;
        }
    }
}
