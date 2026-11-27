using System.Collections.Generic;
using Nomad.Equipment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(390)]
    public sealed class BossVisualStyle : MonoBehaviour
    {
        private DragonBossCombat boss;
        private Renderer[] bodies;
        private readonly List<Renderer> outlines = new();
        private readonly List<Material> materials = new();
        private readonly List<Mesh> meshes = new();
        private MaterialPropertyBlock properties;
        private BossParrySignal signal;
        private ParticleSystem eyes;
        private Light flash;
        private Transform eyeBone;
        // 원본 용의 실제 눈 표면을 위턱 뼈 좌표로 측정한 소켓입니다.
        private static readonly Vector3[] EyeSockets = { new Vector3(-0.02833f, 0.06875f, -0.00212f),
            new Vector3(-0.00463f, 0.07017f, -0.02426f) };
        public bool Hovered { get; private set; }
        public bool EyesBurning => boss != null && boss.Enraged && boss.Introduced;

        public void Configure(DragonBossCombat owner)
        {
            boss = owner;
            signal = GetComponent<BossParrySignal>();
            eyeBone = boss.Mouth.parent;
            properties = new MaterialPropertyBlock();
            bodies = transform.Find("Body").GetComponentsInChildren<Renderer>(true);
            Shader cel = Resources.Load<Shader>("BossCel");
            Shader edge = Resources.Load<Shader>("BossOutline");
            foreach (Renderer source in bodies)
            {
                if (source is not SkinnedMeshRenderer && source is not MeshRenderer) continue;
                Material[] replacement = new Material[source.sharedMaterials.Length];
                for (int index = 0; index < replacement.Length; index++)
                {
                    Material original = source.sharedMaterials[index];
                    var toon = new Material(cel) { mainTexture = original.mainTexture,
                        color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white };
                    materials.Add(toon);
                    replacement[index] = toon;
                }

                source.sharedMaterials = replacement;
                var root = new GameObject("Boss ink outline");
                root.transform.SetParent(source.transform, false);
                Renderer outline;
                if (source is SkinnedMeshRenderer skin)
                {
                    var clone = root.AddComponent<SkinnedMeshRenderer>();
                    clone.sharedMesh = SmoothOutline(skin.sharedMesh);
                    clone.bones = skin.bones;
                    clone.rootBone = skin.rootBone;
                    clone.localBounds = skin.localBounds;
                    outline = clone;
                }
                else
                {
                    root.AddComponent<MeshFilter>().sharedMesh = SmoothOutline(source.GetComponent<MeshFilter>().sharedMesh);
                    outline = root.AddComponent<MeshRenderer>();
                }

                var ink = new Material(edge);
                materials.Add(ink);
                var slots = new Material[source.sharedMaterials.Length];
                System.Array.Fill(slots, ink);
                outline.sharedMaterials = slots;
                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outlines.Add(outline);
            }

            eyes = WeaponVfx.CreateParticles(transform, "Enraged burning eyes", WeaponVfx.Glow);
            var main = eyes.main;
            main.startLifetime = 0.22f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startColor = new Color(3, 0.015f, 0.005f);
            var noise = eyes.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 4;
            noise.scrollSpeed = 3;
            WeaponVfx.Shrink(eyes);
            WeaponVfx.Fade(eyes, 0);

            flash = new GameObject("Yellow parry flash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.type = LightType.Point;
            flash.color = new Color(1, 0.85f, 0.05f);
            flash.range = 12;
            flash.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (boss == null) return;
            bool alive = boss.Introduced && boss.Phase != DragonPhase.Dead;
            Camera camera = Camera.main;
            Hovered = alive && camera != null && Mouse.current != null && GetComponent<Collider>().Raycast(
                camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out _, 2000);
            foreach (Renderer outline in outlines)
            {
                outline.GetPropertyBlock(properties);
                properties.SetColor("_OutlineColor", signal.Flashing ? new Color(2.5f, 1.8f, 0.35f) : Hovered ? new Color(1, 0.025f, 0.015f) : new Color(0.035f, 0.025f, 0.03f));
                properties.SetFloat("_Width", signal.Flashing ? 0.08f : Hovered ? 0.12f : 0.045f);
                outline.SetPropertyBlock(properties);
            }
            bool yellow = alive && signal.Flashing;
            foreach (Renderer body in bodies)
            {
                body.GetPropertyBlock(properties);
                properties.SetColor("_SignalColor", Color.clear);
                properties.SetFloat("_Enraged", EyesBurning ? 1 : 0);
                body.SetPropertyBlock(properties);
            }
            flash.transform.position = transform.position + Vector3.up * 2;
            flash.intensity = yellow ? 5 : 0;
            if (!EyesBurning) { eyes.Clear(); return; }
            foreach (Vector3 socket in EyeSockets)
            {
                Vector3 point = eyeBone.TransformPoint(socket);
                float side = Mathf.Sign(Vector3.Dot(point - eyeBone.position, transform.right));
                point += (transform.right * side + transform.forward).normalized * 0.45f;
                eyes.Emit(new ParticleSystem.EmitParams { position = point, velocity = Vector3.up * 0.8f }, 2);
            }
        }

        private Mesh SmoothOutline(Mesh source)
        {
            Mesh mesh = Instantiate(source);
            mesh.name = "Welded boss outline normals";
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var sums = new Dictionary<Vector3, Vector3>();
            for (int index = 0; index < vertices.Length; index++)
            {
                sums.TryGetValue(vertices[index], out Vector3 sum);
                sums[vertices[index]] = sum + normals[index];
            }
            for (int index = 0; index < vertices.Length; index++) normals[index] = sums[vertices[index]].normalized;
            mesh.normals = normals;
            meshes.Add(mesh);
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
