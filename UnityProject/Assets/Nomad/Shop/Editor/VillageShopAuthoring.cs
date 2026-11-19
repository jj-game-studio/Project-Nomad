using System;
using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Player;
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
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("VILLAGE_SHOP_CREATED " + ScenePath);
        }

        // 마을 재생성 경로도 이 함수를 사용합니다. 기존 노점과 플레이어 설정을 유지합니다.
        public static void ApplyToScene(Scene scene)
        {
            Transform market = null;
            PlayerMovement player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                market ??= root.transform.Find("Plaza/Market");
                player ??= root.GetComponentInChildren<PlayerMovement>(true);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopAuthoring.Folder + "/Demo/TemporaryShop.asset");
            if (market == null || player == null || catalog == null)
                throw new InvalidOperationException("마을 시장·플레이어·상점 카탈로그가 필요합니다.");

            PlayerInventory inventory = player.GetComponent<PlayerInventory>() ?? player.gameObject.AddComponent<PlayerInventory>();
            if (player.GetComponent<PlayerWallet>() == null)
                player.gameObject.AddComponent<PlayerWallet>();
            if (player.GetComponent<Health>() == null)
                player.gameObject.AddComponent<Health>();
            if (player.GetComponent<PlayerInteraction>() == null)
                player.gameObject.AddComponent<PlayerInteraction>();
            RegisterItems(inventory, catalog);

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
            sign.text = "MARKET\n[E] Trade";
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.fontSize = 60;
            sign.characterSize = 0.045f;
            sign.color = new Color(1, 0.9f, 0.6f);
        }

        private static void RegisterItems(PlayerInventory inventory, ShopCatalog catalog)
        {
            var data = new SerializedObject(inventory);
            SerializedProperty registered = data.FindProperty("items");
            var items = new HashSet<UnityEngine.Object>();
            for (int index = 0; index < registered.arraySize; index++)
                items.Add(registered.GetArrayElementAtIndex(index).objectReferenceValue);

            for (int index = 0; index < catalog.Count; index++)
            {
                ItemDefinition item = catalog.GetOffer(index).Item;
                if (!items.Add(item))
                    continue;
                registered.arraySize++;
                registered.GetArrayElementAtIndex(registered.arraySize - 1).objectReferenceValue = item;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
