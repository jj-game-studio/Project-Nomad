using System;
using System.IO;
using System.Linq;
using Nomad.Monsters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class SkeletonAppearanceSetup
    {
        private const string Source = "Assets/ThirdParty/KayKit/Skeletons";
        private const string Output = "Assets/ThirdParty/Nomad/Monsters/Skeletons";
        private static readonly string[] Characters = { "Warrior", "Mage", "Minion", "Rogue" };
        private const string GalleryKey = "Nomad.SkeletonGallery";

        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            foreach (string character in Characters)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath($"{Source}/Models/Skeleton_{character}.fbx");
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath($"{Source}/Models/Skeleton_Warrior.fbx").OfType<Avatar>().First();
            var clips = new System.Collections.Generic.List<AnimationClip>();
            foreach (string file in new[] { "Rig_Medium_General", "Rig_Medium_MovementBasic" })
            {
                string path = $"{Source}/Animations/{file}.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                ModelImporterClipAnimation[] settings = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in settings)
                    clip.loopTime = clip.name.Contains("Idle") || clip.name.Contains("Walking") || clip.name.Contains("Running");
                importer.clipAnimations = settings;
                importer.SaveAndReimport();
                clips.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__")));
            }
            Debug.Log("SKELETON_CLIPS: " + string.Join(",", clips.Select(clip => clip.name)));
            string controllerPath = Output + "/Skeleton.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState old in machine.states) machine.RemoveState(old.state);
            foreach (string stateName in new[] { "Idle_A", "Walking_A", "Running_A", "Hit_A", "Death_A" })
            {
                AnimationClip clip = clips.FirstOrDefault(value => value.name == stateName || value.name.EndsWith("|" + stateName));
                if (clip == null) throw new InvalidOperationException("Missing clip: " + stateName);
                AnimatorState state = machine.AddState(stateName);
                state.motion = clip;
                if (stateName == "Idle_A") machine.defaultState = state;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Skeleton.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, Output + "/Skeleton.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/skeleton_texture.png"));
            material.SetColor("_BaseColor", Color.white);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(Output + "/SkeletonAppearances.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MonsterAppearanceSet>();
                AssetDatabase.CreateAsset(catalog, Output + "/SkeletonAppearances.asset");
            }
            catalog.normal = new GameObject[Characters.Length];
            for (int index = 0; index < Characters.Length; index++)
            {
                var wrapper = new GameObject("Skeleton_" + Characters[index]);
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{Source}/Models/Skeleton_{Characters[index]}.fbx"), wrapper.transform);
                Renderer[] bodies = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = bodies[0].bounds;
                foreach (Renderer body in bodies)
                {
                    bounds.Encapsulate(body.bounds);
                    body.sharedMaterials = Enumerable.Repeat(material, body.sharedMaterials.Length).ToArray();
                    if (body is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                if (bounds.size.y < 0.1f) throw new InvalidOperationException("Invalid model height");
                model.transform.localPosition -= Vector3.up * bounds.min.y;
                wrapper.transform.localScale = Vector3.one * (1.8f / bounds.size.y);
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                string path = $"{Output}/Skeleton_{Characters[index]}.prefab";
                catalog.normal[index] = PrefabUtility.SaveAsPrefabAsset(wrapper, path);
                Object.DestroyImmediate(wrapper);
            }
            // 없는 EXTRA 모델을 무료 모델로 대체하지 않습니다.
            catalog.elite = null;
            catalog.boss = null;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            GameplayIntegrationSetup.Apply();
            ConfigureTestScene("Assets/Nomad/Monsters/MonsterCombat.unity", catalog);
            ConfigureTestScene("Assets/Nomad/Monsters/MonsterElite.unity", catalog);
            Debug.Log("SKELETON_APPEARANCE_SETUP_PASS: four normal variants; elite/boss awaiting EXTRA models");
        }

        private static void ConfigureTestScene(string path, MonsterAppearanceSet catalog)
        {
            EditorSceneManager.OpenScene(path);
            catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(Output + "/SkeletonAppearances.asset");
            var panel = Object.FindFirstObjectByType<Nomad.Monsters.Demo.MonsterTestPanel>();
            var data = new SerializedObject(panel);
            data.FindProperty("appearances").objectReferenceValue = catalog;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (path.EndsWith("MonsterCombat.unity"))
            {
                var monster = Object.FindFirstObjectByType<MonsterMeleeAttack>();
                Transform old = monster.transform.Find("Body");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(catalog.normal[0], monster.transform);
                body.name = "Body";
                var demo = monster.GetComponentInChildren<Nomad.Monsters.Demo.MonsterCombatDemo>();
                var visual = new SerializedObject(demo);
                visual.FindProperty("monsterBody").objectReferenceValue = body.GetComponentInChildren<Renderer>();
                visual.ApplyModifiedPropertiesWithoutUndo();
                if (monster.GetComponent<MonsterAnimationDriver>() == null)
                    monster.gameObject.AddComponent<MonsterAnimationDriver>();
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static void OpenGalleryForPlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(Output + "/SkeletonAppearances.asset");
            for (int index = 0; index < catalog.normal.Length; index++)
            {
                GameObject actor = (GameObject)PrefabUtility.InstantiatePrefab(catalog.normal[index]);
                actor.transform.position = new Vector3((index - 1.5f) * 2.2f, 0f, 0f);
                actor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            Camera camera = new GameObject("Gallery Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2.2f, -8f);
            camera.transform.LookAt(new Vector3(0f, 0.85f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            Light light = new GameObject("Gallery Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
            light.intensity = 1.4f;
            RenderSettings.ambientLight = Color.gray;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Output + "/SkeletonGallery.unity");
            SessionState.SetBool(GalleryKey, true);
            ResumeGallery();
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.EnterPlaymode();
            };
        }

        [InitializeOnLoadMethod]
        private static void ResumeGallery()
        {
            EditorApplication.update -= CaptureGallery;
            if (SessionState.GetBool(GalleryKey, false)) EditorApplication.update += CaptureGallery;
        }

        private static void CaptureGallery()
        {
            if (!EditorApplication.isPlaying || Time.time < 1.5f) return;
            if (SessionState.GetBool(GalleryKey, false))
            {
                Directory.CreateDirectory("Artifacts");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/skeleton-appearance-gallery.png"));
                SessionState.SetBool(GalleryKey, false);
                Debug.Log("SKELETON_GALLERY_READY");
                return;
            }
            if (Time.time < 2f) return;
            EditorApplication.update -= CaptureGallery;
            EditorSceneManager.LoadSceneAsyncInPlayMode(Nomad.Terrain.Editor.TerrainIntegrationAuthoring.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
        }
    }
}
