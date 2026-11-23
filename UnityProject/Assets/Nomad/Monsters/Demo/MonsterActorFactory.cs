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
        [SerializeField] private MonsterAppearanceSet appearances;

        public void ConfigureAppearances(MonsterAppearanceSet value) => appearances = value;

        public void ConfigureMaterials(Material normal, Material elite, Material warning, Material fill)
        {
            normalMaterial = normal;
            eliteMaterial = elite;
            warningMaterial = warning;
            fillMaterial = fill;
        }

        public Health Create(Health target, MonsterTestSettings config, bool elite, Vector3 position,
            Quaternion rotation, string actorName, Transform parent = null)
            => CreateRanked(target, config, elite ? MonsterRank.Elite : MonsterRank.Normal,
                position, rotation, actorName, parent);

        public Health CreateRanked(Health target, MonsterTestSettings config, MonsterRank rank, Vector3 position,
            Quaternion rotation, string actorName, Transform parent = null)
        {
            bool elite = rank != MonsterRank.Normal;
            var root = new GameObject(actorName);
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.SetActive(false);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, rotation);
            GameObject prefab = rank == MonsterRank.Boss ? appearances?.boss : elite ? appearances?.elite :
                appearances != null && appearances.normal.Length > 0 ? appearances.normal[
                    (config.normalStyle >= 0 ? config.normalStyle : Variant(actorName)) % appearances.normal.Length] : null;
            MonsterWeaponStyle style = prefab != null && prefab.name.Contains("Mage") ? MonsterWeaponStyle.Staff :
                prefab != null && prefab.name.Contains("Warrior") ? MonsterWeaponStyle.Sword : MonsterWeaponStyle.Crossbow;
            GameObject body = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            if (prefab == null)
            {
                body.transform.localPosition = Vector3.up * (elite ? 0.99f : 0.9f);
                body.transform.localScale = elite ? new Vector3(0.875f, 0.99f, 0.875f) : new Vector3(0.7f, 0.9f, 0.7f);
                body.GetComponent<Collider>().enabled = false;
                Destroy(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = elite ? eliteMaterial : normalMaterial;
            }
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = rank == MonsterRank.Boss ? 3.8f : rank == MonsterRank.Elite ? 3.2f : 1.8f;
            capsule.radius = 0.35f;
            capsule.center = Vector3.up * (capsule.height * 0.5f);
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = config.moveSpeed;
            agent.stoppingDistance = Mathf.Min(1.2f, config.attackRange * 0.7f);
            agent.angularSpeed = 540f;
            agent.acceleration = rank == MonsterRank.Normal ? 20f : 28f;

            Health health = root.AddComponent<Health>();
            JsonUtility.FromJsonOverwrite("{\"maxHealth\":" + config.health + "}", health);
            MonsterIdentity identity = root.AddComponent<MonsterIdentity>();
            identity.Configure(rank);
            identity.Weapon = style;
            SegmentedHealthBar bar = root.AddComponent<SegmentedHealthBar>();
            bar.Boss = rank == MonsterRank.Boss;
            bar.WorldOffset = Vector3.up * (rank == MonsterRank.Elite ? 4.1f : 2.4f);
            root.AddComponent<MonsterDeathDrop>().Configure(parent != null ? parent : transform);
            root.AddComponent<MonsterEquipmentLoot>().Configure(target.GetComponent<Nomad.Inventory.PlayerInventory>());
            root.AddComponent<MonsterHitFeedback>();
            if (prefab != null) root.AddComponent<MonsterAnimationDriver>();
            MonsterTracker tracker = root.AddComponent<MonsterTracker>();
            tracker.BindTarget(target.transform);
            tracker.Configure(config.sightRange, config.abandonRange, config.homeRange);
            if (rank != MonsterRank.Normal && config.skills)
                root.AddComponent<MonsterTactics>().Configure(target, config.chargeMin, config.chargeMax,
                    config.chargeSpeed, config.chargeCooldown, config.areaRadius, config.areaCooldown,
                    config.skillWindup, config.skillRecovery, config.heavyDamage);
            if (config.ranged && appearances != null && (rank != MonsterRank.Normal || style != MonsterWeaponStyle.Sword))
            {
                GameObject bolt = rank == MonsterRank.Boss ? appearances.fireBolt : rank == MonsterRank.Elite ?
                    appearances.rockBolt : prefab != null && prefab.name.Contains("Mage") ? appearances.magicBolt : appearances.arrow;
                root.AddComponent<MonsterRangedAttack>().Configure(bolt, config.rangedRange, config.projectileSpeed,
                    config.rangedDamage, config.rangedWindup, config.rangedRecovery,
                    new Vector3(0f, rank == MonsterRank.Boss ? 1.8f : rank == MonsterRank.Elite ? 2.2f : 1.3f, 0.5f));
                if (rank == MonsterRank.Normal)
                    root.GetComponent<MonsterRangedAttack>().WeaponMuzzle = System.Array.Find(
                        body.GetComponentsInChildren<Transform>(), part => part.name == "WeaponMuzzle");
            }
            MonsterMeleeAttack attack = root.AddComponent<MonsterMeleeAttack>();
            attack.Configure(target, elite && config.skills, config.damage, config.heavyDamage,
                config.attackRange, config.heavyRange, config.attackSpeed);
            attack.ConfigureTempo(config.basicWindup, config.basicRecovery, config.skillWindup, config.skillRecovery);
            attack.ConfigureBasicAngle(rank == MonsterRank.Boss ? 120f : rank == MonsterRank.Elite ? 140f : 360f);
            if (rank == MonsterRank.Elite && prefab != null) root.AddComponent<GolemMotion>();
            if (rank == MonsterRank.Boss && prefab != null) root.AddComponent<DragonLegMotion>();
            if (rank == MonsterRank.Normal && prefab != null) root.AddComponent<MonsterWeaponMotion>();

            LineRenderer outline = new GameObject("Attack Outline").AddComponent<LineRenderer>();
            outline.transform.SetParent(root.transform, false);
            outline.useWorldSpace = true;
            outline.widthMultiplier = 0.06f;
            outline.sharedMaterial = warningMaterial;
            MeshFilter fill = new GameObject("Attack Fill").AddComponent<MeshFilter>();
            fill.transform.SetParent(root.transform, false);
            fill.gameObject.AddComponent<MeshRenderer>().sharedMaterial = fillMaterial;
            root.AddComponent<MonsterCombatDemo>().ConfigureDisplay(target, health, attack,
                body.GetComponentInChildren<Renderer>(), outline, fill);
            root.SetActive(true);
            return health;
        }

        private static int Variant(string name)
        {
            uint hash = 2166136261;
            foreach (char value in name) hash = (hash ^ value) * 16777619;
            return (int)(hash & 0x7fffffff);
        }
    }
}
