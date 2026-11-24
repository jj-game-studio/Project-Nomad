using Nomad.Inventory.Editor;
using Nomad.Shop.Editor;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Game.Editor
{
    public static class PlaytestPolishAuthoring
    {
        public static void UpdateVillagePresentation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Nomad/Village/Village.unity");
            VillageShopAuthoring.ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("VILLAGE_GUIDE_SAVED");
        }

        public static void Apply()
        {
            ProvidedItemArt.Apply();
            var scene = EditorSceneManager.OpenScene("Assets/Nomad/Village/Village.unity");
            VillageShopAuthoring.ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            foreach (string path in new[] { "Assets/Nomad/Game/Field.unity", "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity" })
            {
                scene = EditorSceneManager.OpenScene(path);
                // 새 벽 마감 규격을 실제 필드와 통합 검증 씬에 함께 저장합니다.
                foreach (FieldDressing dressing in Object.FindObjectsByType<FieldDressing>(FindObjectsSortMode.None))
                {
                    var data = new SerializedObject(dressing);
                    data.FindProperty("structureWallBay").floatValue = 2.5f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYTEST_POLISH_SCENES_SAVED");
        }
    }
}
