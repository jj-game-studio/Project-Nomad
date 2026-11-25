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

            Transform previous = village.Find("Field Exit Guide");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var guide = new GameObject("Field Exit Guide");
            guide.transform.SetParent(village, false);
            Material wood = Material("Guide Wood", new Color(0.32f, 0.19f, 0.09f));
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
            Transform entrance = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
                .First(node => node.name == "Field Entrance");
            Material arrowMaterial = Material("Guide Arrow", new Color(1f, 0.78f, 0.12f));
            arrowMaterial.shader = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (float offset in new[] { 18f, 10f, 3f }) Arrow(guide.transform, entrance.position.z + offset, arrowMaterial);

            Vector3 gate = entrance.position + Vector3.forward;
            Box(guide.transform, "Entrance Banner", gate + Vector3.up * 3.3f, new Vector3(6.5f, 1.1f, 0.15f), wood);
            foreach (float x in new[] { -3.1f, 3.1f })
                Box(guide.transform, "Entrance Guide Post", gate + new Vector3(x, 1.65f, 0), new Vector3(0.18f, 3.3f, 0.18f), wood);
            var banner = new GameObject("Field Entrance Label").AddComponent<TextMesh>();
            banner.transform.SetParent(guide.transform, false);
            banner.transform.position = gate + new Vector3(0, 3.3f, -0.12f);
            banner.transform.rotation = Quaternion.Euler(25, 0, 0);
            banner.anchor = TextAnchor.MiddleCenter;

            banner.fontSize = 72;
            banner.characterSize = 0.065f;
            banner.color = new Color(1f, 0.91f, 0.56f);
            ShopAuthoring.SetSignText(banner, "필드 입구  ↓  이 문을 지나 출격");
            // 안내물은 이동·점프를 막지 않습니다.
            foreach (Collider collider in guide.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            EditorUtility.SetDirty(shop);
        }

        private static void Arrow(Transform parent, float z, Material material)
        {
            var arrow = new GameObject("To Field");
            arrow.transform.SetParent(parent, false);
            // 포장 돌 위에서도 화살표가 조각나 보이지 않는 높이입니다.
            arrow.transform.position = new Vector3(0, 0.45f, z);
            arrow.transform.localScale = new Vector3(1.5f, 1, 1.5f);
            const string path = MaterialFolder + "/Field Guide Arrow.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Filled Field Guide Arrow" };
                mesh.vertices = new[] { new Vector3(-0.28f, 0, 1.1f), new Vector3(0.28f, 0, 1.1f),
                    new Vector3(0.28f, 0, -0.3f), new Vector3(-0.28f, 0, -0.3f),
                    new Vector3(-1, 0, -0.25f), new Vector3(1, 0, -0.25f), new Vector3(0, 0, -1.6f) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6 };

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            arrow.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
