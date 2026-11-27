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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Boss Debug");
            var debug = root.AddComponent<BossDebugBootstrap>();
            debug.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab");
            debug.DragonPrefab = dragon;
            var content = root.AddComponent<BossEncounterContent>();
            content.Rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Monsters/Resources/BossArenaRock.prefab");
            content.BossHealth = BossArmor.ReferenceHealth;
            content.Knockdown = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Nomad/Monsters/Resources/BossKnockdown.anim");
            content.UnevenFloor = true;
            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
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
            Animator animator = prefab.GetComponentInChildren<Animator>();
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
            {
                bool root = bone.name == "base";
                bool leg = bone.name.Contains("leg_") && bone.name.Contains("upper");
                bool neck = bone.name == "neck1";
                if (!root && !leg && !neck) continue;
                string binding = AnimationUtility.CalculateTransformPath(bone, animator.transform);
                Quaternion rest = bone.localRotation;
                Quaternion down = root
                    ? Quaternion.Inverse(bone.parent.rotation) * (Quaternion.AngleAxis(-70, prefab.transform.forward) * bone.rotation)
                    : rest * Quaternion.Euler(leg ? 60 : 30, 0, 0);
                for (int axis = 0; axis < 4; axis++)
                {
                    var curve = new AnimationCurve(new Keyframe(0, rest[axis]), new Keyframe(0.38f, down[axis]),
                        new Keyframe(3.1f, down[axis]), new Keyframe(3.5f, down[axis]), new Keyframe(4.2f, rest[axis]));
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(binding, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]), curve);
                }
                if (!root) continue;
                Vector3 position = bone.localPosition;
                Vector3 fallen = position + bone.parent.InverseTransformVector(Vector3.down * 0.15f);
                for (int axis = 0; axis < 3; axis++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(binding, typeof(Transform), "m_LocalPosition." + "xyz"[axis]),
                        new AnimationCurve(new Keyframe(0, position[axis]), new Keyframe(0.38f, fallen[axis]),
                            new Keyframe(3.5f, fallen[axis]), new Keyframe(4.2f, position[axis])));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.startTime = 0;
            settings.stopTime = 4.2f;
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
    }
}
