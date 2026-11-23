using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Village
{
    // 기도 직후 플레이어 발밑에서 퍼지는 빛과 위로 오르는 빛 입자입니다.
    // 플레이어 자식으로 두고 로컬 공간에서 시뮬레이션해 이동 중에도 함께 따라갑니다.
    public static class PrayerVfx
    {
        public const string RootName = "Prayer VFX";
        public const float Duration = 1.4f;

        private static readonly Color Core = new Color(1f, 0.95f, 0.7f);
        private static readonly Color Glow = new Color(1f, 0.75f, 0.3f);

        public static GameObject Play(Transform player)
        {
            // 연속 기도 시 이전 효과를 지우고 새로 재생합니다.
            Transform previous = player.Find(RootName);
            if (previous != null)
                Object.Destroy(previous.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(player, false);
            root.transform.localPosition = Vector3.zero;
            const ParticleSystemSimulationSpace local = ParticleSystemSimulationSpace.Local;

            // 발밑에서 퍼지는 원형 빛입니다.
            ParticleSystem ring = WeaponVfx.CreateParticles(root.transform, "Ring", WeaponVfx.Glow, local);
            ring.transform.localPosition = Vector3.up * 0.1f;
            ParticleSystem.MainModule main = ring.main;
            main.startLifetime = 0.7f;
            main.startSize = 3.2f;
            main.startColor = Glow;
            ParticleSystem.SizeOverLifetimeModule grow = ring.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
            WeaponVfx.Fade(ring, 0.2f);
            ring.Emit(1);

            // 몸을 감싸는 짧은 섬광입니다.
            ParticleSystem flash = WeaponVfx.CreateParticles(root.transform, "Flash", WeaponVfx.Glow, local);
            flash.transform.localPosition = Vector3.up;
            main = flash.main;
            main.startLifetime = 0.35f;
            main.startSize = 2.2f;
            main.startColor = Core;
            WeaponVfx.Fade(flash, 0.1f);
            flash.Emit(1);

            // 몸 주변에서 위로 떠오르는 빛 입자입니다.
            ParticleSystem motes = WeaponVfx.CreateParticles(root.transform, "Motes", WeaponVfx.Glow, local);
            main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(Core, Glow);
            main.gravityModifier = -0.25f;
            ParticleSystem.ShapeModule shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(90, 0, 0);
            ParticleSystem.VelocityOverLifetimeModule rise = motes.velocityOverLifetime;
            rise.enabled = true;
            rise.space = local;
            rise.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rise.y = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            rise.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            WeaponVfx.Shrink(motes);
            motes.Emit(28);

            Object.Destroy(root, Duration);
            return root;
        }
    }
}
