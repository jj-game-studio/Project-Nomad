using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class MicroTerrainSceneSetup
    {
        public static void Create()
        {
            MicroTerrainAuthoring.Create();
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            var data = new SerializedObject(controls);
            data.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(MicroTerrainAuthoring.CatalogPath);
            data.FindProperty("width").intValue = 18;
            data.FindProperty("height").intValue = 18;
            SerializedProperty gallery = data.FindProperty("microSources");
            TerrainPiece[] prefabs = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(MicroTerrainAuthoring.CatalogPath).variants
                .Select(v => v.prefab).Distinct().ToArray();
            gallery.arraySize = prefabs.Length;
            for (int index = 0; index < prefabs.Length; index++) gallery.GetArrayElementAtIndex(index).objectReferenceValue = prefabs[index];
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObject template = new SerializedObject(controls).FindProperty("propTemplate").objectReferenceValue as GameObject;
            Transform crate = template.transform.GetChild(0);
            crate.localScale = Vector3.one * 0.35f;
            crate.localPosition = Vector3.up * 0.175f;
            controls.Generator.ConfigureWfc(AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(MicroTerrainAuthoring.CatalogPath),
                18, 18, 51, new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f)));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), TerrainIntegrationAuthoring.ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
