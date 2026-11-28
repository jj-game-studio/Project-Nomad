using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossEnrageTransition
    {
        private Transform[] poseBones;
        private readonly Dictionary<Transform, Quaternion> poseRest = new();

        private void ConfigurePose()
        {
            poseBones = System.Array.FindAll(GetComponentsInChildren<Transform>(), bone => bone.name == "neck1" ||
                bone.name == "jaw_lower" || bone.name.Contains("wing_upper"));
        }

        private void RestorePose()
        {
            foreach (var pair in poseRest) if (pair.Key != null) pair.Key.localRotation = pair.Value;
            poseRest.Clear();
        }

        private void LateUpdate()
        {
            if (!Started || health.IsDead) return;
            if (!Completed)
            {
                float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - since) / 1.1f)) * Mathf.Clamp01((1 - Progress) * 4);
                foreach (Transform bone in poseBones)
                {
                    poseRest[bone] = bone.localRotation;
                    float angle = bone.name == "neck1" ? -38 : bone.name == "jaw_lower" ? 30 : bone.name.EndsWith(".L") ? -28 : 28;
                    bone.rotation = Quaternion.AngleAxis(angle * lift, bone.name.Contains("wing") ? transform.forward : transform.right) * bone.rotation;
                }
            }
            if (Random.value > Time.deltaTime * (Completed ? 18 : 65)) return;
            Vector3 location = transform.position + transform.right * Random.Range(-1.3f, 1.3f) +
                transform.forward * Random.Range(-1.5f, 0.7f) + Vector3.up * Random.Range(0.8f, 2.6f);
            aura.Emit(new ParticleSystem.EmitParams { position = location, velocity = Vector3.up * Random.Range(0.8f, 2.2f) }, 1);
        }

        private void Roar()
        {
            const int rate = 22050;
            float[] samples = new float[rate * 2];
            var random = new System.Random(214);
            float noise = 0;
            for (int index = 0; index < samples.Length; index++)
            {
                float t = index / (float)rate;
                float envelope = Mathf.Min(1, t * 9) * Mathf.Clamp01((2 - t) / 0.7f);
                noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2 - 1, 0.08f);
                samples[index] = (noise * 0.55f + Mathf.Sin(t * (70 - 9 * t) * Mathf.PI * 2) * 0.22f +
                    Mathf.Sin(t * 137 * Mathf.PI * 2) * 0.08f) * envelope;
            }
            roar = AudioClip.Create("Dragon phase two roar", samples.Length, 1, rate, false);
            roar.SetData(samples, 0);
            var source = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            source.PlayOneShot(roar, 0.65f);
        }
    }
}
