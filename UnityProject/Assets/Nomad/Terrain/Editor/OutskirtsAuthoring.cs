using Nomad.Village.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class OutskirtsAuthoring
    {
        public static void Apply()
        {
            VillageAuthoring.UpdateBoundary();
            foreach (string path in new[] { "Assets/Nomad/Game/Field.unity", TerrainIntegrationAuthoring.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (FieldDressing dressing in Object.FindObjectsByType<FieldDressing>(FindObjectsSortMode.None))
                {
                    var data = new SerializedObject(dressing);
                    data.FindProperty("outerForestWidth").floatValue = 18f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("OUTSKIRTS_SCENES_SAVED");
        }
    }
}
