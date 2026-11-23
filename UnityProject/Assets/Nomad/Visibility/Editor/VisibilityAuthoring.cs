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
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material floor = MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.22f));
            Material wall = MaterialAsset("Wall", new Color(0.35f, 0.38f, 0.42f));
            Material green = MaterialAsset("Foliage", new Color(0.15f, 0.5f, 0.16f));
            Material blue = MaterialAsset("Player", new Color(0.2f, 0.65f, 0.95f));
            Material red = MaterialAsset("Enemy", new Color(0.85f, 0.25f, 0.12f));
            Primitive("Ground", PrimitiveType.Cube, new Vector3(0f, -0.25f, 4f), new Vector3(36f, 0.5f, 36f), floor);
            Primitive("Wall", PrimitiveType.Cube, new Vector3(-4f, 1.5f, 0f), new Vector3(5f, 3f, 0.6f), wall);
            Primitive("Low cover", PrimitiveType.Cube, new Vector3(4f, 0.3f, 3f), new Vector3(3f, 0.6f, 0.5f), wall);

            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.1f, -5f);
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
            Shader shader = FadeShader;
            sight.gameObject.AddComponent<VisibilityField>().Configure(sight, shader, ShadeShader);

            Enemy("Visible enemy", new Vector3(3f, 0f, 1f), red);
            Enemy("Behind wall", new Vector3(-4f, 0f, 3f), red);
            Enemy("Behind player", new Vector3(0f, 0f, -10f), red);
            Enemy("Inside foliage", new Vector3(5f, 0f, 2f), red);
            GameObject bush = new GameObject("Bush cutaway sample");
            bush.transform.position = new Vector3(5f, 0f, 2f);
            GameObject crown = Primitive("Leaves", PrimitiveType.Sphere, Vector3.zero, new Vector3(5f, 3.6f, 5f), green);
            crown.transform.SetParent(bush.transform, false);
            crown.transform.localPosition = Vector3.up * 1.8f;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            bush.AddComponent<VisibilityFoliage>().Configure(shader);

            GameObject tree = new GameObject("Tree cutaway sample");
            tree.transform.position = new Vector3(-6f, 0f, -1f);
            GameObject trunk = Primitive("Trunk", PrimitiveType.Cube, Vector3.zero, new Vector3(0.4f, 2f, 0.4f),
                MaterialAsset("Trunk", new Color(0.4f, 0.22f, 0.1f)));
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = Vector3.up;
            GameObject leaves = Primitive("Leaves", PrimitiveType.Sphere, Vector3.zero, new Vector3(4f, 4f, 4f), green);
            leaves.transform.SetParent(tree.transform, false);
            leaves.transform.localPosition = Vector3.up * 3f;
            Object.DestroyImmediate(leaves.GetComponent<Collider>());
            tree.AddComponent<VisibilityFoliage>().Configure(shader);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            BuildModule();
            AssetDatabase.SaveAssets();
            Debug.Log("VISIBILITY_DEMO_CREATED");
        }

        // 원본 통합 씬을 수정하지 않고 복사본에서 생성 필드/몬스터 연결을 검토합니다.
        [MenuItem("Nomad/Visibility/Build Field Preview")]
        public static void BuildField()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), FieldPath);
            var controls = Object.FindFirstObjectByType<TerrainIntegrationControls>();
            var sight = new GameObject("Player Visibility").AddComponent<PlayerVisibility>();
            sight.Configure(controls.Player.transform, Camera.main);
            sight.gameObject.AddComponent<VisibilityField>().Configure(sight, FadeShader, ShadeShader);
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

        [MenuItem("Nomad/Visibility/Build Visibility Module")]
        public static void BuildModule()
        {
            var module = new GameObject("PlayerVisibilityModule");
            var sight = module.AddComponent<PlayerVisibility>();
            module.AddComponent<VisibilityField>().Configure(sight, FadeShader, ShadeShader);
            module.AddComponent<VisibilityBinding>();
            PrefabUtility.SaveAsPrefabAsset(module, Folder + "/PlayerVisibilityModule.prefab");
            Object.DestroyImmediate(module);
        }

        private static Shader FadeShader => AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/FoliageFade.shader");
        private static Shader ShadeShader => AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/VisibilityShade.shader");

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
