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
            set.coverThemes = new[]
            {
                Theme("Meadow", Load(Environments, "Grass_Tuft_01", "Grass_Tuft_02", "Grass_Tuft_03"),
                    Load(Kingdom + "Prefabs/Generic/SM_Generic_", "Grass_Patch_01", "Grass_Patch_02", "Grass_Patch_03")),
                Theme("Flowers", Load(Environments, "Flowers_01", "Flowers_02", "Flowers_03", "Flowers_04",
                    "Bush_Small_03", "Grass_Tuft_01")),
                Theme("Ferns", Load(Environments, "Fern_01", "Fern_02", "Fern_03", "Fern_04", "Bush_Small_01", "Bush_Small_02"),
                    Load(Kingdom + "Prefabs/Items/SM_Item_", "Mushroom_01", "Mushroom_02")),
                Theme("Shrubs", Load(Environments, "Bush_Small_04", "Bush_Small_05", "Grass_Tuft_02", "Rock_Chunk_01"),
                    Load(Kingdom + "Prefabs/Generic/SM_Generic_", "Small_Rocks_01", "Small_Rocks_02")),
            };
            set.cliffRocks = Load(Environments, "Rock_Cliff_01", "Rock_Cliff_02", "Rock_Cliff_03", "Rock_Cliff_04", "Rock_Cliff_05");
            set.trees = Load(Environments, "Tree_Round_01", "Tree_Round_02", "Tree_Round_03", "Tree_Round_04",
                "Tree_Thin_01", "Tree_Thin_02", "Tree_Thin_03");
            set.cover = Load(Kingdom + "Prefabs/Props/SM_Prop_", "Barrel_01", "Crate_Wood_01", "Hay_Bale_01", "Sack_01", "Sack_02")
                .Concat(Load(Kingdom + "Prefabs/Generic/SM_Generic_", "TreeStump_01")).ToArray();
            set.fences = Load(Kingdom + "Prefabs/Props/SM_Prop_", "Fence_01", "Fence_02", "Fence_03", "Fence_04");
            set.borderWalls = Load(Environments, "StoneWall_01", "StoneWall_02", "StoneWall_03");

            // 기존 에셋을 갱신할 때도 밀도를 현재 기준값으로 맞춥니다.
            set.coverThreshold = 0.48f;
            set.coverMaxPerTile = 3;
            set.treeChance = 0.16f;
            set.coverChance = 0.08f;
            set.fenceChance = 0.03f;
            set.cliffRockChance = 0.4f;

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"FIELD_DRESSING_SET ground={set.groundMaterial != null} themes={set.coverThemes.Length} "
                + $"rocks={set.cliffRocks.Length} trees={set.trees.Length} props={set.cover.Length} "
                + $"fences={set.fences.Length} walls={set.borderWalls.Length}");
            LogSizes(set.cliffRocks.Concat(set.fences).Concat(set.borderWalls).Concat(set.cover));
        }

        private static FieldDressingSet.CoverTheme Theme(string name, params GameObject[][] groups) =>
            new FieldDressingSet.CoverTheme { name = name, prefabs = groups.SelectMany(group => group).ToArray() };

        // 배치 배율 조정의 근거가 되도록 원본 크기를 로그로 남깁니다.
        private static void LogSizes(IEnumerable<GameObject> prefabs)
        {
            foreach (GameObject prefab in prefabs)
            {
                Bounds bounds = default;
                bool found = false;
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>())
                {
                    if (found) bounds.Encapsulate(renderer.bounds);
                    else bounds = renderer.bounds;
                    found = true;
                }
                Debug.Log($"FIELD_DRESSING_SIZE {prefab.name} size={bounds.size:F2}");
            }
        }

        // 원본 Grass 재질은 삼각 무늬 텍스처가 3m 타일마다 반복되어 어색하므로, 텍스처 없는 무광 잔디색을 씁니다.
        // 2단계 Terrain Layer 도입 전까지의 임시 지면입니다.
        private static Material CreateGroundMaterial()
        {
            string path = (Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(SetPath))) + "/FieldGround.mat")
                .Replace('\\', '/');
            AssetDatabase.DeleteAsset(path);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.34f, 0.43f, 0.2f));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
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
            var scatter = (PropScatter)source.FindProperty("scatter").objectReferenceValue;
            int[] seeds = { 51, 7, 1234, 2024, 99, 4242 };

            int totalBlockers = 0;
            int baselineChests = 0;
            int dressedChests = 0;
            for (int run = 0; run < seeds.Length; run++)
            {
                // 같은 seed에서 꾸밈 없는 기준 결과를 먼저 만들어 상자·적 후보 수를 비교합니다.
                source.Update();
                source.FindProperty("seed").intValue = seeds[run];
                source.ApplyModifiedPropertiesWithoutUndo();
                dressing.Configure(null, scatter);
                controls.Regenerate(false);
                FieldGenerationResult baseline = controls.Navigation.LastResult;

                dressing.Configure(controls.Generator, scatter);
                TerrainGenerationResult result = controls.Regenerate(false);
                FieldGenerationResult field = controls.Navigation.LastResult;
                baselineChests += baseline.ChestPoints.Count;
                dressedChests += field.ChestPoints.Count;
                if (!result.Success || field == null || !field.Success)
                    throw new System.InvalidOperationException($"seed {result.Seed} 준비 실패: {controls.NavigationFailure}");
                if (dressing.Placed == 0 || dressing.Root == null)
                    throw new System.InvalidOperationException($"seed {result.Seed} 꾸밈이 배치되지 않았습니다.");
                if (dressing.Root.GetComponentsInChildren<Collider>().Any(collider => !(collider is BoxCollider)))
                    throw new System.InvalidOperationException("꾸밈에 BoxCollider 이외의 충돌체가 남았습니다.");

                // 2단계: 지면·충돌이 Terrain으로 바뀌고 타일 BoxCollider는 모두 꺼져 있어야 합니다.
                Transform fieldRoot = result.Pieces[0].transform.parent;
                if (fieldRoot.GetComponentInChildren<UnityEngine.Terrain>() == null)
                    throw new System.InvalidOperationException($"seed {result.Seed} 필드 Terrain이 없습니다.");
                if (result.Pieces.Any(piece => piece.GetComponentsInChildren<BoxCollider>().Any(box => box.enabled)))
                    throw new System.InvalidOperationException($"seed {result.Seed} 켜진 타일 BoxCollider가 남았습니다.");

                // 언덕 지형은 절벽 없이 모두 걸을 수 있어야 하고(최대 경사 45° 미만), 0/1/2m 단이 나뉘어야 합니다.
                UnityEngine.Terrain surface = fieldRoot.GetComponentInChildren<UnityEngine.Terrain>();
                float steepest = 0f;
                var levels = new HashSet<int>();
                for (int row = 0; row <= 64; row++)
                for (int column = 0; column <= 64; column++)
                {
                    float u = column / 64f, v = row / 64f;
                    steepest = Mathf.Max(steepest, surface.terrainData.GetSteepness(u, v));
                    levels.Add(Mathf.RoundToInt(surface.terrainData.GetInterpolatedHeight(u, v)));
                }
                if (steepest >= 45f)
                    throw new System.InvalidOperationException($"seed {result.Seed} 걸을 수 없는 급경사 {steepest:F1}°가 있습니다.");
                Debug.Log($"FIELD_TERRAIN seed={result.Seed} steepest={steepest:F1} levels={string.Join("/", levels.OrderBy(level => level))}");

                // 시작점이 덤불·나무 충돌체 안에 있으면 플레이어가 갇힙니다.
                // 플레이어 캡슐(반지름 0.35m, 높이 1.8m)과 실제로 겹치는 꾸밈 충돌체가 있는지 확인합니다.
                Vector3 start = field.PlayerPosition;
                Physics.SyncTransforms();
                bool trapped = Physics.OverlapCapsule(start + Vector3.up * 0.4f, start + Vector3.up * 1.45f, 0.34f)
                    .Any(collider => collider.transform.IsChildOf(dressing.Root));
                if (trapped) throw new System.InvalidOperationException($"seed {result.Seed} 시작점이 꾸밈 충돌체와 겹칩니다.");

                Debug.Log($"FIELD_DRESSING_SEED seed={result.Seed} placed={dressing.Placed} blockers={dressing.Blockers} "
                    + $"removed={dressing.RemovedBlockers} enemies={baseline.EnemyPoints.Count}->{field.EnemyPoints.Count} "
                    + $"chests={baseline.ChestPoints.Count}->{field.ChestPoints.Count} paths={controls.Generator.ReachableSamples}");
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

                    // 외곽 돌벽과 절벽 바위를 가까이에서 확인합니다.
                    Transform wall = dressing.Root.Cast<Transform>().FirstOrDefault(child => child.name.Contains("StoneWall"));
                    Transform rock = dressing.Root.Cast<Transform>().FirstOrDefault(child => child.name.Contains("Rock_Cliff"));
                    if (wall == null) throw new System.InvalidOperationException("외곽 돌벽이 배치되지 않았습니다.");
                    TerrainIntegrationValidation.Capture(controls, 0, "terrain-test-border", wall.position);
                    if (rock != null) TerrainIntegrationValidation.Capture(controls, 0, "terrain-test-cliff", rock.position);
                    TerrainPiece ramp = result.Pieces.FirstOrDefault(piece => piece.GetComponent<MicroTerrainTile>().IsStair);
                    if (ramp != null) TerrainIntegrationValidation.Capture(controls, 0, "terrain-test-ramp", ramp.transform.position);

                    // 맵 안쪽 능선의 돌담·울타리를 가까이에서 확인합니다(외곽 돌벽 제외).
                    Bounds area = fieldRoot.GetComponentInChildren<UnityEngine.Terrain>().terrainData.bounds;
                    area.center += fieldRoot.GetComponentInChildren<UnityEngine.Terrain>().transform.position;
                    area.Expand(new Vector3(-6f, 100f, -6f));
                    Transform[] ridges = dressing.Root.Cast<Transform>()
                        .Where(child => (child.name.Contains("StoneWall") || child.name.Contains("Fence")) && area.Contains(child.position)).ToArray();
                    Debug.Log($"FIELD_DRESSING_RIDGES seed={result.Seed} crossings={dressing.RidgeCandidates} placed={dressing.RidgeSegments} kept={ridges.Length}");
                    if (ridges.Length > 0) TerrainIntegrationValidation.Capture(controls, 0, "terrain-test-ridge", ridges[ridges.Length / 2].position);
                }
            }
            if (totalBlockers == 0)
                throw new System.InvalidOperationException("모든 seed에서 나무·엄폐물이 경로 복구로 제거되었습니다.");
            Debug.Log($"FIELD_DRESSING_PASS blockers={totalBlockers} chests={baselineChests}->{dressedChests}");
        }
    }
}
