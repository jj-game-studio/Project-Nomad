using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        private Material lavaMaterial;

        private void CreateLavaSurface()
        {
            lavaMaterial = new Material(Resources.Load<Shader>("BossLavaPool"));
            lavaMaterial.SetVector("_Center", transform.position);
            lavaMaterial.SetFloat("_Radius", Radius);
            lavaMaterial.SetFloat("_ShapeSeed", ShapeSeed);
            var shape = fire.shape;
            shape.enabled = false;
            var emission = fire.emission;
            emission.enabled = false;
            fill.GetComponent<Renderer>().sharedMaterial = lavaMaterial;
            fill.GetComponent<Renderer>().SetPropertyBlock(null);
            boundary.startColor = boundary.endColor = new Color(1.5f, 0.3f, 0.015f);
            boundary.widthMultiplier = 0.12f;
            var main = fire.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            var velocity = fire.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = 0.8f;
        }
    }
}
