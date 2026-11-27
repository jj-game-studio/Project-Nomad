using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(375)]
    public sealed class BossStrikeFeedback : MonoBehaviour
    {
        private Health health;
        private BossArmor armor;
        private Transform player, head;
        private CombatFeedbackOverlay overlay;
        private AudioSource sound;
        private ParticleSystem sparks, fragments;
        private Mesh shardMesh;
        private Material shardMaterial;
        private Renderer[] bodies;
        private Quaternion headBase, headApplied;
        private bool recoilApplied;
        private float hitAt = -10, lastSound = -10;
        private MaterialPropertyBlock tint;
        public int LastDamage { get; private set; }

        private void Awake()
        {
            tint = new MaterialPropertyBlock();
            health = GetComponent<Health>();
            armor = GetComponent<BossArmor>();
            overlay = GetComponent<CombatFeedbackOverlay>() ?? gameObject.AddComponent<CombatFeedbackOverlay>();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0.2f;
            sound.maxDistance = 35;
            Transform body = transform.Find("Body");
            bodies = body.GetComponentsInChildren<Renderer>();
            head = System.Array.Find(body.GetComponentsInChildren<Transform>(), bone => bone.name == "neck3");
            GetComponent<MonsterHitFeedback>()?.SetEffects(false, false, false);
            CreateParticles();
        }

        public void Configure(Transform victim) => player = victim;
        private void OnEnable() { health.Damaged += OnDamaged; armor.Broken += OnArmorBroken; }
        private void OnDisable() { health.Damaged -= OnDamaged; armor.Broken -= OnArmorBroken; RestoreHead(); }
        private void Update() => RestoreHead();

        private void RestoreHead()
        {
            if (recoilApplied && head != null && Quaternion.Angle(head.localRotation, headApplied) < 0.05f) head.localRotation = headBase;
            recoilApplied = false;
        }

        private Vector3 ImpactPoint()
        {
            Vector3 from = player != null ? player.position + Vector3.up * 1.3f : transform.position + transform.forward * 3 + Vector3.up;
            return GetComponent<Collider>().ClosestPoint(from);
        }

        private void OnDamaged(Health _, int amount, DamageKind kind)
        {
            LastDamage = amount;
            hitAt = Time.time;
            Vector3 point = ImpactPoint();
            overlay.ShowDamage(amount, point + Vector3.up * 0.4f, armor.BreakCount > 0 ? new Color(1, 0.65f, 0.18f) : new Color(1, 0.9f, 0.6f));
            Emit(sparks, point, amount >= 80 ? 24 : 14, 4);
            HitCameraImpulse.Pulse(amount >= 80 ? 0.12f : 0.075f, 0.1f);
            if (Time.time - lastSound > 0.08f)
            {
                lastSound = Time.time;
                CombatImpactAudio.Play(sound, false, 0.28f);
            }
        }

        private void OnArmorBroken(int stage)
        {
            Vector3 point = transform.position + Vector3.up * 1.8f;
            overlay.ArmorBreak(stage);
            Emit(fragments, point, 28, 5);
            Emit(sparks, point, 40, 6);
            HitCameraImpulse.Pulse(0.2f, 0.2f);
            CombatImpactAudio.Play(sound, true, 0.45f);
        }

        private void Emit(ParticleSystem particles, Vector3 point, int count, float speed)
        {
            Vector3 outward = player != null ? Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).normalized : -transform.forward;
            for (int index = 0; index < count; index++)
                particles.Emit(new ParticleSystem.EmitParams { position = point,
                    velocity = outward * speed * 0.5f + Random.insideUnitSphere * speed + Vector3.up * 2 }, 1);
        }

        private void CreateParticles()
        {
            sparks = WeaponVfx.CreateParticles(transform, "Boss strike sparks", WeaponVfx.Glow);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.19f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1, 0.55f, 0.08f));
            WeaponVfx.Shrink(sparks);

            shardMesh = new Mesh { name = "Broken armor shard" };
            shardMesh.vertices = new[] { new Vector3(0, 0.8f, 0), new Vector3(-0.5f, -0.3f, 0.3f), new Vector3(0.6f, -0.3f, 0.3f), new Vector3(0, -0.3f, -0.4f) };
            shardMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2 };
            shardMesh.RecalculateNormals();
            shardMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.35f, 0.22f, 0.12f) };
            fragments = WeaponVfx.CreateParticles(transform, "Armor fragments", shardMaterial);
            main = fragments.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
            main.gravityModifier = 1.5f;
            var renderer = fragments.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = shardMesh;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            if (health.IsDead) { overlay.HideArmor(); return; }
            overlay.ShowArmor(armor.BreakCount, armor.DamageMultiplier);
            float flash = Mathf.Clamp01(1 - (Time.time - hitAt) / 0.075f);
            foreach (Renderer renderer in bodies)
            {
                if (renderer is ParticleSystemRenderer || renderer.sharedMaterial == null) continue;
                Color baseColor = renderer.sharedMaterial.GetColor("_BaseColor");
                Color stage = Color.Lerp(baseColor, new Color(0.95f, 0.3f, 0.08f), armor.BreakCount * 0.12f);
                renderer.GetPropertyBlock(tint);
                tint.SetColor("_BaseColor", Color.Lerp(stage, new Color(1.6f, 1.2f, 0.6f), flash * 0.7f));
                renderer.SetPropertyBlock(tint);
            }
            if (head == null || health.IsDead) return;
            float age = Time.time - hitAt;
            if (age >= 0.16f) return;
            headBase = head.localRotation;
            head.rotation = Quaternion.AngleAxis(-Mathf.Sin(age / 0.16f * Mathf.PI) * 7, transform.right) * head.rotation;
            headApplied = head.localRotation;
            recoilApplied = true;
        }

        private void OnDestroy()
        {
            if (shardMesh != null) Destroy(shardMesh);
            if (shardMaterial != null) Destroy(shardMaterial);
        }
    }
}
