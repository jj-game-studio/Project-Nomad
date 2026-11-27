using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossStoneDebris : MonoBehaviour
    {
        private Mesh shard;
        private Material stone, dustMaterial;

        public static void Create(Transform parent, Vector3 point)
        {
            var root = new GameObject("Stone fracture debris and dust");
            root.transform.SetParent(parent, true);
            root.transform.position = point;
            root.AddComponent<BossStoneDebris>().Build();
            Destroy(root, 1.8f);
        }

        private void Build()
        {
            stone = new Material(Resources.Load<Shader>("BossStone")) { color = new Color(0.38f, 0.34f, 0.3f) };
            shard = new Mesh { name = "Jagged stone fragment", vertices = new[] { Vector3.up, Vector3.down * 0.7f,
                Vector3.left * 0.8f, Vector3.right, Vector3.forward * 0.7f, Vector3.back },
                triangles = new[] { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 } };
            shard.RecalculateNormals();
            for (int index = 0; index < 12; index++)
            {
                var piece = new GameObject("Basalt fragment", typeof(MeshFilter), typeof(MeshRenderer), typeof(Rigidbody));
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = Random.insideUnitSphere * 0.3f;
                piece.transform.localScale = Vector3.one * Random.Range(0.15f, 0.38f);
                piece.GetComponent<MeshFilter>().sharedMesh = shard;
                piece.GetComponent<MeshRenderer>().sharedMaterial = stone;
                var body = piece.GetComponent<Rigidbody>();
                body.linearVelocity = Random.insideUnitSphere * 3 + Vector3.up * Random.Range(2, 4);
                body.angularVelocity = Random.insideUnitSphere * 7;
            }
            dustMaterial = new Material(WeaponVfx.Glow);
            dustMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            dustMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            var dust = WeaponVfx.CreateParticles(transform, "Non glowing stone dust", dustMaterial);
            var main = dust.main;
            main.startColor = new Color(0.45f, 0.39f, 0.32f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            WeaponVfx.Fade(dust, 0);
            for (int index = 0; index < 24; index++) dust.Emit(new ParticleSystem.EmitParams {
                position = transform.position + Random.insideUnitSphere * 0.5f, velocity = Random.insideUnitSphere * 1.5f + Vector3.up }, 1);
        }

        private void OnDestroy()
        {
            if (shard != null) Destroy(shard);
            if (stone != null) Destroy(stone);
            if (dustMaterial != null) Destroy(dustMaterial);
        }
    }
}
