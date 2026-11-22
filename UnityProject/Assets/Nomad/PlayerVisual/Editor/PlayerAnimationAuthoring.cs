using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
            var fall = Copy("Fall", Warrior + "ARPG_Warrior_Airborne.anim", true);
            var jump = Copy("Jump", "Assets/ThirdParty/Mixamo/StandingJump.fbx", false);
            var roll = Copy("Roll", "Assets/ThirdParty/Mixamo/StandToRoll.fbx", false);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            AddState(machine, "Idle", idle, 1f);
            AddState(machine, "Run", run, 1f);
            AddState(machine, "Jump", jump, jump.length / PlayerAnimation.JumpMotionDuration);
            AddState(machine, "Fall", fall, 1f);
            AddState(machine, "Roll", roll, roll.length / PlayerAnimation.RollMotionDuration);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
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
