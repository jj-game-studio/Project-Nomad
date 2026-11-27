using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Shop.Editor
{
    public static class VillagePresentationAuthoring
    {


        public static void Apply(Scene scene)
        {
            Transform village = scene.GetRootGameObjects().First(root => root.name == "Village").transform;
            Transform market = village.Find("Plaza/Market");
            Transform shop = market.Find("Village Trading Post");
            if (shop == null) return;

            // 거래 위치와 가장 가까운 실제 노점을 카메라 쪽으로 돌립니다.
            Transform stall = market.Cast<Transform>().Where(child => child != shop && child.GetComponentsInChildren<Renderer>().Length > 0)
                .OrderBy(child => Vector3.SqrMagnitude(child.position - shop.position)).First();
            stall.rotation = Quaternion.Euler(0, 180, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stall);
            Renderer[] renderers = stall.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 front = new Vector3(bounds.center.x, 0.03f, bounds.min.z - 0.25f);
            shop.position = front + Vector3.back * 0.8f;

            Transform old = shop.Find("Merchant NPC");
            GameObject npc = old != null ? old.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/PlayerVisual/KingdomSoldierVisual.prefab"), shop);
            npc.name = "Merchant NPC";
            npc.transform.SetPositionAndRotation(front, Quaternion.Euler(0, 180, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
            foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Animator animator in npc.GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;
            TextMesh sign = shop.Find("Trade sign").GetComponent<TextMesh>();
            ShopAuthoring.SetSignText(sign, "상인 · E 거래");
            sign.transform.position = front + Vector3.up * 2.6f;

            FieldGatewayAuthoring.Apply(scene);
            EditorUtility.SetDirty(shop);
        }
    }
}
