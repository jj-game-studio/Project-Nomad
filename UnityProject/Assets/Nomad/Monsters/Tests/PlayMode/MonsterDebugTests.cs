#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Nomad.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterDebugTests
    {
        private Scene previous;
        private Scene scene;
        private MonsterIdentity[] actors;
        private MonoBehaviour panel;
        private Health player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterDebug.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return null;
            actors = scene.GetRootGameObjects().Select(root => root.GetComponent<MonsterIdentity>()).Where(a => a != null).ToArray();
            GameObject controls = scene.GetRootGameObjects().First(root => root.name == "Test Controls");
            panel = (MonoBehaviour)controls.GetComponent(Type.GetType("Nomad.Monsters.Demo.MonsterTestPanel, Assembly-CSharp"));
            player = scene.GetRootGameObjects().First(root => root.name == "Player").GetComponent<Health>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator DebugLineupHasThreeWeaponStylesTwoLargeMonstersAndFaintDetection()
        {
            Assert.That(actors.Length, Is.EqualTo(5));
            Assert.That(actors.Count(a => a.Rank == MonsterRank.Normal), Is.EqualTo(3));
            foreach (MonsterWeaponStyle style in Enum.GetValues(typeof(MonsterWeaponStyle)))
                Assert.That(actors.Count(a => a.Rank == MonsterRank.Normal && a.Weapon == style), Is.EqualTo(1));
            Assert.That(actors.Count(a => a.Rank == MonsterRank.Elite), Is.EqualTo(1));
            Assert.That(actors.Count(a => a.Rank == MonsterRank.Boss), Is.EqualTo(1));
            Assert.That(player.IsCombatImmune, Is.True);
            foreach (MonsterIdentity actor in actors)
            {
                Transform disc = actor.transform.Find("Faint detection area");
                Assert.That(disc != null && disc.gameObject.activeInHierarchy, Is.True);
                Assert.That(disc.GetComponent<Collider>(), Is.Null);
                var block = new MaterialPropertyBlock();
                disc.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor").a, Is.LessThan(0.05f));
            }
            player.transform.position = new Vector3(0f, 0.05f, 4f);
            Component arena = panel.GetComponent(Type.GetType("Nomad.Monsters.Demo.MonsterDebugArena, Assembly-CSharp"));
            arena.GetType().GetMethod("ResetLineup").Invoke(arena, null);
            yield return null;
            Assert.That(scene.GetRootGameObjects().Count(root => root.GetComponent<MonsterIdentity>() != null), Is.EqualTo(5),
                "reset must not lose a type when player occupied its spawn point");
        }

        [UnityTest]
        public IEnumerator BasicAttacksHaveNoFloorWarningButEliteAndBossRangedSkillsDo()
        {
            foreach (MonsterIdentity actor in actors) actor.gameObject.SetActive(false);
            foreach (MonsterIdentity actor in actors)
            {
                actor.gameObject.SetActive(true);
                actor.GetComponent<NavMeshAgent>().Warp(new Vector3(3f, 0f, -2f));
                actor.GetComponent<NavMeshAgent>().speed = 0f;
                actor.GetComponent<MonsterTracker>().Configure(20f, 30f, 0f);
                bool sword = actor.Rank == MonsterRank.Normal && actor.Weapon == MonsterWeaponStyle.Sword;
                player.transform.position = new Vector3(3f, 0f, sword ? -0.6f : 4f);
                Physics.SyncTransforms();
                var attack = actor.GetComponent<MonsterMeleeAttack>();
                float deadline = Time.time + 4f;
                while ((attack.Phase != MeleePhase.Windup || attack.WindupProgress < 0.3f) && Time.time < deadline) yield return null;
                Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Windup));
                yield return null;
                bool skill = actor.Rank != MonsterRank.Normal;
                Assert.That(attack.IsSkillAttack, Is.EqualTo(skill));
                Assert.That(actor.GetComponentsInChildren<LineRenderer>().Single(line => line.name == "Attack Outline").enabled,
                    Is.EqualTo(skill), actor.DisplayName);
                actor.gameObject.SetActive(false);
            }
        }

        [UnityTest]
        public IEnumerator GolemHandsStayInFrontDuringIdleAndThrowWindup()
        {
            MonsterIdentity golem = actors.Single(actor => actor.Rank == MonsterRank.Elite);
            foreach (MonsterIdentity actor in actors) if (actor != golem) actor.gameObject.SetActive(false);
            Transform[] bones = golem.GetComponentsInChildren<Transform>();
            Transform[] hands = { bones.Single(t => t.name == "hand.L"), bones.Single(t => t.name == "hand.R") };
            Transform[] shoulders = { bones.Single(t => t.name == "upper_arm.L"), bones.Single(t => t.name == "upper_arm.R") };
            for (int i = 0; i < 2; i++)
                Assert.That(Vector3.Dot(hands[i].position - shoulders[i].position, golem.transform.forward), Is.GreaterThan(0.1f));
            player.transform.position = golem.transform.position + golem.transform.forward * 6f;
            golem.GetComponent<NavMeshAgent>().speed = 0f;
            golem.GetComponent<MonsterTracker>().Configure(20f, 30f, 0f);
            Physics.SyncTransforms();
            var attack = golem.GetComponent<MonsterMeleeAttack>();
            float deadline = Time.time + 4f;
            while (attack.Phase != MeleePhase.Windup && Time.time < deadline) yield return null;
            Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Windup));
            for (int frame = 0; frame < 90; frame++)
            {
                yield return null;
                for (int i = 0; i < 2; i++)
                    Assert.That(Vector3.Dot(hands[i].position - shoulders[i].position, golem.transform.forward), Is.GreaterThan(0.05f));
            }
        }

        [UnityTest]
        public IEnumerator SwordSwingsWhileCrossbowAndStaffAimDuringTheirRealAttacks()
        {
            foreach (MonsterIdentity actor in actors) actor.gameObject.SetActive(false);
            foreach (MonsterWeaponStyle style in Enum.GetValues(typeof(MonsterWeaponStyle)))
            {
                MonsterIdentity actor = actors.Single(a => a.Rank == MonsterRank.Normal && a.Weapon == style);
                actor.gameObject.SetActive(true);
                actor.GetComponent<NavMeshAgent>().Warp(new Vector3(3f, 0f, -2f));
                actor.GetComponent<NavMeshAgent>().speed = 0f;
                actor.GetComponent<MonsterTracker>().Configure(20f, 30f, 0f);
                player.transform.position = new Vector3(3f, 0f, style == MonsterWeaponStyle.Sword ? -0.6f : 4f);
                Physics.SyncTransforms();
                Transform arm = actor.GetComponentsInChildren<Transform>().First(t => t.name == "upperarm.r");
                Quaternion before = arm.localRotation;
                var attack = actor.GetComponent<MonsterMeleeAttack>();
                float deadline = Time.time + 4f;
                while ((attack.Phase != MeleePhase.Windup || attack.WindupProgress < 0.65f) && Time.time < deadline) yield return null;
                Assert.That(attack.Phase, Is.EqualTo(MeleePhase.Windup));
                Assert.That(Quaternion.Angle(before, arm.localRotation), Is.GreaterThan(15f), style + " actual aiming/swing bone");
                Assert.That(actor.GetComponent<MonsterWeaponMotion>().PoseWeight, Is.GreaterThan(0.8f));
                if (style == MonsterWeaponStyle.Sword)
                {
                    Assert.That(actor.GetComponent<MonsterRangedAttack>(), Is.Null);
                    Quaternion raised = arm.localRotation;
                    while (attack.Phase == MeleePhase.Windup && Time.time < deadline) yield return null;
                    Assert.That(Quaternion.Angle(raised, arm.localRotation), Is.GreaterThan(20f), "blade crosses strike at release");
                }
                else
                {
                    var ranged = actor.GetComponent<MonsterRangedAttack>();
                    Assert.That(ranged.WeaponMuzzle, Is.Not.Null);
                    while (ranged.ShotsFired == 0 && Time.time < deadline) yield return null;
                    Assert.That(ranged.ShotsFired, Is.EqualTo(1));
                }
                actor.gameObject.SetActive(false);
            }
        }

        [UnityTest]
        public IEnumerator DragonLegsStayWithinBoundedGaitWithoutRotationFlips()
        {
            MonsterIdentity boss = actors.Single(a => a.Rank == MonsterRank.Boss);
            boss.GetComponent<MonsterMeleeAttack>().enabled = false;
            boss.GetComponent<MonsterTracker>().enabled = false;
            NavMeshAgent agent = boss.GetComponent<NavMeshAgent>();
            agent.Warp(new Vector3(10f, 0f, 15f));
            agent.SetDestination(new Vector3(10f, 0f, 24f));
            Transform[] joints = boss.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("leg_front_") || t.name.StartsWith("leg_back_")).ToArray();
            Assert.That(joints.Length, Is.GreaterThanOrEqualTo(12));
            Quaternion[] reference = joints.Select(j => j.localRotation).ToArray();
            Quaternion[] before = (Quaternion[])reference.Clone();
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                for (int i = 0; i < joints.Length; i++)
                {
                    Assert.That(Quaternion.Angle(reference[i], joints[i].localRotation), Is.LessThan(40f));
                    Assert.That(Quaternion.Angle(before[i], joints[i].localRotation), Is.LessThan(15f));
                    before[i] = joints[i].localRotation;
                }
            }
            boss.GetComponent<Health>().TryApplyDamage(10000);
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < joints.Length; i++) Assert.That(Quaternion.Angle(reference[i], joints[i].localRotation), Is.LessThan(40f));
        }

        [Test]
        public void DragonPreservesEveryOriginalTriangleUvAndSkinWeightWithFlatNormals()
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/OpenGameArt/Dragon/dragon.dae")
                .OfType<Mesh>().OrderByDescending(mesh => mesh.vertexCount).First();
            var derived = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ThirdParty/Nomad/Monsters/Candidates/DragonLowPoly.asset");
            Assert.That(source.triangles.Length, Is.GreaterThan(0), "readable original mesh required for comparison");
            Assert.That(derived.triangles.Length, Is.EqualTo(source.triangles.Length));
            Assert.That(derived.bindposes.Length, Is.EqualTo(source.bindposes.Length));
            Assert.That(derived.boneWeights.Length, Is.EqualTo(derived.vertexCount));
            Assert.That(derived.uv.Length, Is.EqualTo(derived.vertexCount));
            int[] triangles = derived.triangles;
            Vector3[] normals = derived.normals;
            Vector3[] originalVertices = source.vertices;
            Vector3[] actualVertices = derived.vertices;
            Vector2[] originalUv = source.uv;
            Vector2[] actualUv = derived.uv;
            BoneWeight[] originalWeights = source.boneWeights;
            BoneWeight[] actualWeights = derived.boneWeights;
            int[] originalIndices = source.triangles;
            for (int i = 0; i < triangles.Length; i++)
            {
                Assert.That(actualVertices[triangles[i]], Is.EqualTo(originalVertices[originalIndices[i]]));
                Assert.That(actualUv[triangles[i]], Is.EqualTo(originalUv[originalIndices[i]]));
                Assert.That(actualWeights[triangles[i]], Is.EqualTo(originalWeights[originalIndices[i]]));
            }
            for (int i = 0; i < triangles.Length; i += 3)
                Assert.That(Vector3.Angle(normals[triangles[i]], normals[triangles[i + 1]]), Is.LessThan(0.1f));
        }
    }
}
#endif
