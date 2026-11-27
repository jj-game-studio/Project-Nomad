using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(500)]
    public sealed class BossStunDisplay : MonoBehaviour
    {
        public bool Visible => indicator != null && indicator.activeInHierarchy;
        private DragonBossCombat boss;
        private Transform mouth;
        private GameObject indicator;
        private readonly Transform[] stars = new Transform[5];
        private Mesh starMesh;
        private ParticleSystem gathering;

        public void Configure(DragonBossCombat owner, Transform source)
        {
            boss = owner;
            mouth = source;
            indicator = new GameObject("Stun indicator");
            indicator.transform.SetParent(transform, false);

            var vertices = new Vector3[11];
            var triangles = new int[30];
            for (int index = 0; index < 10; index++)
            {
                float angle = (90 + index * 36) * Mathf.Deg2Rad;
                float radius = index % 2 == 0 ? 1 : 0.45f;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = (index + 1) % 10 + 1;
            }
            starMesh = new Mesh { name = "Stun star", vertices = vertices, triangles = triangles };
            starMesh.RecalculateNormals();
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(1.8f, 1.4f, 0.1f));
            for (int index = 0; index < stars.Length; index++)
            {
                var go = new GameObject("Orbiting stun star");
                stars[index] = go.transform;
                stars[index].SetParent(indicator.transform, false);
                stars[index].localScale = Vector3.one * 0.25f;
                go.AddComponent<MeshFilter>().sharedMesh = starMesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = WeaponVfx.Plain;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.SetPropertyBlock(tint);
            }
            indicator.SetActive(false);

            gathering = WeaponVfx.CreateParticles(mouth, "Mouth fire preparation", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            var main = gathering.main;
            main.startLifetime = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = 0.1f;
            main.startColor = new Color(1, 0.35f, 0.02f);
            var shape = gathering.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var emission = gathering.emission;
            emission.rateOverTime = 40;
            WeaponVfx.Fade(gathering, 0);
        }

        private void LateUpdate()
        {
            if (boss == null) return;
            bool stunned = boss.Phase == DragonPhase.Stunned;
            indicator.SetActive(stunned);
            var emission = gathering.emission;
            bool preparing = boss.Phase == DragonPhase.Windup &&
                (boss.Pattern == DragonPattern.Breath || boss.Pattern == DragonPattern.TripleFireball);
            emission.enabled = preparing;
            if (!preparing) gathering.Clear();
            if (!stunned) return;

            Vector3 center = mouth.position + Vector3.up * 1.2f;
            for (int index = 0; index < stars.Length; index++)
            {
                float angle = Time.time * 3 + index * Mathf.PI * 2 / stars.Length;
                stars[index].position = center + new Vector3(Mathf.Cos(angle) * 0.85f, Mathf.Sin(angle * 2) * 0.1f, Mathf.Sin(angle) * 0.85f);
            }
            FaceCamera(Camera.main);
        }

        public void FaceCamera(Camera camera)
        {
            if (camera == null) return;
            foreach (Transform star in stars) star.rotation = camera.transform.rotation;
        }

        private void OnDestroy() { if (starMesh != null) Destroy(starMesh); }
    }
}
