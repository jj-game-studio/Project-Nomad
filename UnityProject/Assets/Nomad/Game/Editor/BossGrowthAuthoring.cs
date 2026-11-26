using System.IO;
using Nomad.Monsters.Demo;
using Nomad.Game.Modules;
using Nomad.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Game.Editor
{
    public static class BossGrowthAuthoring
    {
        public static void Prime() => AssetDatabase.ImportAsset("Assets/Nomad/Combat/Health.cs", ImportAssetOptions.ForceUpdate);

        public static void Apply()
        {
            const string output = "Assets/Nomad/Monsters/Resources/BossArenaRock.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Rock_Chunk_01.prefab");
            if (source == null) throw new System.InvalidOperationException("보스 바위 에셋 없음");
            GameObject instance = Object.Instantiate(source);
            GameObject rock = PrefabUtility.SaveAsPrefabAsset(instance, output);
            Object.DestroyImmediate(instance);
            foreach (string path in new[] { "Assets/Nomad/Game/Field.unity", "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GameObject root = path.Contains("TerrainIntegration") ? Object.FindFirstObjectByType<GameplayIntegration>(FindObjectsInactive.Include).gameObject :
                    Object.FindFirstObjectByType<FieldRegion>(FindObjectsInactive.Include).gameObject;
                foreach (BossEncounterContent old in Object.FindObjectsByType<BossEncounterContent>(FindObjectsSortMode.None))
                    if (old.gameObject != root) Object.DestroyImmediate(old);
                var content = root.GetComponent<BossEncounterContent>() ?? root.AddComponent<BossEncounterContent>();
                content.Rock = rock;
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BOSS_GROWTH_SCENES_SAVED");
        }
    }
}
