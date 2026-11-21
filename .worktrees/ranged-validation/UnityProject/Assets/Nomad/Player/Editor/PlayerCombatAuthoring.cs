using Nomad.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Player.Editor
{
    internal static class PlayerCombatAuthoring
    {
        internal const string ScenePath = "Assets/Nomad/Player/A_PlayerCombat.unity";
        private const string ProjectilePath = "Assets/Nomad/Combat/PlayerProjectile.prefab";

        [MenuItem("Nomad/Player/Create Combat Demo")]
        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform player = new GameObject("Player").transform;
            CharacterController controller = player.gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0f;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Player Body";
            body.transform.SetParent(player, false);
            body.transform.localPosition = controller.center;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player, false);
            muzzle.localPosition = new Vector3(0f, 1.2f, 0.45f);

            Camera camera = new GameObject("Top Down Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            SetReference(camera.gameObject.AddComponent<TopDownCamera>(), "target", player);
            SetReference(player.gameObject.AddComponent<PlayerMovement>(), "viewCamera", camera);

            PlayerCombat combat = player.gameObject.AddComponent<PlayerCombat>();
            SetReference(combat, "viewCamera", camera);
            SetReference(combat, "muzzle", muzzle);
            SetReference(combat, "projectile", CreateProjectile());

            CreateBlock("Ground", new Vector3(0f, -0.5f, 2f), new Vector3(20f, 1f, 18f));
            CreateBlock("High Platform", new Vector3(-3f, 1f, 6f), new Vector3(2f, 2f, 2f));
            CreateTarget("High Target", new Vector3(-3f, 2.75f, 6f));
            CreateTarget("Low Target", new Vector3(3f, 0.65f, 6f));
            CreateTarget("Shielded Target", new Vector3(0f, 1.25f, 6f));
            CreateBlock("Thin Wall", new Vector3(0f, 0.75f, 2f), new Vector3(1.6f, 1.5f, 0.04f));

            Light sunlight = new GameObject("Sun").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.shadows = LightShadows.Soft;
            sunlight.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Projectile CreateProjectile()
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Player Projectile";
            projectile.transform.localScale = new Vector3(0.12f, 0.12f, 0.24f);
            Object.DestroyImmediate(projectile.GetComponent<Collider>());
            projectile.AddComponent<Projectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePath);
            Object.DestroyImmediate(projectile);
            return prefab.GetComponent<Projectile>();
        }

        private static void CreateTarget(string name, Vector3 position)
        {
            GameObject target = CreateBlock(name, position, new Vector3(0.8f, 1.3f, 0.8f));
            Health health = target.AddComponent<Health>();
            SetReference(target.AddComponent<CombatDemoTarget>(), "health", health);
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 size)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = size;
            return block;
        }

        private static void SetReference(Object component, string property, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
