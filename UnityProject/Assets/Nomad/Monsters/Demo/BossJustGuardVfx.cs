using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossJustGuardVfx : MonoBehaviour
    {
        private readonly LineRenderer[] rays = new LineRenderer[3];
        private float started;
        private bool success;
        private static readonly Color Gold = new Color(2.5f, 1.65f, 0.35f);

        public static void Cue(Transform boss) => Create((boss.Find("Body") != null ? boss.Find("Body").position : boss.position) + Vector3.up * 2, boss.rotation, false);
        public static void Success(Transform player, Vector3 point)
        {
            Create(point, player.rotation, true);
            ParrySuccessBanner.Show(player);
        }

        private static void Create(Vector3 point, Quaternion facing, bool guarded)
        {
            var root = new GameObject(guarded ? "Just guard impact" : "Just guard golden tell");
            root.transform.SetPositionAndRotation(point, facing);
            var effect = root.AddComponent<BossJustGuardVfx>();
            effect.started = Time.time;
            effect.success = guarded;
            for (int index = 0; index < effect.rays.Length; index++)
            {
                var ray = new GameObject("Golden light streak").AddComponent<LineRenderer>();
                ray.transform.SetParent(root.transform, false);
                ray.useWorldSpace = false;
                ray.sharedMaterial = WeaponVfx.Beam;
                ray.positionCount = 3;
                Vector3 axis = index == 0 ? Vector3.up : index == 1 ? Vector3.right : (Vector3.right + Vector3.up).normalized;
                float length = guarded ? 2.6f : 2.7f;
                ray.SetPositions(new[] { -axis * length, Vector3.zero, axis * length });
                ray.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, guarded ? 0.32f : 0.12f), new Keyframe(1, 0));
                effect.rays[index] = ray;
            }

            ParticleSystem sparks = WeaponVfx.CreateParticles(root.transform, "Golden guard sparks", WeaponVfx.Glow);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.startColor = Gold;
            WeaponVfx.Fade(sparks, 0);
            for (int index = 0; index < (guarded ? 84 : 24); index++)
                sparks.Emit(new ParticleSystem.EmitParams { position = point, velocity = Random.onUnitSphere * (guarded ? 6 : 4) }, 1);
            Destroy(root, 0.45f);
        }

        private void LateUpdate()
        {
            if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
            float alpha = Mathf.Clamp01(1 - (Time.time - started) / (success ? 0.3f : 0.22f));
            foreach (LineRenderer ray in rays)
                if (ray != null) ray.startColor = ray.endColor = new Color(Gold.r, Gold.g, Gold.b, alpha);
        }
    }
}
