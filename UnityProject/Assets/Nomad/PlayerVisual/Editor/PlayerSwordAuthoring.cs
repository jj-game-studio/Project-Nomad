using System;
using Nomad.Equipment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    internal static class PlayerSwordAuthoring
    {
        internal const string Folder = PlayerVisualAuthoring.Folder;
        internal const string RigPath = Folder + "/SwordPlayerRig.prefab";
        internal const string ScenePath = Folder + "/SwordPreview.unity";
        private const string ControllerPath = Folder + "/Animations/PlayerSword.controller";

        [MenuItem("Nomad/Player Visual/Build Sword Preview")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
                AssetDatabase.CopyAsset(PlayerAnimationAuthoring.ControllerPath, ControllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            PlayerVisualAuthoring.Require(controller != null, "Missing sword controller");
            while (controller.layers.Length > 1) controller.RemoveLayer(controller.layers.Length - 1);
            if (!Array.Exists(controller.parameters, p => p.name == "LocomotionSpeed"))
                controller.AddParameter("LocomotionSpeed", AnimatorControllerParameterType.Float);
            foreach (var state in controller.layers[0].stateMachine.states)
                if (state.state.name == "Run")
                {
                    state.state.speedParameter = "LocomotionSpeed";
                    state.state.speedParameterActive = true;
                }

            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/ThirdParty/ARPGPack/ARPGWarrior/Animations/Humanoid/ARPG_Warrior_Attack_Combo1.anim");
            PlayerVisualAuthoring.Require(source != null && source.humanMotion, "Missing Humanoid sword clip");
            string clipPath = Folder + "/Animations/SwordAttack.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = Object.Instantiate(source);
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = "SwordAttack";
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string maskPath = Folder + "/Animations/SwordUpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
                         AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                         AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                mask.SetHumanoidBodyPartActive(part, true);

            controller.AddLayer(PlayerSwordAnimation.LayerName);
            var layers = controller.layers;
            var attackLayer = layers[1];
            attackLayer.avatarMask = mask;
            attackLayer.defaultWeight = 0f;
            var attack = attackLayer.stateMachine.AddState("SwordAttack");
            attack.motion = clip;
            attack.speed = clip.length / 0.6f;
            attackLayer.stateMachine.defaultState = attack;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(mask);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualAuthoring.RigPath));
            try
            {
                rig.name = "SwordPlayerRig";
                var animator = rig.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                rig.GetComponentInChildren<PlayerAnimation>().gameObject.AddComponent<PlayerSwordAnimation>();
                PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
            }
            finally { Object.DestroyImmediate(rig); }

            EditorSceneManager.OpenScene(PlayerVisualAuthoring.ScenePath);
            var previous = Object.FindAnyObjectByType<PlayerAnimation>();
            Object.DestroyImmediate(previous.transform.root.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Sword Target";
            target.transform.position = new Vector3(0f, 0.9f, 1.5f);
            target.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            target.AddComponent<Nomad.Combat.Health>();
            GameObject.Find("Instructions").GetComponent<TextMesh>().text =
                "SWORD PREVIEW\nWASD: move | LMB: attack | Tab: equipment\nSpace: jump | Shift: roll";
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"SWORD_PREVIEW_BUILT sourceLength={source.length:F3}");
        }

        [MenuItem("Nomad/Player Visual/Open Sword Preview")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
