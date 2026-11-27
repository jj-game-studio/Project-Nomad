using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nomad.PlayerVisual.Editor
{
    public static class PlayerHitReactionAuthoring
    {
        private const string Folder = "Assets/Nomad/PlayerVisual/Animations/";
        public static void Apply()
        {
            foreach (string name in new[] { "PlayerLocomotion", "PlayerSword" })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + name + ".controller");
                if (controller != null) ApplyToController(controller);
            }
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToController(AnimatorController controller)
        {
            const string clipPath = Folder + "DamageFlinch.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip { name = "DamageFlinch", frameRate = 60 }; AssetDatabase.CreateAsset(clip, clipPath); }
            foreach (var curve in new[] { ("Chest Front-Back", -0.2f), ("Chest Twist Left-Right", 0.08f) })
            {
                if (!HumanTrait.MuscleName.Contains(curve.Item1)) throw new InvalidOperationException("Unknown muscle: " + curve.Item1);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), curve.Item1),
                    new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.045f, curve.Item2), new Keyframe(0.24f, 0)));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = 0.24f;
            settings.loopTime = false;
            settings.hasAdditiveReferencePose = true;
            settings.additiveReferencePoseTime = 0;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            const string maskPath = Folder + "DamageReaction.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask { name = "Damage Reaction" }; AssetDatabase.CreateAsset(mask, maskPath); }
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, part == (int)AvatarMaskBodyPart.Body || part == (int)AvatarMaskBodyPart.Head);
            EditorUtility.SetDirty(mask);

            string layerName = Nomad.CombatFeedback.PlayerDamageFeedback.LayerName;
            var layer = controller.layers.FirstOrDefault(candidate => candidate.name == layerName);
            if (layer == null)
            {
                var machine = new AnimatorStateMachine { name = layerName };
                AssetDatabase.AddObjectToAsset(machine, controller);
                layer = new AnimatorControllerLayer { name = layerName, stateMachine = machine };
                controller.layers = controller.layers.Concat(new[] { layer }).ToArray();
            }
            var layers = controller.layers;
            layer = layers.First(candidate => candidate.name == layerName);
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Additive;
            layer.defaultWeight = 0;
            layer.iKPass = false;
            controller.layers = layers;
            AnimatorState state = layer.stateMachine.states.Select(child => child.state).FirstOrDefault(child => child.name == "DamageFlinch") ?? layer.stateMachine.AddState("DamageFlinch");
            state.motion = clip;
            state.writeDefaultValues = false;
            layer.stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
        }
    }
}
