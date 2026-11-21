using System.IO;
using Nomad.Monsters.Demo;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Nomad.Monsters.Editor
{
    public static class MonsterTrackingAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Monsters/MonsterTracking.unity";

        [MenuItem("Nomad/Monsters/Create Tracking Preview")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var terrain = new GameObject("FixedTestPath");
            terrain.AddComponent<TrackingNavigation>();
            AddBox(terrain.transform, "Lower", new Vector3(0f, -0.25f, 0f), new Vector3(10f, 0.5f, 10f));
            AddBox(terrain.transform, "Upper", new Vector3(0f, 1.75f, 14f), new Vector3(10f, 0.5f, 10f));

            for (int i = 0; i < 8; i++)
                AddBox(terrain.transform, "Step" + i,
                    new Vector3(0f, (i + 1) * 0.25f - 0.125f, 5.25f + i * 0.5f),
                    new Vector3(4f, 0.25f, 0.5f));

            // 끊긴 경로와 시야 차단을 정상 이동 경로와 분리해 검증합니다.
            AddBox(terrain.transform, "Isolated", new Vector3(13f, -0.25f, 0f), new Vector3(4f, 0.5f, 4f));
            AddBox(terrain.transform, "SightWall", new Vector3(-3f, 1.5f, 0f), new Vector3(0.4f, 3f, 3f));

            GameObject player = CreateActor("Player", new Vector3(0f, 0.05f, 2f));
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            PlayerMovement movement = player.AddComponent<PlayerMovement>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            SetReference(movement, "viewCamera", camera);
            SetReference(cameraObject.AddComponent<TopDownCamera>(), "target", player.transform);

            GameObject monster = CreateActor("Monster", new Vector3(0f, 0f, -2f));
            NavMeshAgent agent = monster.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 3.5f;
            agent.stoppingDistance = 1.2f;
            agent.angularSpeed = 360f;
            SetReference(monster.AddComponent<MonsterTracker>(), "target", player.transform);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("MONSTER_AUTHORING_PASS: " + ScenePath);
        }

        private static GameObject CreateActor(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f;
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            return root;
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
