using System.Collections.Generic;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    [RequireComponent(typeof(Health))]
    [DefaultExecutionOrder(150)]
    public sealed class MonsterHitFeedback : MonoBehaviour
    {
        private struct DamageNumber
        {
            public int amount;
            public Vector3 position;
            public float age;
            public bool critical;
        }

        private const float NumberDuration = 0.8f;
        private readonly List<DamageNumber> numbers = new List<DamageNumber>(8);
        private Health health;
        private float flashRemaining;
        private AudioSource sound;
        private bool flashEnabled = true;
        private bool numbersEnabled = true;
        private bool shardsEnabled = true;
        private ParticleSystem shards;
        private Mesh shardMesh;
        private Material shardMaterial;
        private GUIStyle numberStyle;

        public bool IsFlashing => flashEnabled && CombatFeedbackSettings.Current.MonsterFlash && flashRemaining > 0f;
        public int ActiveNumberCount => numbers.Count;
        public int LastDamage { get; private set; }
        public bool LastCritical { get; private set; }
        public int ActiveCriticalNumberCount
        {
            get
            {
                int count = 0;
                foreach (DamageNumber number in numbers) if (number.critical) count++;
                return count;
            }
        }
        public int ActiveShardCount => shards == null ? 0 : shards.particleCount;

        private void Awake()
        {
            health = GetComponent<Health>();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 1f;
            sound.minDistance = 5f;
            sound.maxDistance = 35f;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            LastDamage = 0;
            LastCritical = false;
        }

        public void SetEffects(bool flash, bool damageNumbers, bool particles)
        {
            flashEnabled = flash;
            numbersEnabled = damageNumbers;
            shardsEnabled = particles;
            if (!flash)
                flashRemaining = 0f;
            if (!damageNumbers)
                numbers.Clear();
            if (!particles && shards != null)
                shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            RefreshEffects();
            flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                DamageNumber number = numbers[i];
                number.age += Time.deltaTime;
                if (number.age >= NumberDuration)
                    numbers.RemoveAt(i);
                else
                    numbers[i] = number;
            }

        }

        private void OnDamaged(Health owner, int damage, DamageKind kind) => PreviewHit(damage, owner.LastHitCritical);

        // HP와 AI 상태를 변경하지 않고 실제 적중과 같은 표시 경로를 재생합니다.
        public void PreviewHit(int damage, bool critical = false)
        {
            if (!isActiveAndEnabled || damage <= 0 || Time.timeScale <= 0) return;
            var settings = CombatFeedbackSettings.Current;

            LastDamage = damage;
            LastCritical = critical;
            if (flashEnabled && settings.MonsterFlash)
                flashRemaining = Mathf.Max(flashRemaining, critical ? settings.CriticalFlashSeconds : settings.FlashSeconds);
            if (settings.MonsterShake)
                HitCameraImpulse.Pulse(settings.MonsterShakeStrength * (critical ? settings.CriticalShakeMultiplier : 1f), 0.09f);
            HitImpactAudio.PlayMonster(sound, critical);

            if (numbersEnabled && settings.MonsterNumbers)
            {
                if (numbers.Count == 8)
                    numbers.RemoveAt(0);
                numbers.Add(new DamageNumber { amount = damage, critical = critical,
                    position = transform.position + Vector3.up * 2.9f });
            }
            if (shardsEnabled && settings.MonsterShards)
                EmitShards(critical);
        }

        public void RefreshEffects()
        {
            var settings = CombatFeedbackSettings.Current;
            if (!settings.MonsterFlash || !flashEnabled) flashRemaining = 0;
            if (!settings.MonsterNumbers || !numbersEnabled) numbers.Clear();
            if ((!settings.MonsterShards || !shardsEnabled) && shards != null)
                shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!settings.MonsterSound && sound != null) sound.Stop();
        }

        private void EmitShards(bool critical)
        {
            if (shards == null)
                CreateShards();

            shards.transform.position = transform.position + Vector3.up;
            shards.Play();
            for (int i = 0; i < (critical ? 16 : 8); i++)
            {
                Vector3 velocity = Random.onUnitSphere * Random.Range(1.5f, 3f);
                velocity.y = Mathf.Abs(velocity.y) + 1f;
                shards.Emit(new ParticleSystem.EmitParams { velocity = velocity }, 1);
            }
        }

        private void CreateShards()
        {
            var root = new GameObject("Hit shards");
            root.transform.SetParent(transform, false);
            shards = root.AddComponent<ParticleSystem>();
            shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = shards.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.65f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            var emission = shards.emission;
            emission.enabled = false;
            var shape = shards.shape;
            shape.enabled = false;

            // 외부 에셋 없이 작은 사면체 메시를 파편으로 사용합니다.
            shardMesh = new Mesh { name = "Procedural hit shard" };
            shardMesh.vertices = new[] { new Vector3(0f, 0.6f, 0f), new Vector3(-0.5f, -0.4f, 0.4f),
                new Vector3(0.5f, -0.4f, 0.4f), new Vector3(0f, -0.4f, -0.5f) };
            shardMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2 };
            shardMesh.RecalculateNormals();
            shardMesh.RecalculateBounds();
            shardMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            shardMaterial.SetColor("_BaseColor", new Color(1f, 0.72f, 0.2f));
            var renderer = shards.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = shardMesh;
            renderer.sharedMaterial = shardMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (camera == null || numbers.Count == 0)
                return;

            if (numberStyle == null)
                numberStyle = new GUIStyle(GUI.skin.label) { fontSize = 22,
                    fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (DamageNumber number in numbers)
            {
                Vector3 screen = camera.WorldToScreenPoint(number.position + Vector3.up * number.age);
                if (screen.z <= 0f)
                    continue;

                numberStyle.fontSize = Mathf.RoundToInt(Mathf.Lerp(30, 22, Mathf.Clamp01(number.age / 0.16f)));
                if (number.critical) numberStyle.fontSize += 10;
                Rect rect = new Rect(screen.x - 50f, Screen.height - screen.y - 20f, 100f, 40f);
                string text = number.amount + (number.critical ? "!" : "");
                float alpha = 1f - number.age / NumberDuration;
                numberStyle.normal.textColor = new Color(0f, 0f, 0f, alpha);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, numberStyle);
                numberStyle.normal.textColor = number.critical ? new Color(1f, 0.4f, 0.05f, alpha) : new Color(1f, 0.85f, 0.25f, alpha);
                GUI.Label(rect, text, numberStyle);
            }
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            ClearFeedback();
        }

        public void ClearFeedback()
        {
            flashRemaining = 0f;
            numbers.Clear();
            if (sound != null) sound.Stop();
            if (shards != null)
                shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            if (shardMesh != null) Destroy(shardMesh);
            if (shardMaterial != null) Destroy(shardMaterial);
        }
    }
}
