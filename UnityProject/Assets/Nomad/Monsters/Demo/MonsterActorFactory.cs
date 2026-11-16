using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Nomad.Monsters.Demo
{
    // 테스트 스폰창과 생성 필드가 같은 전투 개체를 생성하도록 공유합니다.
    public sealed class MonsterActorFactory : MonoBehaviour
    {
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material eliteMaterial;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material fillMaterial;

        public void ConfigureMaterials(Material normal, Material elite, Material warning, Material fill)
        {
            normalMaterial = normal;
            eliteMaterial = elite;
            warningMaterial = warning;
            fillMaterial = fill;
        }

        public Health Create(Health target, MonsterTestSettings config, bool elite, Vector3 position,
            Quaternion rotation, string actorName, Transform parent = null)
        {
            var root = new GameObject(actorName);
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.SetActive(false);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, rotation);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * (elite ? 0.99f : 0.9f);
            body.transform.localScale = elite ? new Vector3(0.875f, 0.99f, 0.875f) : new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Collider>().enabled = false;
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = elite ? eliteMaterial : normalMaterial;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = Vector3.up * 0.9f;
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = config.moveSpeed;
            agent.stoppingDistance = Mathf.Min(1.2f, config.attackRange * 0.7f);
            agent.angularSpeed = 360f;

            Health health = root.AddComponent<Health>();
            JsonUtility.FromJsonOverwrite("{\"maxHealth\":" + config.health + "}", health);
            root.AddComponent<SegmentedHealthBar>();
            root.AddComponent<MonsterHitFeedback>();
            MonsterTracker tracker = root.AddComponent<MonsterTracker>();
            tracker.BindTarget(target.transform);
            tracker.Configure(config.sightRange, config.abandonRange, config.homeRange);
            MonsterMeleeAttack attack = root.AddComponent<MonsterMeleeAttack>();
            attack.Configure(target, elite, config.damage, config.heavyDamage,
                config.attackRange, config.heavyRange, config.attackSpeed);

            LineRenderer outline = new GameObject("Attack Outline").AddComponent<LineRenderer>();
            outline.transform.SetParent(root.transform, false);
            outline.useWorldSpace = true;
            outline.widthMultiplier = 0.06f;
            outline.sharedMaterial = warningMaterial;
            MeshFilter fill = new GameObject("Attack Fill").AddComponent<MeshFilter>();
            fill.transform.SetParent(root.transform, false);
            fill.gameObject.AddComponent<MeshRenderer>().sharedMaterial = fillMaterial;
            root.AddComponent<MonsterCombatDemo>().ConfigureDisplay(target, health, attack,
                body.GetComponent<Renderer>(), outline, fill);
            root.SetActive(true);
            return health;
        }
    }
}
