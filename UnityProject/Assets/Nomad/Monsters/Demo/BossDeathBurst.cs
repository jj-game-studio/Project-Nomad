using Nomad.Equipment;
using Nomad.Monsters;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public static class BossDeathBurst
    {
        public static void Create(Vector3 point)
        {
            WeaponVfx.Burst(point, new WeaponVfxStyle(new Color(3,1.5f,0.2f), new Color(1.5f,0.15f,0.01f), 3.5f), 2);
            BossStoneDebris.Create(null, point);
            var root = new GameObject("Boss final ash explosion"); root.transform.position = point;
            var ash = WeaponVfx.CreateParticles(root.transform, "Ash and dying embers", BossFlameVfx.Smoke);
            var main = ash.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f,1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f,1.5f); main.startColor = new Color(0.15f,0.09f,0.06f,0.6f);
            var emission = ash.emission; emission.enabled = false;
            for (int i=0;i<80;i++) ash.Emit(new ParticleSystem.EmitParams { position=point,velocity=Random.onUnitSphere*Random.Range(3,9)+Vector3.up*3 },1);
            HitCameraImpulse.Pulse(0.35f,0.3f);
            Object.Destroy(root,2);
        }
    }
}
