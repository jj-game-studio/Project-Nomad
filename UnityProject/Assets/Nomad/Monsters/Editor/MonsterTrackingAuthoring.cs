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
        private const string MaterialFolder = "Assets/Nomad/Monsters/Demo/Materials";

        [MenuItem("Nomad/Monsters/Create Tracking Preview")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            Material ground = CreateMaterial("Ground", new Color(0.23f, 0.3f, 0.35f));
            Material stairs = CreateMaterial("Stairs", new Color(0.85f, 0.63f, 0.22f));
            Material wall = CreateMaterial("Wall", new Color(0.65f, 0.24f, 0.18f));
            Material blue = CreateMaterial("Player", new Color(0.16f, 0.55f, 0.95f));
            Material green = CreateMaterial("Monster", new Color(0.37f, 0.65f, 0.19f));
            Material bone = CreateMaterial("Bone", new Color(0.95f, 0.88f, 0.63f));
            Material eyes = CreateMaterial("Eyes", new Color(0.95f, 0.28f, 0.12f));
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
            foreach (Renderer surface in terrain.GetComponentsInChildren<Renderer>())
                surface.sharedMaterial = surface.name.StartsWith("Step") ? stairs :
                    surface.name == "SightWall" ? wall : ground;

            GameObject player = CreateActor("Player", new Vector3(0f, 0.05f, 2f));
            player.GetComponentInChildren<Renderer>().sharedMaterial = blue;
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
            monster.GetComponentInChildren<Renderer>().sharedMaterial = green;
            AddVisual(monster.transform, "Head", PrimitiveType.Sphere,
                new Vector3(0f, 1.4f, 0.18f), Vector3.one * 0.65f, green);
            AddVisual(monster.transform, "LeftEye", PrimitiveType.Sphere,
                new Vector3(-0.16f, 1.47f, 0.45f), Vector3.one * 0.12f, eyes);
            AddVisual(monster.transform, "RightEye", PrimitiveType.Sphere,
                new Vector3(0.16f, 1.47f, 0.45f), Vector3.one * 0.12f, eyes);
            AddVisual(monster.transform, "LeftHorn", PrimitiveType.Cube,
                new Vector3(-0.24f, 1.78f, 0.1f), new Vector3(0.14f, 0.35f, 0.14f), bone);
            AddVisual(monster.transform, "RightHorn", PrimitiveType.Cube,
                new Vector3(0.24f, 1.78f, 0.1f), new Vector3(0.14f, 0.35f, 0.14f), bone);
            NavMeshAgent agent = monster.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 3.5f;
            agent.stoppingDistance = 1.2f;
            agent.angularSpeed = 360f;
            MonsterTracker tracker = monster.AddComponent<MonsterTracker>();
            SetReference(tracker, "target", player.transform);
            var guide = new GameObject("Playground Guide").AddComponent<MonsterPlaygroundGuide>();
            SetReference(guide, "player", player.transform);
            SetReference(guide, "monster", tracker);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("MONSTER_AUTHORING_PASS: " + ScenePath);
        }

        [MenuItem("Nomad/Monsters/Open Playable Playground")]
        public static void OpenForPlay()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                SessionState.SetBool("Nomad.Monsters.CapturePreview", true);
                ResumePreview();
                EditorApplication.EnterPlaymode();
            };
        }

        [InitializeOnLoadMethod]
        private static void ResumePreview()
        {
            EditorApplication.update -= CapturePreview;
            if (SessionState.GetBool("Nomad.Monsters.CapturePreview", false))
                EditorApplication.update += CapturePreview;
        }

        private static void CapturePreview()
        {
            if (!EditorApplication.isPlaying || Time.time < 1f)
                return;

            EditorApplication.update -= CapturePreview;
            SessionState.SetBool("Nomad.Monsters.CapturePreview", false);
            Directory.CreateDirectory("Artifacts");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/monster-playground.png"));
            Debug.Log("MONSTER_PLAYGROUND_READY: 파란 플레이어 / 초록 임시 몬스터 / R 초기화");
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
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

        private static void AddVisual(Transform root, string name, PrimitiveType shape,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            visual.transform.SetParent(root, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
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
