using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Shop.Editor
{
    public static class FieldGatewayAuthoring
    {
        private const string Models = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs/";

        public static void Apply(Scene scene)
        {
            Transform village = scene.GetRootGameObjects().First(root => root.name == "Village").transform;
            Transform entrance = village.Find("Field Entrance");
            foreach (string name in new[] { "Field Exit Guide", "Field Gateway" })
                if (village.Find(name) is Transform old) Object.DestroyImmediate(old.gameObject);

            // 출입 트리거와 귀환 위치는 유지하고 장식만 교체합니다.
            foreach (Transform child in entrance.Cast<Transform>().ToArray())
                if (child.name.StartsWith("SM_")) Object.DestroyImmediate(child.gameObject);

            var root = new GameObject("Field Gateway");
            root.transform.SetParent(village, false);
            root.transform.position = entrance.position;
            Model(root.transform, "Stone Arch", "Castle/SM_Bld_Castle_Wall_Archway_01", new Vector3(0, 2.6f, 0), new Vector3(7.2f, 5.2f, 1.2f));
            foreach (float side in new[] { -1f, 1f })
            {
                // 아치 안쪽에는 충돌체를 두지 않습니다.
                var pillar = new GameObject("Gate Pillar");
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.localPosition = new Vector3(side * 3.1f, 2.4f, 0);
                pillar.AddComponent<BoxCollider>().size = new Vector3(1, 4.8f, 1.2f);

                Model(root.transform, "Gate Banner", "Props/Banners/SM_Prop_Flag_Horizontal_03",
                    new Vector3(side * 2.7f, 3.2f, -0.75f), new Vector3(0.75f, 1.5f, 0.15f));
                Model(root.transform, "Gate Brazier", "Props/BattleGround/SM_Prop_Camp_Brazier_01",
                    new Vector3(side * 3.8f, 0.6f, -1), new Vector3(0.7f, 1.2f, 0.7f));
                var fire = new GameObject("Gate Fire");
                fire.transform.SetParent(root.transform, false);
                fire.transform.localPosition = new Vector3(side * 3.8f, 1.2f, -1);
                fire.AddComponent<Nomad.Village.FieldGatewayFlame>();
                Light light = fire.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1, 0.65f, 0.25f);
                light.range = 4;
                light.intensity = 1.5f;
            }

            var title = new GameObject("Gateway Title").AddComponent<TextMesh>();
            title.transform.SetParent(root.transform, false);
            title.transform.localPosition = new Vector3(0, 4.5f, -0.7f);
            title.anchor = TextAnchor.MiddleCenter;
            title.fontSize = 64;
            title.characterSize = 0.065f;
            title.color = new Color(1, 0.9f, 0.65f);
            ShopAuthoring.SetSignText(title, "필 드");
        }

        private static GameObject Model(Transform parent, string name, string path, Vector3 center, Vector3 size)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + path + ".prefab");
            if (prefab == null) throw new System.InvalidOperationException("관문 모델 없음: " + path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Bounds bounds = Bounds(model);
            model.transform.localScale = Vector3.Scale(model.transform.localScale,
                new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
            model.transform.position += parent.TransformPoint(center) - Bounds(model).center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            return model;
        }

        private static Bounds Bounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
