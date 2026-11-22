using Nomad.Inventory;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Equipment
{
    // 무기별 VFX 색과 크기입니다. Slash는 근접 공격에 베기 궤적 VFX를 쓸지 정합니다.
    public readonly struct WeaponVfxStyle
    {
        public readonly Color Core;
        public readonly Color Glow;
        public readonly float Size;
        public readonly bool Slash;

        public WeaponVfxStyle(Color core, Color glow, float size, bool slash = false)
        {
            Core = core;
            Glow = glow;
            Size = size;
            Slash = slash;
        }
    }

    // 무기 VFX가 함께 쓰는 재질·텍스처·명중 폭발입니다.
    // 재질은 플레이어 빌드에서도 셰이더가 빠지지 않도록 Resources 에셋을 우선 사용합니다.
    public static class WeaponVfx
    {
        public const string MaterialResource = "WeaponVfxAdditive";
        public const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

        private static Material plain;
        private static Material glow;
        private static Material beam;

        public static Material Plain => plain != null ? plain : plain = CreateMaterial(null);
        public static Material Glow => glow != null ? glow : glow = CreateMaterial(CreateTexture("Weapon VFX Glow", true));
        public static Material Beam => beam != null ? beam : beam = CreateMaterial(CreateTexture("Weapon VFX Beam", false));

        public static bool TryGetStyle(ItemDefinition weapon, out WeaponVfxStyle style)
        {
            style = default;
            if (weapon == null)
                return false;

            switch (weapon.ItemId)
            {
                case "weapon-apprentice-staff":
                    style = new WeaponVfxStyle(new Color(0.8f, 0.95f, 1f), new Color(0.25f, 0.55f, 1f), 0.8f);
                    return true;
                case "weapon-crystal-staff":
                    style = new WeaponVfxStyle(new Color(0.85f, 1f, 1f), new Color(0.2f, 0.95f, 0.85f), 1f);
                    return true;
                case "weapon-archmage-staff":
                    style = new WeaponVfxStyle(new Color(1f, 0.85f, 1f), new Color(0.7f, 0.25f, 1f), 1.3f);
                    return true;
                case "weapon-greatsword":
                    style = new WeaponVfxStyle(new Color(1f, 0.97f, 0.88f), new Color(1f, 0.55f, 0.15f), 1f, true);
                    return true;
            }

            // CSV에 새 지팡이를 추가해도 기본 마법 VFX가 나가도록 합니다.
            if (weapon.AttackMode != WeaponAttackMode.Staff)
                return false;
            style = new WeaponVfxStyle(new Color(0.8f, 0.95f, 1f), new Color(0.25f, 0.55f, 1f), 1f);
            return true;
        }

        // 마법 명중 지점의 섬광·충격파·불꽃입니다. 루트를 1초 뒤 지우면 하위 입자도 함께 정리됩니다.
        public static GameObject Burst(Vector3 point, WeaponVfxStyle style, float scale = 1f)
        {
            var root = new GameObject("Magic Burst VFX");
            root.transform.position = point;
            float size = style.Size * scale;

            ParticleSystem flash = CreateParticles(root.transform, "Flash", Glow);
            ParticleSystem.MainModule main = flash.main;
            main.startLifetime = 0.14f;
            main.startSize = 1.7f * size;
            main.startColor = style.Core;
            Fade(flash, 0.2f);
            flash.Emit(1);

            ParticleSystem ring = CreateParticles(root.transform, "Shockwave", Glow);
            main = ring.main;
            main.startLifetime = 0.3f;
            main.startSize = 2.6f * size;
            main.startColor = style.Glow;
            ParticleSystem.SizeOverLifetimeModule grow = ring.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
            Fade(ring, 0f);
            ring.Emit(1);

            ParticleSystem sparks = CreateParticles(root.transform, "Sparks", Glow);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 4.5f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * size, 0.14f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(style.Core, style.Glow);
            main.gravityModifier = 0.5f;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            Shrink(sparks);
            sparks.Emit(Mathf.RoundToInt(16 * scale) + 4);

            Object.Destroy(root, 1f);
            return root;
        }

        // 방출은 꺼 둔 채 재생하는 입자 시스템입니다. 호출자가 Emit하거나 방출 모듈을 켭니다.
        // 정리는 소유한 루트 오브젝트를 지울 때 함께 이뤄집니다.
        public static ParticleSystem CreateParticles(Transform parent, string name, Material material,
            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startSpeed = 0f;
            main.simulationSpace = space;
            main.maxParticles = 256;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            return particles;
        }

        // 수명 동안 알파를 줄입니다. hold 구간까지는 처음 밝기를 유지합니다.
        public static void Fade(ParticleSystem particles, float hold)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, hold), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }

        // 수명 동안 크기와 알파를 함께 줄입니다.
        public static void Shrink(ParticleSystem particles)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            Fade(particles, 0.3f);
        }

        // 가산 혼합 반투명 설정입니다. 편집기 저작 스크립트도 같은 설정으로 Resources 재질을 만듭니다.
        public static void ConfigureAdditive(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetColor("_BaseColor", Color.white);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Material CreateMaterial(Texture texture)
        {
            Material source = Resources.Load<Material>(MaterialResource);
            Material material;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                material = new Material(Shader.Find(ShaderName));
                ConfigureAdditive(material);
            }

            material.name = texture != null ? texture.name : "Weapon VFX Plain";
            material.hideFlags = HideFlags.DontSave;
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            return material;
        }

        // round는 가운데가 밝은 원형, 아니면 폭 방향만 부드러운 띠(꼬리용) 텍스처입니다.
        private static Texture2D CreateTexture(string name, bool round)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float distance = round ? Mathf.Sqrt(u * u + v * v) : Mathf.Abs(v);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
