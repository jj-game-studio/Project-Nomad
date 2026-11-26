using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Game.Editor
{
    public static class StaffGatewayAuthoring
    {
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Nomad/PlayerVisual/Animations/PlayerSword.controller");
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);

            var scene = EditorSceneManager.OpenScene("Assets/Nomad/Village/Village.unity");
            Nomad.Shop.Editor.FieldGatewayAuthoring.Apply(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("STAFF_GATEWAY_SAVED");
        }
    }
}
