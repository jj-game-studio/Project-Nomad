using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Shop.Editor
{
    public static class VillageShopAuthoring
    {
        internal const string ScenePath = "Assets/Nomad/Village/Village.unity";
        internal const string ShopName = "Village Trading Post";

        [MenuItem("Nomad/Shop/Open Village Shop")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/Shop/Place Shop In Village")]
        public static void Build()
        {
            if (EditorApplication.isPlaying ||
                (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()))
                return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("마을 상점이 배치된 씬을 저장하지 못했습니다.");
            AssetDatabase.SaveAssets();
            Debug.Log("VILLAGE_SHOP_CREATED " + ScenePath);
        }

        // 마을에는 거래 지점만 둡니다. 플레이어는 메인 게임에서 생성합니다.
        public static void ApplyToScene(Scene scene)
        {
            Transform market = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                market ??= root.transform.Find("Plaza/Market");

            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopAuthoring.CatalogPath);
            if (market == null || catalog == null)
                throw new InvalidOperationException("마을 시장과 상점 카탈로그가 필요합니다.");

            Transform existing = market.Find(ShopName);
            if (existing != null)
            {
                ShopAuthoring.SetReference(existing.GetComponent<Shopkeeper>(), "catalog", catalog);
                return;
            }

            // 남서쪽 노점의 광장 쪽 앞면에 접근점을 두어 길과 카운터 충돌체를 피합니다.
            Transform stall = market.GetChild(market.childCount - 1);
            Renderer[] renderers = stall.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException("시장 노점 외형을 찾을 수 없습니다.");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            Vector3 inward = -new Vector3(bounds.center.x, 0, bounds.center.z).normalized;
            float depth = Mathf.Abs(inward.x) * bounds.extents.x + Mathf.Abs(inward.z) * bounds.extents.z;
            Vector3 point = bounds.center + inward * (depth + 0.8f);
            point.y = 0.05f;

            var shop = new GameObject(ShopName);
            shop.transform.SetParent(market, false);
            shop.transform.position = point;
            Shopkeeper keeper = shop.AddComponent<Shopkeeper>();
            ShopAuthoring.SetReference(keeper, "catalog", catalog);
            var data = new SerializedObject(keeper);
            data.FindProperty("interactionId").stringValue = "village-market-trader";
            data.ApplyModifiedPropertiesWithoutUndo();
            BoxCollider trigger = shop.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 0.5f;
            trigger.size = new Vector3(1.2f, 1, 1.2f);

            var sign = new GameObject("Trade sign").AddComponent<TextMesh>();
            sign.transform.SetParent(shop.transform, false);
            sign.transform.localPosition = Vector3.up * 2.2f;
            sign.transform.rotation = Quaternion.Euler(50, 0, 0);
            ShopAuthoring.SetSignText(sign, "마을 상점\n[E] 거래");
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.fontSize = 60;
            sign.characterSize = 0.045f;
            sign.color = new Color(1, 0.9f, 0.6f);
        }
    }
}
