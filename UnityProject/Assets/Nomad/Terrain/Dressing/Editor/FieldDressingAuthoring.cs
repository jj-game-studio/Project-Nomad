using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomad.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nomad.Terrain.Editor
{
    // #73 1단계: 기존 통합 씬을 건드리지 않고 TerrainTest 씬에서 조명·포스트·Kingdom 꾸밈을 검토합니다.
    public static class FieldDressingAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Terrain/Test/TerrainTest.unity";
        public const string VolumePath = "Assets/Nomad/Terrain/Test/TerrainTestVolume.asset";
        public const string SetPath = "Assets/ThirdParty/Nomad/Terrain/Dressing/Resources/Nomad/FieldDressingSet.asset";

        private const string Kingdom = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/";
        private const string Environments = Kingdom + "Prefabs/Environments/SM_Env_";

        [MenuItem("Nomad/Terrain/Dressing/Rebuild Kingdom Dressing Set")]
        public static void CreateSet()
        {
            FieldDressingSet set = AssetDatabase.LoadAssetAtPath<FieldDressingSet>(SetPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SetPath));
                set = ScriptableObject.CreateInstance<FieldDressingSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            set.groundMaterial = CreateGroundMaterial();
            set.groundCover = Load(Environments, "Grass_Tuft_01", "Grass_Tuft_02", "Grass_Tuft_03", "Fern_01", "Fern_02",
                "Flowers_01", "Flowers_02", "Bush_Small_01", "Bush_Small_02", "Bush_Small_03");
            set.cliffRocks = Load(Environments, "Rock_Cliff_01", "Rock_Cliff_02", "Rock_Cliff_03", "Rock_Cliff_04", "Rock_Cliff_05");
            set.trees = Load(Environments, "Tree_Round_01", "Tree_Round_02", "Tree_Round_03", "Tree_Round_04",
                "Tree_Thin_01", "Tree_Thin_02", "Tree_Thin_03");
            set.cover = Load(Kingdom + "Prefabs/Props/SM_Prop_", "Barrel_01", "Barrel_Stack_01", "Crate_Wood_01",
                "Crate_Stack_01", "Hay_Bale_01", "Sack_01", "Sack_02");

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"FIELD_DRESSING_SET ground={set.groundMaterial != null} cover={set.groundCover.Length} "
                + $"rocks={set.cliffRocks.Length} trees={set.trees.Length} props={set.cover.Length}");
        }

        // 원본 Grass_01은 위에서 내려다볼 때 햇빛 정반사로 노랗게 번쩍이므로, 반사를 끈 사본을 비공개 폴더에 둡니다.
        private static Material CreateGroundMaterial()
        {
            string path = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(SetPath))) + "/FieldGround.mat";
            Material source = AssetDatabase.LoadAssetAtPath<Material>(Kingdom + "Materials/Ground/Grass_01.mat");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path.Replace('\\', '/'));
            }
            else material.CopyPropertiesFromMaterial(source);

            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Glossiness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject[] Load(string prefix, params string[] names)
        {
            var result = new List<GameObject>();
            foreach (string name in names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefix + name + ".prefab");
                if (prefab == null) Debug.LogWarning($"FIELD_DRESSING_MISSING {prefix}{name}");
                else result.Add(prefab);
            }
            return result.ToArray();
        }

        [MenuItem("Nomad/Terrain/Dressing/Open TerrainTest Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) RebuildScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        // 기존 통합 씬을 복사한 뒤 꾸밈·조명·포스트만 덧붙입니다. 원본 통합 씬은 수정하지 않습니다.
        [MenuItem("Nomad/Terrain/Dressing/Rebuild TerrainTest Scene")]
        public static void RebuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(TerrainIntegrationAuthoring.ScenePath, ScenePath))
                throw new IOException("통합 씬을 TerrainTest로 복사하지 못했습니다.");

            EditorSceneManager.OpenScene(ScenePath);
            TerrainIntegrationControls controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            AddDressing(controls);
            ConfigureLight();
            ConfigureVolume(Camera.main);

            EditorSceneManager.MarkSceneDirty(controls.gameObject.scene);
            EditorSceneManager.SaveScene(controls.gameObject.scene);
            Debug.Log($"FIELD_DRESSING_SCENE {ScenePath}");
        }

        private static void AddDressing(TerrainIntegrationControls controls)
        {
            var source = new SerializedObject(controls);
            var generator = (TerrainGenerator)source.FindProperty("generator").objectReferenceValue;
            var scatter = (PropScatter)source.FindProperty("scatter").objectReferenceValue;

            FieldDressing dressing = controls.GetComponent<FieldDressing>();
            if (dressing == null) dressing = controls.gameObject.AddComponent<FieldDressing>();
            var target = new SerializedObject(dressing);
            target.FindProperty("generator").objectReferenceValue = generator;
            target.FindProperty("obstacleOwner").objectReferenceValue = scatter;
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        // Duckov 참고 화면처럼 낮은 각도의 따뜻한 햇빛과 부드러운 긴 그림자를 사용합니다.
        private static void ConfigureLight()
        {
            Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                .FirstOrDefault(light => light.type == LightType.Directional);
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            sun.color = new Color(1f, 0.9f, 0.76f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.45f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.16f);
        }

        private static void ConfigureVolume(Camera camera)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            Tonemapping tonemapping = Override<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Bloom bloom = Override<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.3f);

            ColorAdjustments color = Override<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(14f);
            color.saturation.Override(4f);

            WhiteBalance balance = Override<WhiteBalance>(profile);
            balance.temperature.Override(5f);

            Vignette vignette = Override<Vignette>(profile);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Volume volume = Object.FindAnyObjectByType<Volume>();
            if (volume == null) volume = new GameObject("TerrainTestVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.2f, 0.14f);
        }

        private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        // 세트·씬을 다시 만들고 여러 seed에서 Ready·경로·꾸밈 배치를 확인한 뒤 렌더를 남깁니다.
        public static void CreateAndVerify()
        {
            int code = 1;
            try
            {
                CreateSet();
                RebuildScene();
                Verify();
                code = 0;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("Nomad/Terrain/Dressing/Verify TerrainTest")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath);
            TerrainIntegrationControls controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            FieldDressing dressing = controls.GetComponent<FieldDressing>();
            var source = new SerializedObject(controls);
            dressing.Configure(controls.Generator, (PropScatter)source.FindProperty("scatter").objectReferenceValue);

            int totalBlockers = 0;
            for (int run = 0; run < 6; run++)
            {
                TerrainGenerationResult result = controls.Regenerate(run > 0);
                FieldGenerationResult field = controls.Navigation.LastResult;
                if (!result.Success || field == null || !field.Success)
                    throw new System.InvalidOperationException($"seed {result.Seed} 준비 실패: {controls.NavigationFailure}");
                if (dressing.Placed == 0 || dressing.Root == null)
                    throw new System.InvalidOperationException($"seed {result.Seed} 꾸밈이 배치되지 않았습니다.");
                if (dressing.Root.GetComponentsInChildren<Collider>().Any(collider => !(collider is BoxCollider)))
                    throw new System.InvalidOperationException("꾸밈에 BoxCollider 이외의 충돌체가 남았습니다.");

                Debug.Log($"FIELD_DRESSING_SEED seed={result.Seed} placed={dressing.Placed} blockers={dressing.Blockers} "
                    + $"removed={dressing.RemovedBlockers} enemies={field.EnemyPoints.Count} chests={field.ChestPoints.Count} "
                    + $"paths={controls.Generator.ReachableSamples}");
                totalBlockers += dressing.Blockers;
                if (run == 0)
                {
                    // 지면 재질이 교체되지 않은 타일 렌더러를 확인합니다.
                    string materials = string.Join(", ", result.Pieces
                        .SelectMany(piece => piece.GetComponentsInChildren<MeshRenderer>())
                        .GroupBy(renderer => $"{renderer.sharedMaterial?.name}/{renderer.enabled}")
                        .Select(group => $"{group.Key}x{group.Count()}"));
                    Debug.Log($"FIELD_DRESSING_MATERIALS {materials}");
                    TerrainIntegrationValidation.Capture(controls, 1, "terrain-test-overview");
                    TerrainIntegrationValidation.Capture(controls, 0, "terrain-test-player");
                }
            }
            if (totalBlockers == 0)
                throw new System.InvalidOperationException("모든 seed에서 나무·엄폐물이 경로 복구로 제거되었습니다.");
            Debug.Log($"FIELD_DRESSING_PASS blockers={totalBlockers}");
        }
    }
}
