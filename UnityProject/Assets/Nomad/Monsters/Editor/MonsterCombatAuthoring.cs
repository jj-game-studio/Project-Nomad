using System.IO;
using Nomad.Combat;
using Nomad.Monsters.Demo;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Nomad.Monsters.Editor
{
    public static class MonsterCombatAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Monsters/MonsterCombat.unity";
        public const string EliteScenePath = "Assets/Nomad/Monsters/MonsterElite.unity";
        private const string Folder = "Assets/Nomad/Monsters/Demo/CombatMaterials";

        [MenuItem("Nomad/Monsters/Create Combat Preview")]
        public static void Create()
        {
            CreateScene(false);
        }

        [MenuItem("Nomad/Monsters/Create Elite Preview")]
        public static void CreateElite()
        {
            CreateScene(true);
        }

        private static void CreateScene(bool elite)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material blue = Material("Player", new Color(0.15f, 0.5f, 0.95f));
            Material green = elite ? Material("Elite", new Color(0.6f, 0.25f, 0.8f)) :
                Material("Monster", new Color(0.35f, 0.7f, 0.2f));
            Material stone = Material("Ground", new Color(0.3f, 0.35f, 0.4f));
            var ground = new GameObject("Combat Ground");
            ground.AddComponent<TrackingNavigation>();
            Box(ground.transform, "Floor", new Vector3(0f, -0.25f, 2f), new Vector3(16f, 0.5f, 20f), stone);
            Box(ground.transform, "Wall", new Vector3(-3f, 1.5f, 0f), new Vector3(0.4f, 3f, 3f), stone);

            GameObject player = Actor("Player", new Vector3(0f, 0.05f, 2f), blue);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0f;
            Health playerHealth = player.AddComponent<Health>();
            Camera camera = new GameObject("Combat Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            Reference(camera.gameObject.AddComponent<TopDownCamera>(), "target", player.transform);
            Reference(player.AddComponent<PlayerMovement>(), "viewCamera", camera);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.2f, 0.45f);
            PlayerCombat shooting = player.AddComponent<PlayerCombat>();
            Reference(shooting, "viewCamera", camera);
            Reference(shooting, "muzzle", muzzle);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nomad/Combat/PlayerProjectile.prefab");
            Reference(shooting, "projectile", projectilePrefab.GetComponent<Projectile>());

            GameObject monster = Actor("Monster", new Vector3(0f, 0f, -2f), green);
            var collider = monster.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.35f;
            collider.center = Vector3.up * 0.9f;
            NavMeshAgent agent = monster.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 3.5f;
            agent.stoppingDistance = 1.2f;
            agent.angularSpeed = 360f;
            Health monsterHealth = monster.AddComponent<Health>();
            if (elite)
            {
                SetValue(monsterHealth, "maxHealth", 250);
                Transform body = monster.transform.Find("Body");
                body.localScale = new Vector3(0.875f, 0.99f, 0.875f);
                body.localPosition = Vector3.up * 0.99f;
            }
            Reference(monster.AddComponent<MonsterTracker>(), "target", player.transform);
            MonsterMeleeAttack melee = monster.AddComponent<MonsterMeleeAttack>();
            Reference(melee, "target", playerHealth);
            if (elite)
                SetValue(melee, "enableHeavyAttack", true);

            var demo = new GameObject("Combat Guide").AddComponent<MonsterCombatDemo>();
            Reference(demo, "player", playerHealth);
            Reference(demo, "monster", monsterHealth);
            Reference(demo, "attack", melee);
            Reference(demo, "monsterBody", monster.GetComponentInChildren<Renderer>());
            if (elite)
            {
                SetValue(demo, "opponentLabel", "ELITE: 2 light -> 1 heavy");
                var warning = new GameObject("Heavy Warning").AddComponent<LineRenderer>();
                warning.useWorldSpace = true;
                warning.positionCount = 19;
                warning.startWidth = warning.endWidth = 0.06f;
                warning.sharedMaterial = Material("HeavyWarning", new Color(1f, 0.15f, 0.05f),
                    "Universal Render Pipeline/Unlit");
                warning.enabled = false;
                Reference(demo, "heavyWarning", warning);
            }
            Light light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            EditorSceneManager.SaveScene(scene, elite ? EliteScenePath : ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("MONSTER_COMBAT_AUTHORING_PASS");
        }

        [MenuItem("Nomad/Monsters/Open Playable Combat")]
        public static void OpenForPlay()
        {
            OpenSceneForPlay(ScenePath);
        }

        [MenuItem("Nomad/Monsters/Open Playable Elite")]
        public static void OpenEliteForPlay()
        {
            OpenSceneForPlay(EliteScenePath);
        }

        private static void OpenSceneForPlay(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(path);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                SessionState.SetBool("Nomad.Monsters.CombatPreview", true);
                ResumePreview();
                EditorApplication.EnterPlaymode();
            };
        }

        [InitializeOnLoadMethod]
        private static void ResumePreview()
        {
            EditorApplication.update -= CapturePreview;
            if (SessionState.GetBool("Nomad.Monsters.CombatPreview", false))
                EditorApplication.update += CapturePreview;
        }

        private static void CapturePreview()
        {
            if (!EditorApplication.isPlaying || Time.time < 1f)
                return;

            EditorApplication.update -= CapturePreview;
            SessionState.SetBool("Nomad.Monsters.CombatPreview", false);
            Directory.CreateDirectory("Artifacts");
            bool elite = SceneManager.GetActiveScene().path == EliteScenePath;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(elite ?
                "Artifacts/monster-elite.png" : "Artifacts/monster-combat.png"));
            Debug.Log(elite ? "MONSTER_ELITE_READY" : "MONSTER_COMBAT_READY");
        }

        private static GameObject Actor(string name, Vector3 position, Material material)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            return root;
        }

        private static void Box(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Material(string name, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            string path = $"{Folder}/{name}.mat";
            Material value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null)
            {
                value = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(value, path);
            }
            value.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(value);
            return value;
        }

        private static void Reference(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetValue(Object component, string field, object value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (value is bool enabled)
                property.boolValue = enabled;
            else if (value is int number)
                property.intValue = number;
            else if (value is string text)
                property.stringValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
