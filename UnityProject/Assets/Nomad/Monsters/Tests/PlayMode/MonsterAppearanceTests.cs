#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
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
    public sealed class MonsterAppearanceTests
    {
        private Scene previous;
        private Scene scene;
        private Health player;
        private Component factory;
        private MonsterAppearanceSet catalog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(
                "Assets/ThirdParty/Nomad/Monsters/Skeletons/SkeletonAppearances.asset");
            Assert.That(catalog, Is.Not.Null, "private appearance catalog is required");
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Nomad/Monsters/MonsterElite.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            GameObject controls = Array.Find(scene.GetRootGameObjects(), root => root.name == "Test Controls");
            player = Array.Find(scene.GetRootGameObjects(), root => root.name == "Player").GetComponent<Health>();
            player.IsCombatImmune = true;
            yield return null;
            factory = controls.GetComponent(Type.GetType("Nomad.Monsters.Demo.MonsterActorFactory, Assembly-CSharp"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator FourNormalVariantsHaveSkinsMaterialsAndIdleAnimation()
        {
            Assert.That(catalog.normal.Length, Is.EqualTo(4));
            Assert.That(catalog.elite, Is.Not.Null, "Iron Golem test elite");
            Assert.That(catalog.boss, Is.Not.Null, "Dragon test boss");
            for (int i = 0; i < 4; i++)
            {
                Health actor = Create("variant-" + i, new Vector3(4f, 0f, -3f));
                yield return null;
                yield return null;
                Animator animator = actor.GetComponentInChildren<Animator>();
                RequireModel(actor, animator);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle_A"), Is.True);
                actor.gameObject.SetActive(false);
            }
        }

        [UnityTest]
        public IEnumerator TexturedFlashCoversEveryBodyPartThenRestoresAtlasAndDeathAnimation()
        {
            Health actor = Create("flash-0", new Vector3(4f, 0f, -3f));
            yield return null;
            Renderer[] bodies = actor.transform.Find("Body").GetComponentsInChildren<Renderer>();
            Assert.That(bodies.Length, Is.GreaterThan(1));
            actor.TryApplyDamage(25);
            yield return null;
            var block = new MaterialPropertyBlock();
            foreach (Renderer body in bodies)
            {
                body.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                Assert.That(block.GetTexture("_BaseMap"), Is.EqualTo(Texture2D.whiteTexture));
            }
            yield return new WaitForSeconds(0.2f);
            foreach (Renderer body in bodies)
            {
                body.GetPropertyBlock(block);
                Assert.That(block.GetTexture("_BaseMap"), Is.EqualTo(body.sharedMaterial.GetTexture("_BaseMap")));
            }
            actor.TryApplyDamage(1000);
            yield return new WaitForSeconds(0.3f);
            Assert.That(actor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0)
                .IsName("Base Layer.Death_A"), Is.True);
        }

        [UnityTest]
        public IEnumerator RealAgentMovementSelectsRunAnimationWithoutRootMotion()
        {
            Health actor = Create("move-1", new Vector3(4f, 0f, -3f));
            yield return null;
            Transform leg = Array.Find(actor.GetComponentsInChildren<Transform>(), bone => bone.name == "upperleg.l");
            Assert.That(leg, Is.Not.Null);
            Quaternion idleLeg = leg.localRotation;
            actor.GetComponent<MonsterTracker>().enabled = false;
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.acceleration = 20f;
            agent.SetDestination(new Vector3(4f, 0f, 4f));
            float end = Time.time + 2f;
            Animator animator = actor.GetComponentInChildren<Animator>();
            while ((!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Running_A") || agent.velocity.magnitude < 2.5f)
                && Time.time < end) yield return null;
            Assert.That(agent.velocity.magnitude, Is.GreaterThan(2.5f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Running_A"), Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            yield return new WaitForSeconds(0.2f);
            Assert.That(Quaternion.Angle(idleLeg, leg.localRotation), Is.GreaterThan(2f), "실제 뼈 포즈도 움직여야 합니다.");
        }

        private Health Create(string name, Vector3 position)
        {
            Type settingsType = Type.GetType("Nomad.Monsters.Demo.MonsterTestSettings, Assembly-CSharp");
            object settings = settingsType.GetMethod("Defaults", new[] { typeof(bool) }).Invoke(null, new object[] { false });
            settingsType.GetField("moveSpeed").SetValue(settings, 0f);
            Health actor = (Health)factory.GetType().GetMethod("Create").Invoke(factory,
                new[] { (object)player, settings, false, position, Quaternion.identity, name, null });
            actor.GetComponent<MonsterMeleeAttack>().enabled = false;
            return actor;
        }

        private static void RequireModel(Health actor, Animator animator)
        {
            Assert.That(actor.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar != null && animator.avatar.isValid, Is.True);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(actor.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
        }
    }
}
#endif
