using System.Linq;
using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;

namespace Nomad.Game.Editor
{
    public static class BossDebugAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Monsters/BossDebug.unity";
        [MenuItem("Nomad/Boss/Create Debug Scene And Knockdown")]
        public static void Apply()
        {
            GameObject dragon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Nomad/Monsters/Candidates/Dragon.prefab");
            CreateKnockdown(dragon);
            CreateDeath(dragon);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Boss Debug");
            var debug = root.AddComponent<BossDebugBootstrap>();
            debug.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab");
            debug.DragonPrefab = dragon;
            var content = root.AddComponent<BossEncounterContent>();
            content.Rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Monsters/Resources/BossArenaRock.prefab");
            content.BossHealth = BossArmor.ReferenceHealth;
            content.Knockdown = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Nomad/Monsters/Resources/BossKnockdown.anim");
            content.DropEntrance = true;
            content.UnevenFloor = true;
            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1, 0.78f, 0.6f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("BOSS_DEBUG_SCENE_SAVED " + ScenePath);
        }

        private static void CreateKnockdown(GameObject prefab)
        {
            const string path = "Assets/Nomad/Monsters/Resources/BossKnockdown.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = "BossKnockdown", frameRate = 60 }; AssetDatabase.CreateAsset(clip, path); }
            BossKnockdownPoseAuthoring.Write(prefab, clip);
            Animator animator = prefab.GetComponentInChildren<Animator>();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.startTime = 0;
            settings.stopTime = 4.8f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            const string controllerPath = "Assets/Nomad/Monsters/Resources/BossCombat.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath))
                    throw new System.InvalidOperationException("보스 컨트롤러 복사 실패");
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            }
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(child => child.state).FirstOrDefault(candidate => candidate.name == "BossKnockdown")
                ?? machine.AddState("BossKnockdown");
            state.motion = clip;
            state.writeDefaultValues = true;
            EditorUtility.SetDirty(controller);
        }

        private static void CreateDeath(GameObject prefab)
        {
            const string path = "Assets/Nomad/Monsters/Resources/BossDeath.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = "BossDeath", frameRate = 60 }; AssetDatabase.CreateAsset(clip, path); }
            Animator animator = prefab.GetComponentInChildren<Animator>();
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
            {
                bool root = bone.name == "base";
                bool leg = bone.name.Contains("leg_") && bone.name.Contains("upper");
                bool neck = bone.name == "neck1" || bone.name == "neck3";
                bool wing = bone.name.Contains("wing_upper");
                if (!root && !leg && !neck && !wing) continue;
                string binding = AnimationUtility.CalculateTransformPath(bone, animator.transform);
                Quaternion rest = bone.localRotation;
                Quaternion dead = root
                    ? Quaternion.Inverse(bone.parent.rotation) * (Quaternion.AngleAxis(-88, prefab.transform.forward) * bone.rotation)
                    : rest * Quaternion.Euler(neck ? 55 : leg ? 35 : 20, 0, wing ? 15 : 0);
                Quaternion halfway = Quaternion.Slerp(rest, dead, 0.2f);
                for (int axis = 0; axis < 4; axis++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(binding, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]),
                        new AnimationCurve(new Keyframe(0, rest[axis]), new Keyframe(0.25f, halfway[axis]),
                            new Keyframe(1.4f, dead[axis]), new Keyframe(2.6f, dead[axis])));

                if (!root) continue;
                Vector3 position = bone.localPosition;
                Vector3 fallen = position + bone.parent.InverseTransformVector(Vector3.down * 0.15f);
                for (int axis = 0; axis < 3; axis++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(binding, typeof(Transform), "m_LocalPosition." + "xyz"[axis]),
                        new AnimationCurve(new Keyframe(0, position[axis]), new Keyframe(1.4f, fallen[axis]), new Keyframe(2.6f, fallen[axis])));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = 2.6f;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Nomad/Monsters/Resources/BossCombat.controller");
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(child => child.state).FirstOrDefault(candidate => candidate.name == "BossDeath") ?? machine.AddState("BossDeath");
            state.motion = clip;
            state.writeDefaultValues = true;
            EditorUtility.SetDirty(controller);
        }
    }
}
