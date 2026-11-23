using System;
using Nomad.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Village.Editor
{
    // 마을 광장의 큰 나무에 기도(체력·마나 회복)를 붙이고, 마을 규칙(이동 속도)과 시작 위치를 맞춥니다.
    internal static class PrayerTreeAuthoring
    {
        private const string TreeName = "SM_Env_Big_Tree_01";
        private const string RulesName = "Village Rules";

        // 기존 마을 씬을 다시 만들지 않고 이번 변경만 적용합니다.
        [MenuItem("Nomad/Village/Apply Prayer Tree And Rules")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(VillageAuthoring.ScenePath, OpenSceneMode.Single);
            Nomad.Shop.Editor.VillageShopAuthoring.ApplyToScene(scene);
            ApplyToScene(scene);
            MoveStartEntrance(scene);
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

            // 지상 1m 높이에서 줄기 바깥 면까지 거리를 재고, 그 바깥 1.4m까지 기도할 수 있게 합니다.
            Physics.SyncTransforms();
            Vector3 origin = tree.position + Vector3.up - Vector3.forward * 10;
            float nearest = 10;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.forward, 10, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(tree))
                    nearest = Mathf.Min(nearest, hit.distance);
            }

            float radius = nearest < 10 ? 10 - nearest : 1;
            var data = new SerializedObject(prayer);
            data.FindProperty("range").floatValue = Mathf.Round((radius + 1.4f) * 10) / 10;
            data.ApplyModifiedPropertiesWithoutUndo();

            // 마을에 있는 동안만 이동 속도 배율을 적용하는 규칙 오브젝트입니다.
            Transform village = tree.root;
            Transform rules = village.Find(RulesName);
            if (rules == null)
            {
                rules = new GameObject(RulesName).transform;
                rules.SetParent(village, false);
            }
            if (!rules.TryGetComponent(out VillageRules _))
                rules.gameObject.AddComponent<VillageRules>();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Village] 기도 나무 줄기 반경 {radius:0.00}m");
            return prayer;
        }

        // 게임 시작(기본 입구) 위치를 광장 큰 나무 남쪽으로 옮깁니다. 필드 복귀 입구는 그대로 둡니다.
        private static void MoveStartEntrance(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RegionEntrance entrance in root.GetComponentsInChildren<RegionEntrance>(true))
                {
                    if (!string.IsNullOrEmpty(entrance.FromRegion))
                        continue;

                    entrance.transform.position = VillageAuthoring.StartPosition;
                    EditorUtility.SetDirty(entrance.transform);
                }
            }
        }
    }
}
