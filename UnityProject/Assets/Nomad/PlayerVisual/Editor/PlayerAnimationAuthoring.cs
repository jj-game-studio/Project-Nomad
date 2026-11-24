using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    internal static class PlayerAnimationAuthoring
    {
        internal const string Folder = PlayerVisualAuthoring.Folder + "/Animations";
        internal const string ControllerPath = Folder + "/PlayerLocomotion.controller";
        private const string Warrior = "Assets/ThirdParty/ARPGPack/ARPGWarrior/Animations/Humanoid/";

        internal static RuntimeAnimatorController Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var idle = Copy("Idle", Warrior + "ARPG_Warrior_Idle_no_weapon.anim", true);
            var run = Copy("Run", Warrior + "ARPG_Warrior_Run_no_weapon_IPC.anim", true);
            var jump = JumpSlice("Jump", 0.6f, 0.93f, false);
            var fall = JumpSlice("Fall", 0.93f, 1.27f, false);
            var land = JumpSlice("Land", 1.27f, 2.1f, true);
            var roll = Copy("Roll", "Assets/ThirdParty/Mixamo/StandToRoll.fbx", false);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            AddState(machine, "Idle", idle, 1f);
            AddState(machine, "Run", run, 1f);
            AddState(machine, "Jump", jump, jump.length / PlayerAnimation.JumpRiseDuration);
            AddState(machine, "Fall", fall, fall.length / PlayerAnimation.JumpFallDuration);
            AddState(machine, "Land", land, land.length / PlayerAnimation.LandingMotionDuration);
            AddState(machine, "Roll", roll, roll.length / PlayerAnimation.RollMotionDuration);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            PlayerMotionAuthoring.ApplyAnimationControllers();
            return controller;
        }

        private static AnimationClip JumpSlice(string name, float from, float to, bool landing)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Mixamo/StandingJump.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            var clip = Copy(name, "Assets/ThirdParty/Mixamo/StandingJump.fbx", false);
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = new AnimationCurve[bindings.Length];
            int steps = Mathf.CeilToInt((to - from) * 60f);

            // 필요한 구간만 다시 샘플링하며 원본 FBX는 수정하지 않습니다.
            for (int i = 0; i < bindings.Length; i++)
            {
                var original = AnimationUtility.GetEditorCurve(source, bindings[i]);
                float offset = !landing && bindings[i].propertyName == "RootT.y"
                    ? original.Evaluate(from) - original.Evaluate(0f) : 0f;
                var keys = new Keyframe[steps + 1];
                for (int k = 0; k <= steps; k++)
                {
                    float time = (to - from) * k / steps;
                    float sample = from + time;
                    float tangent = (original.Evaluate(sample + 0.001f) - original.Evaluate(sample - 0.001f)) / 0.002f;
                    keys[k] = new Keyframe(time, original.Evaluate(sample) - offset, tangent, tangent);
                }
                curves[i] = new AnimationCurve(keys);
            }
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = to - from;
            settings.loopTime = false;
            settings.loopBlend = false;
            settings.loopBlendPositionY = landing;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            PlayerVisualAuthoring.Require(Mathf.Abs(clip.length - (to - from)) < 0.02f && clip.humanMotion,
                $"Invalid jump slice: {name}, {clip.length}");
            Debug.Log($"JUMP_SLICE {name}: {from:F2}-{to:F2}, duration={clip.length:F3}");
            return clip;
        }

        private static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip, float speed)
        {
            var state = machine.AddState(name);
            state.motion = clip;
            state.speed = speed;
            if (name == "Idle") machine.defaultState = state;
        }

        private static AnimationClip Copy(string name, string sourcePath, bool loop)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__"));
            PlayerVisualAuthoring.Require(source != null && source.humanMotion, $"Missing Humanoid clip: {sourcePath}");
            string path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Object.Instantiate(source);
                AssetDatabase.CreateAsset(clip, path);
            }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = name;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionXZ = false;
            settings.loopBlendPositionY = loop;
            settings.keepOriginalPositionXZ = false;
            settings.keepOriginalPositionY = true;
            settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            Debug.Log($"PLAYER_ANIMATION_CLIP {name}: source={source.name}, seconds={clip.length}, human={clip.humanMotion}");
            return clip;
        }
    }
}
