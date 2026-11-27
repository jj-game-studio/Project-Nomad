using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossOrbitingFlames : MonoBehaviour
    {
        private readonly Transform[] orbs = new Transform[3];
        private DragonBossCombat boss;
        private BossArena arena;
        private Health player;
        private Vector3 center;
        private float age, delay, duration, nextHit;
        public int OrbCount => orbs.Length;

        public static BossOrbitingFlames Create(Transform parent, DragonBossCombat boss, BossArena arena, Health player, float delay, float duration)
        {
            var root = new GameObject("Slow orbiting flames");
            root.transform.SetParent(parent, false);
            var orbit = root.AddComponent<BossOrbitingFlames>();
            orbit.boss = boss; orbit.arena = arena; orbit.player = player;
            orbit.center = boss.transform.position; orbit.delay = delay; orbit.duration = duration;
            for (int index = 0; index < orbit.orbs.Length; index++) orbit.BuildOrb(index);
            return orbit;
        }

        private void BuildOrb(int index)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Orbiting living fire";
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one * 1.65f;
            root.GetComponent<Collider>().enabled = false;
            Destroy(root.GetComponent<Collider>());
            root.GetComponent<Renderer>().sharedMaterial = BossFlameVfx.Molten;
            BossFlameVfx.Jet(root.transform, Vector3.zero, Vector3.back * 2.2f, 0.4f, 0.025f);
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(2.6f, 0.55f, 0.035f, 0.8f));
            root.GetComponent<Renderer>().SetPropertyBlock(tint);
            orbs[index] = root.transform;
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "White hot orbit core";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.45f;
            core.GetComponent<Collider>().enabled = false; Destroy(core.GetComponent<Collider>());
            core.GetComponent<Renderer>().sharedMaterial = WeaponVfx.Glow;
            var hot = new MaterialPropertyBlock(); hot.SetColor("_BaseColor", new Color(3, 1.8f, 0.3f, 0.95f));
            core.GetComponent<Renderer>().SetPropertyBlock(hot);

            var fire = WeaponVfx.CreateParticles(root.transform, "Orbit flame tongues", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            var main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.65f);
            main.startColor = new Color(2.2f, 0.55f, 0.03f);
            var shape = fire.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.6f;
            var emission = fire.emission;
            emission.enabled = true; emission.rateOverTime = 36;
            WeaponVfx.Fade(fire, 0);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = WeaponVfx.Beam; trail.time = 1.2f;
            trail.startWidth = 1.1f; trail.endWidth = 0;
            trail.startColor = new Color(1.8f, 0.3f, 0.015f, 0.65f); trail.endColor = Color.clear;
            root.SetActive(false);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (boss == null || player == null || age > delay + duration) { Destroy(gameObject); return; }
            if (age < delay) return;
            bool visible = !player.IsDead && boss.Introduced && boss.Phase != DragonPhase.Waiting && boss.Phase != DragonPhase.Arriving &&
                boss.Phase != DragonPhase.Stunned && boss.Phase != DragonPhase.Dead && !boss.EnrageTransitioning;
            center = boss.transform.position;
            foreach (Transform orb in orbs) orb.gameObject.SetActive(visible);
            if (!visible) return;
            for (int index = 0; index < orbs.Length; index++)
            {
                Transform orb = orbs[index];
                float radians = (index * 120 + (age - delay) * boss.Tuning.OrbitSpeed) * Mathf.Deg2Rad;
                float radius = boss.Tuning.OrbitRadius;
                Vector3 point = center + new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * radius;
                orb.position = arena.GroundPoint(point) + Vector3.up * 1.1f;
                orb.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(radians), 0, -Mathf.Sin(radians)));
                orb.gameObject.SetActive(true);
                orb.localScale = Vector3.one * (1.65f + Mathf.Sin(Time.time * 9 + index) * 0.1f);
                Collider body = player.GetComponent<Collider>();
                if (body != null && Time.time >= nextHit && Vector3.Distance(body.ClosestPoint(orb.position), orb.position) < 1)
                {
                    nextHit = Time.time + 1;
                    if (player.TryApplyDamage(boss.ScaleDamage(14)))
                        PlayerBossReaction.Play(player, center, boss.Tuning.OrbitKnockback, 0.55f, 0, 0.2f);
                }
            }
        }
    }
}
