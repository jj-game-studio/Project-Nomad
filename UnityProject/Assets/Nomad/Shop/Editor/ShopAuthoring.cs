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
    public static class ShopAuthoring
    {
        internal const string Folder = "Assets/Nomad/Shop";
        internal const string ScenePath = Folder + "/ShopDebug.unity";

        [MenuItem("Nomad/Shop/Open Shop Debug")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/Shop/Rebuild Shop Debug")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        // 배치 실행과 메뉴가 동일한 생성 경로를 사용합니다.
        public static void Build()
        {
            EnsureFolder(Folder + "/Demo");
            ItemDefinition[] items = {
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/scrap.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/field-knife.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/field-vest.asset")
            };
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Folder + "/Demo/TemporaryShop.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, Folder + "/Demo/TemporaryShop.asset");
                var data = new SerializedObject(catalog);
                SerializedProperty offers = data.FindProperty("offers");
                offers.arraySize = items.Length;
                int[] buy = { 5, 120, 200 }, sell = { 2, 60, 100 }, stock = { 30, 5, 3 };
                string[] descriptions = {
                    "Salvaged metal. Compare its value before selling.",
                    "A compact field knife for your expedition supplies.",
                    "A field vest. Keep enough gold for your next trip."
                };
                for (int index = 0; index < items.Length; index++)
                {
                    SerializedProperty entry = offers.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("item").objectReferenceValue = items[index];
                    entry.FindPropertyRelative("category").enumValueIndex = index == 0 ? 0 : 1;
                    entry.FindPropertyRelative("description").stringValue = descriptions[index];
                    entry.FindPropertyRelative("buyPrice").intValue = buy[index];
                    entry.FindPropertyRelative("sellPrice").intValue = sell[index];
                    entry.FindPropertyRelative("initialStock").intValue = stock[index];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material floor = MaterialAsset("Ground", new Color(0.17f, 0.23f, 0.23f));
            Material counter = MaterialAsset("Counter", new Color(0.44f, 0.32f, 0.18f));
            Material playerMaterial = MaterialAsset("Player", new Color(0.12f, 0.60f, 0.66f));
            Material merchantMaterial = MaterialAsset("Merchant", new Color(0.88f, 0.70f, 0.34f));
            Primitive("Ground", PrimitiveType.Cube, new Vector3(0, -0.25f, 0), new Vector3(24, 0.5f, 24), floor);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.73f);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0, -0.6f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2;
            controller.radius = 0.35f;
            player.AddComponent<Health>();
            var inventory = player.AddComponent<PlayerInventory>();
            var inventoryData = new SerializedObject(inventory);
            SerializedProperty registered = inventoryData.FindProperty("items");
            registered.arraySize = items.Length;
            for (int index = 0; index < items.Length; index++)
                registered.GetArrayElementAtIndex(index).objectReferenceValue = items[index];
            inventoryData.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<PlayerWallet>();

            GameObject body = Primitive("Body", PrimitiveType.Capsule, player.transform.position + Vector3.up,
                new Vector3(0.7f, 1, 0.7f), playerMaterial);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(player.transform, true);
            Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.backgroundColor = new Color(0.13f, 0.18f, 0.2f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            SetReference(camera.gameObject.AddComponent<TopDownCamera>(), "target", player.transform);
            SetReference(player.AddComponent<PlayerMovement>(), "viewCamera", camera);
            player.AddComponent<PlayerInteraction>();

            var shop = new GameObject("Temporary Shop");
            shop.transform.position = new Vector3(0, 0, 1);
            SetReference(shop.AddComponent<Shopkeeper>(), "catalog", catalog);
            GameObject desk = Primitive("Counter", PrimitiveType.Cube, new Vector3(0, 0.55f, 1.8f), new Vector3(2.8f, 1.1f, 0.8f), counter);
            desk.transform.SetParent(shop.transform, true);
            GameObject merchant = Primitive("Merchant", PrimitiveType.Capsule, new Vector3(0, 1, 2.7f), new Vector3(0.8f, 1, 0.8f), merchantMaterial);
            merchant.transform.SetParent(shop.transform, true);
            var trigger = shop.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 0.5f;
            trigger.size = new Vector3(2.8f, 1, 1.5f);
            var sign = new GameObject("SHOP sign").AddComponent<TextMesh>();
            sign.transform.SetParent(shop.transform, false);
            sign.transform.localPosition = new Vector3(0, 3, 0.8f);
            sign.transform.rotation = Quaternion.Euler(50, 0, 0);
            sign.text = "SHOP\n[E] Trade";
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.fontSize = 60;
            sign.characterSize = 0.06f;
            sign.color = new Color(0.95f, 0.85f, 0.6f);

            PrefabUtility.SaveAsPrefabAsset(shop, Folder + "/Demo/TemporaryShop.prefab");
            SetReference(new GameObject("Shop Demo").AddComponent<ShopDemo>(), "inventory", inventory);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SHOP_DEBUG_CREATED " + ScenePath);
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string path = Folder + "/Demo/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = name;
            result.transform.position = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        internal static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
