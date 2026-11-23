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
        }

        private const float FlashDuration = 0.16f;
        private const float NumberDuration = 0.8f;
        private readonly List<DamageNumber> numbers = new List<DamageNumber>(8);
        private Health health;
        private int previousHp;
        private float flashRemaining;
        private bool flashEnabled = true;
        private bool numbersEnabled = true;
        private bool shardsEnabled = true;
        private ParticleSystem shards;
        private Mesh shardMesh;
        private Material shardMaterial;
        private GUIStyle numberStyle;

        public bool IsFlashing => flashEnabled && flashRemaining > 0f;
        public int ActiveNumberCount => numbers.Count;
        public int LastDamage { get; private set; }
        public int ActiveShardCount => shards == null ? 0 : shards.particleCount;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            previousHp = health.CurrentHealth;
            LastDamage = 0;
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

            // 공용 Health 계약은 유지합니다. 같은 프레임의 피해는 실제 감소량 합계로 표시합니다.
            int damage = previousHp - health.CurrentHealth;
            previousHp = health.CurrentHealth;
            if (damage <= 0)
                return;

            LastDamage = damage;
            if (flashEnabled)
            {
                flashRemaining = FlashDuration;
                HitCameraImpulse.Pulse();
            }
            if (numbersEnabled)
            {
                if (numbers.Count == 8)
                    numbers.RemoveAt(0);
                numbers.Add(new DamageNumber { amount = damage,
                    position = transform.position + Vector3.up * 2.9f });
            }
            if (shardsEnabled)
                EmitShards();
        }

        private void EmitShards()
        {
            if (shards == null)
                CreateShards();

            shards.transform.position = transform.position + Vector3.up;
            shards.Play();
            for (int i = 0; i < 12; i++)
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
                Rect rect = new Rect(screen.x - 50f, Screen.height - screen.y - 20f, 100f, 40f);
                string text = number.amount.ToString();
                float alpha = 1f - number.age / NumberDuration;
                numberStyle.normal.textColor = new Color(0f, 0f, 0f, alpha);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, numberStyle);
                numberStyle.normal.textColor = new Color(1f, 0.85f, 0.25f, alpha);
                GUI.Label(rect, text, numberStyle);
            }
        }

        private void OnDisable()
        {
            flashRemaining = 0f;
            numbers.Clear();
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
