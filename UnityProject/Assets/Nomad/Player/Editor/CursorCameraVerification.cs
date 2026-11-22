using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Player.Editor
{
    public static class CursorCameraVerification
    {
        private const string ScenePath = "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity";

        public static void ApplyIntegrationSettings()
        {
            EditorSceneManager.OpenScene(ScenePath);
            TopDownCamera camera = UnityEngine.Object.FindAnyObjectByType<TopDownCamera>();
            if (camera == null) throw new InvalidOperationException("통합 씬에 TopDownCamera가 없습니다.");

            var data = new SerializedObject(camera);
            data.FindProperty("maxLookAhead").floatValue = 2f;
            data.FindProperty("smoothTime").floatValue = 0.15f;
            data.FindProperty("deadZone").floatValue = 0.1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveScene(camera.gameObject.scene);
            VerifyIntegrationSettings();
        }

        [MenuItem("Nomad/Player/Verify Cursor Camera Settings")]
        public static void VerifyIntegrationSettings()
        {
            EditorSceneManager.OpenScene(ScenePath);
            TopDownCamera camera = UnityEngine.Object.FindAnyObjectByType<TopDownCamera>();
            if (camera == null) throw new InvalidOperationException("통합 씬 카메라 누락");

            var data = new SerializedObject(camera);
            if (data.FindProperty("target").objectReferenceValue == null ||
                data.FindProperty("maxLookAhead").floatValue != 2f ||
                !Mathf.Approximately(data.FindProperty("smoothTime").floatValue, 0.15f) ||
                !Mathf.Approximately(data.FindProperty("deadZone").floatValue, 0.1f))
                throw new InvalidOperationException("통합 씬 카메라 참조·기본값 불일치");

            Debug.Log("CURSOR_CAMERA_INTEGRATION_SETTINGS_PASS");
        }
    }
}
