using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    // 움직이는 조준선이 멈춘 뒤 측면으로 빠질 수 있는 직사각형 브레스입니다.
    public sealed class BossLineBreath : MonoBehaviour
    {
        public const float Range = 26, HalfWidth = 1.8f;
        private DragonBossCombat boss;
        private Health target;
        private BossArena arena;
        private float fireAt, endAt, nextHit;
        private LineRenderer warning;
        private ParticleSystem fire;
        public bool Fired { get; private set; }
        public Vector3 Heading => transform.forward;

        public static BossLineBreath Create(Transform parent, DragonBossCombat owner, BossArena room, Health player, float delay)
        {
            var root = new GameObject("Locked straight breath");
            root.transform.SetParent(parent, false);
            var attack = root.AddComponent<BossLineBreath>();
            attack.boss = owner; attack.target = player; attack.arena = room;
            attack.fireAt = Time.time + delay; attack.endAt = attack.fireAt + 1.3f / owner.Tempo;
            attack.SetHeading(owner.transform.forward);
            attack.warning = root.AddComponent<LineRenderer>();
            attack.warning.sharedMaterial = WeaponVfx.Beam;
            attack.warning.loop = true; attack.warning.positionCount = 4;
            attack.warning.widthMultiplier = 0.09f;
            attack.warning.startColor = attack.warning.endColor = new Color(1.5f, 0.12f, 0.015f);
            return attack;
        }

        public void SetHeading(Vector3 heading)
        {
            if (heading.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(heading);
        }

        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            float reach = Fired ? Range * Mathf.Clamp01((Time.time-fireAt)/0.45f) : Range;
            return Mathf.Abs(local.x) <= HalfWidth && local.z >= 0 && local.z <= reach && Mathf.Abs(local.y) < 3;
        }

        private void Update()
        {
            if (boss == null || target == null || target.IsDead) { Destroy(gameObject); return; }
            if (!Fired)
            {
                Vector3 origin = boss.Mouth.position;
                transform.position = arena.GroundPoint(origin);
                Vector3[] corners = { new(-HalfWidth,0,0), new(-HalfWidth,0,Range), new(HalfWidth,0,Range), new(HalfWidth,0,0) };
                for (int i = 0; i < 4; i++) warning.SetPosition(i, arena.GroundPoint(transform.TransformPoint(corners[i])) + Vector3.up * 0.25f);
                if (Time.time < fireAt) return;
                Fired = true; warning.enabled = false;
                fire = WeaponVfx.CreateParticles(transform, "Focused breath jet", WeaponVfx.Glow);
                var emission = fire.emission; emission.enabled = false;
                var main = fire.main; main.startLifetime = 0.45f; main.startSpeed = 0;
                main.startColor = new Color(2.8f, 0.65f, 0.03f);
                WeaponVfx.Fade(fire, 0); WeaponVfx.Shrink(fire);
                BossCombatImpact.Pulse(0.16f);
            }
            for (int i = 0; i < Mathf.CeilToInt(Time.deltaTime * 550); i++)
            {
                Vector3 position = boss.Mouth.position + transform.right * Random.Range(-0.2f, 0.2f);
                fire.Emit(new ParticleSystem.EmitParams { position = position, velocity = transform.forward * (Range / 0.45f), startSize = Random.Range(1, 1.8f) }, 1);
            }
            if (Time.time >= nextHit)
            {
                nextHit = Time.time + 0.5f;
                if (Contains(target.transform.position)) target.TryApplyDamage(boss.ScaleDamage(16));
            }
            if (Time.time >= endAt) Destroy(gameObject);
        }
    }
}
