using System;
using System.IO;
using System.Linq;
using Nomad.Monsters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class RankedMonsterSetup
    {
        private const string Output = "Assets/ThirdParty/Nomad/Monsters/Candidates";
        private const string Dragon = "Assets/ThirdParty/OpenGameArt/Dragon";
        private const string Golem = "Assets/ThirdParty/Standout7/Golems";
        private const string Catalog = "Assets/ThirdParty/Nomad/Monsters/Skeletons/SkeletonAppearances.asset";

        [MenuItem("Nomad/Gameplay/Apply Three Monster Ranks")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            Import(Golem + "/Iron_Golem.fbx", false);
            Import(Dragon + "/dragon.dae", false);
            string controllerPath = Output + "/Dragon.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState old in machine.states) machine.RemoveState(old.state);
            string[] files = { "idle", "walk", "attack", "die" };
            string[] states = { "Idle_A", "Walking_A", "Attack_A", "Death_A" };
            for (int i = 0; i < files.Length; i++)
            {
                string path = $"{Dragon}/dragon_{files[i]}.dae";
                Import(path, true);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.loopTime = i < 2;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                AnimationClip animation = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
                if (animation == null) throw new InvalidOperationException("Missing Dragon clip: " + path);
                animation = BindDragonClip(animation, files[i]);
                AnimatorState state = machine.AddState(states[i]);
                state.motion = animation;
                if (i == 0) machine.defaultState = state;
                if (i == 1) machine.AddState("Running_A").motion = animation;
                Debug.Log($"DRAGON_CLIP {files[i]} duration={animation.length} bindings={AnimationUtility.GetCurveBindings(animation).Length}");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(Catalog);
            catalog.elite = Prefab("Iron Golem", Golem + "/Iron_Golem.fbx", Golem + "/Texture.png", 3.5f, null);
            catalog.boss = Prefab("Dragon", Dragon + "/dragon.dae", Dragon + "/dragon.png", 5f, controller);
            RangedAssetSetup.Apply(catalog);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            GameplayIntegrationSetup.Apply();
            MonsterDebugSetup.Apply();
            CapturePreview();
            Debug.Log("RANKED_MONSTER_SETUP_PASS");
        }

        private static void Import(string path, bool animation)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("ModelImporter unavailable: " + path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = animation;
            importer.isReadable = path.EndsWith("/dragon.dae");
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static AnimationClip BindDragonClip(AnimationClip source, string label)
        {
            // 애니메이션 전용 DAE는 Armature 루트를 생략하지만 모델 DAE에는 남아 있습니다.
            // 원본을 바꾸지 않고 파생 클립의 경로만 실제 모델 계층에 맞춥니다.
            AnimationClip copy = Object.Instantiate(source);
            copy.name = "Dragon_" + label;
            foreach (EditorCurveBinding original in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, original);
                AnimationUtility.SetEditorCurve(copy, original, null);
                EditorCurveBinding binding = original;
                binding.path = string.IsNullOrEmpty(original.path) ? "Armature" : "Armature/" + original.path;
                AnimationUtility.SetEditorCurve(copy, binding, curve);
            }
            string path = Output + "/Dragon_" + label + ".anim";
            copy.EnsureQuaternionContinuity();
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (saved == null) AssetDatabase.CreateAsset(copy, path);
            else
            {
                EditorUtility.CopySerialized(copy, saved);
                Object.DestroyImmediate(copy);
                EditorUtility.SetDirty(saved);
            }
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static GameObject Prefab(string label, string path, string texture, float height, AnimatorController controller)
        {
            string materialPath = Output + "/" + label + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            var wrapper = new GameObject(label);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), wrapper.transform);
            Animator animator = model.GetComponent<Animator>();
            if (controller != null)
            {
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                controller.animationClips.First(clip => clip.name.Contains("idle")).SampleAnimation(model, 0.2f);
            }
            Renderer[] bodies = model.GetComponentsInChildren<Renderer>();
            if (label == "Dragon")
                foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    skin.sharedMesh = DragonLowPolyMesh.Create(skin.sharedMesh, Output + "/DragonLowPoly.asset");
            if (bodies.Length == 0) throw new InvalidOperationException("Empty model: " + path);
            Bounds bounds = VisualBounds(bodies[0]);
            foreach (Renderer body in bodies)
            {
                bounds.Encapsulate(VisualBounds(body));
                body.sharedMaterials = Enumerable.Repeat(material, body.sharedMaterials.Length).ToArray();
                if (body is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            if (bounds.size.y < 0.01f) throw new InvalidOperationException("Invalid model bounds");
            model.transform.localPosition -= Vector3.up * bounds.min.y;
            wrapper.transform.localScale = Vector3.one * (height / bounds.size.y);
            string prefabPath = Output + "/" + label + ".prefab";
            GameObject result = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            Object.DestroyImmediate(wrapper);
            EditorUtility.SetDirty(material);
            return result;
        }

        private static Bounds VisualBounds(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skin)) return renderer.bounds;
            var mesh = new Mesh();
            skin.BakeMesh(mesh);
            Vector3[] vertices = mesh.vertices;
            Bounds bounds = new Bounds(skin.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(skin.transform.TransformPoint(vertex));
            Object.DestroyImmediate(mesh);
            return bounds;
        }

        private static void CapturePreview()
        {
            // 현재 씬은 건드리지 않고 프리뷰 전용 씬에서 세 등급의 재질/크기를 확인합니다.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(Catalog);
            var models = new[] { catalog.normal[0], catalog.elite, catalog.boss };
            for (int i = 0; i < models.Length; i++)
            {
                GameObject actor = Object.Instantiate(models[i]);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, scene);
                actor.transform.position = new Vector3((i - 1) * 5f, 0f, 0f);
                actor.transform.rotation = Quaternion.Euler(0f, i == 2 ? 225f : 180f, 0f);
                if (i == 1)
                    foreach (Transform bone in actor.GetComponentsInChildren<Transform>())
                        if (bone.name == "upper_arm.L" || bone.name == "upper_arm.R")
                            bone.localRotation *= Quaternion.Euler(0f, 0f, bone.name.EndsWith(".L") ? 65f : -65f);
                Animator animator = actor.GetComponentInChildren<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    AnimationClip idle = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("Idle") || c.name.Contains("idle"));
                    if (idle != null) idle.SampleAnimation(animator.gameObject, 0.2f);
                }
            }
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, 5f, -14f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
            var light = new GameObject("Preview light").AddComponent<Light>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            light.intensity = 2f;
            var rt = new RenderTexture(1200, 650, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(1200, 650, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 650), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/three-monster-ranks.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
