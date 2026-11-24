using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Shop.Editor
{
    public static class VillagePresentationAuthoring
    {
        private const string MaterialFolder = "Assets/Nomad/Shop/Generated";

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
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/PlayerVisual/KingdomSoldierVisual.prefab"), shop);
            npc.name = "Merchant NPC";
            npc.transform.SetPositionAndRotation(front, Quaternion.Euler(0, 180, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
            foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Animator animator in npc.GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;
            TextMesh sign = shop.Find("Trade sign").GetComponent<TextMesh>();
            ShopAuthoring.SetSignText(sign, "상인 · E 거래");
            sign.transform.position = front + Vector3.up * 2.6f;

            Transform previous = village.Find("Field Exit Guide");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var guide = new GameObject("Field Exit Guide");
            guide.transform.SetParent(village, false);
            Material wood = Material("Guide Wood", new Color(0.32f, 0.19f, 0.09f));
            Material gold = Material("Guide Gold", new Color(1f, 0.78f, 0.25f));
            Box(guide.transform, "Signpost", new Vector3(2.5f, 0.8f, -12), new Vector3(0.16f, 1.6f, 0.16f), wood);
            Box(guide.transform, "Signboard", new Vector3(2.5f, 1.35f, -12), new Vector3(2.2f, 0.65f, 0.14f), wood);
            var text = new GameObject("Field direction").AddComponent<TextMesh>();
            text.transform.SetParent(guide.transform, false);
            text.transform.position = new Vector3(2.5f, 1.35f, -12.1f);
            text.transform.rotation = Quaternion.Euler(25, 0, 0);
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 60;
            text.characterSize = 0.04f;
            text.color = new Color(1f, 0.9f, 0.65f);
            ShopAuthoring.SetSignText(text, "필드 출입구 ↓");
            foreach (float z in new[] { -16f, -24f, -31f }) Arrow(guide.transform, z, gold);
            EditorUtility.SetDirty(shop);
        }

        private static void Arrow(Transform parent, float z, Material material)
        {
            var arrow = new GameObject("To Field").AddComponent<LineRenderer>();
            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = new Vector3(0, 0.07f, z);
            arrow.useWorldSpace = false;
            arrow.sharedMaterial = material;
            arrow.startWidth = arrow.endWidth = 0.15f;
            arrow.positionCount = 5;
            arrow.SetPositions(new[] { new Vector3(0, 0, 0.8f), new Vector3(0, 0, -0.8f),
                new Vector3(-0.6f, 0, -0.2f), new Vector3(0, 0, -0.8f), new Vector3(0.6f, 0, -0.2f) });
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Material(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                System.IO.Directory.CreateDirectory(MaterialFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
