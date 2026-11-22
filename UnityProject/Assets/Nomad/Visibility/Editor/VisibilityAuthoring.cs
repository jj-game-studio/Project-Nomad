using System.IO;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Visibility.Editor
{
    public static class VisibilityAuthoring
    {
        public const string Folder = "Assets/Nomad/Visibility";
        public const string ScenePath = Folder + "/VisibilityDemo.unity";
        public const string FieldPath = Folder + "/VisibilityField.unity";

        [MenuItem("Nomad/Visibility/Open Visibility Demo")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildDemo();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/Visibility/Build Visibility Demo")]
        public static void BuildDemo()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material floor = MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.22f));
            Material wall = MaterialAsset("Wall", new Color(0.35f, 0.38f, 0.42f));
            Material green = MaterialAsset("Foliage", new Color(0.15f, 0.5f, 0.16f));
            Material blue = MaterialAsset("Player", new Color(0.2f, 0.65f, 0.95f));
            Material red = MaterialAsset("Enemy", new Color(0.85f, 0.25f, 0.12f));
            Primitive("Ground", PrimitiveType.Cube, new Vector3(0f, -0.25f, 4f), new Vector3(36f, 0.5f, 36f), floor);
            Primitive("Wall", PrimitiveType.Cube, new Vector3(-4f, 1.5f, 0f), new Vector3(5f, 3f, 0.6f), wall);
            Primitive("Low cover", PrimitiveType.Cube, new Vector3(4f, 0.3f, 3f), new Vector3(3f, 0.6f, 0.5f), wall);

            GameObject player = Primitive("Player", PrimitiveType.Capsule, new Vector3(0f, 1f, -5f), Vector3.one, blue);
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.transform.position = new Vector3(0f, 0.1f, -5f);
            player.GetComponent<Renderer>().enabled = false;
            GameObject body = Primitive("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.7f, 0.9f, 0.7f), blue);
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            player.AddComponent<CharacterController>().center = Vector3.up;
            player.AddComponent<Health>();
            player.AddComponent<PlayerMovement>();

            Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            var follow = camera.gameObject.AddComponent<TopDownCamera>();
            var settings = new SerializedObject(follow);
            settings.FindProperty("target").objectReferenceValue = player.transform;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var movement = new SerializedObject(player.GetComponent<PlayerMovement>());
            movement.FindProperty("viewCamera").objectReferenceValue = camera;
            movement.ApplyModifiedPropertiesWithoutUndo();

            var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = Color.gray;

            var sight = new GameObject("Player Visibility").AddComponent<PlayerVisibility>();
            sight.Configure(player.transform, camera);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/FoliageCutaway.shader");
            sight.gameObject.AddComponent<VisibilityField>().Configure(sight, shader);

            Enemy("Visible enemy", new Vector3(3f, 0f, 1f), red);
            Enemy("Behind wall", new Vector3(-4f, 0f, 3f), red);
            Enemy("Behind player", new Vector3(0f, 0f, -10f), red);
            Enemy("Inside foliage", new Vector3(5f, 0f, 2f), red);
            GameObject bush = Primitive("Bush cutaway sample", PrimitiveType.Sphere, new Vector3(5f, 1.8f, 2f),
                new Vector3(5f, 3.6f, 5f), green);
            Object.DestroyImmediate(bush.GetComponent<Collider>());
            bush.transform.position = new Vector3(5f, 0f, 2f);
            GameObject crown = Primitive("Leaves", PrimitiveType.Sphere, Vector3.zero, new Vector3(5f, 3.6f, 5f), green);
            crown.transform.SetParent(bush.transform, false);
            crown.transform.localPosition = Vector3.up * 1.8f;
            bush.transform.localScale = Vector3.one;
            bush.GetComponent<Renderer>().enabled = false;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            bush.AddComponent<VisibilityFoliage>().Configure(shader);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("VISIBILITY_DEMO_CREATED");
        }

        // 원본 통합 씬을 수정하지 않고 복사본에서 생성 필드/몬스터 연결을 검토합니다.
        [MenuItem("Nomad/Visibility/Build Field Preview")]
        public static void BuildField()
        {
            EditorSceneManager.OpenScene("Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), FieldPath);
            var controls = Object.FindFirstObjectByType<TerrainIntegrationControls>();
            var sight = new GameObject("Player Visibility").AddComponent<PlayerVisibility>();
            sight.Configure(controls.Player.transform, Camera.main);
            sight.gameObject.AddComponent<VisibilityField>().Configure(sight,
                AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/FoliageCutaway.shader"));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("VISIBILITY_FIELD_CREATED");
        }

        [MenuItem("Nomad/Visibility/Open Field Preview")]
        public static void OpenField()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(FieldPath)) BuildField();
            else EditorSceneManager.OpenScene(FieldPath);
        }

        private static void Enemy(string name, Vector3 position, Material material)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<Health>();
            root.AddComponent<MonsterIdentity>();
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up;
            capsule.height = 2f;
            capsule.radius = 0.35f;
            GameObject body = Primitive("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.7f, 0.9f, 0.7f), material);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            root.AddComponent<SegmentedHealthBar>();
            root.AddComponent<VisibilityTarget>();
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static Material MaterialAsset(string name, Color color)
        {
            Directory.CreateDirectory(Folder + "/Materials");
            string path = Folder + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

