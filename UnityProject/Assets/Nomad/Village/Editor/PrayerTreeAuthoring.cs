using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Village.Editor
{
    // 마을 광장의 큰 나무에 기도(체력·마나 회복) 상호작용을 붙입니다.
    internal static class PrayerTreeAuthoring
    {
        private const string TreeName = "SM_Env_Big_Tree_01";

        [MenuItem("Nomad/Village/Place Prayer Tree")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(VillageAuthoring.ScenePath, OpenSceneMode.Single);
            ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static PrayerTree ApplyToScene(Scene scene)
        {
            Transform tree = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                tree ??= root.transform.Find("Plaza/" + TreeName);
            if (tree == null)
                throw new InvalidOperationException("마을 광장의 큰 나무를 찾을 수 없습니다.");

            if (!tree.TryGetComponent(out PrayerTree prayer))
                prayer = tree.gameObject.AddComponent<PrayerTree>();

            // 지상 1m 높이에서 줄기 바깥 면까지 거리를 재서 상호작용 지점 반경으로 씁니다.
            Physics.SyncTransforms();
            Vector3 center = tree.position + Vector3.up;
            Vector3 origin = center - Vector3.forward * 10;
            float nearest = 10;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.forward, 10, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(tree))
                    nearest = Mathf.Min(nearest, hit.distance);
            }

            float radius = nearest < 10 ? 10 - nearest : 1;

            var data = new SerializedObject(prayer);
            data.FindProperty("trunkRadius").floatValue = Mathf.Round((radius + 0.2f) * 10) / 10;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Village] 기도 나무 줄기 반경 {radius:0.00}m");
            return prayer;
        }
    }
}
