using System;
using System.Linq;
using Nomad.Inventory.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    public static class PlayerMotionAuthoring
    {
        private const string Folder = "Assets/Nomad/PlayerVisual/Animations/";

        public static void Apply()
        {
            ProvidedItemArt.Apply();
            ApplyAnimationControllers();
            Debug.Log("PLAYER_MOTION_AND_UI_APPLIED");
        }

        internal static void ApplyAnimationControllers()
        {
            AnimationClip roll = BuildRoll();
            AnimationClip bow = Pose("BowAttack",
                ("Left Arm Front-Back", 0.85f, 0.85f), ("Left Arm Down-Up", 0f, -0.1f),
                ("Left Forearm Stretch", 0.9f, 0.8f), ("Right Arm Front-Back", 0.35f, -0.25f),
                ("Right Forearm Stretch", -0.4f, -0.8f), ("Chest Twist Left-Right", -0.15f, 0.1f));
            AnimationClip staff = Pose("StaffAttack",
                ("Right Arm Front-Back", 0.4f, 0.85f), ("Right Arm Down-Up", -0.3f, 0.35f),
                ("Right Forearm Stretch", -0.65f, 0.5f), ("Left Arm Front-Back", 0.25f, 0.7f),
                ("Left Forearm Stretch", -0.8f, -0.3f), ("Chest Front-Back", -0.1f, 0.15f));
            AnimationClip ranged = Pose("RangedAttack",
                ("Right Arm Front-Back", 0.9f, 0.7f), ("Right Arm Down-Up", -0.1f, 0.05f),
                ("Right Forearm Stretch", 0.7f, 0.2f), ("Left Arm Front-Back", 0.8f, 0.6f),
                ("Left Forearm Stretch", -0.2f, -0.5f), ("Chest Front-Back", 0f, -0.15f));

            foreach (string name in new[] { "PlayerLocomotion", "PlayerSword" })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + name + ".controller");
                if (controller == null) continue;
                foreach (var child in controller.layers[0].stateMachine.states)
                {
                    if (child.state.name == "Roll") { child.state.motion = roll; child.state.speed = roll.length / PlayerAnimation.RollMotionDuration; }
                    if (child.state.name == "Land") child.state.speed = ((AnimationClip)child.state.motion).length / PlayerAnimation.LandingMotionDuration;
                }
                if (controller.layers.Length > 1)
                {
                    var layers = controller.layers;
                    layers[0].iKPass = true;
                    layers[1].iKPass = true;
                    controller.layers = layers;
                    foreach (AnimationClip clip in new[] { bow, staff, ranged })
                    {
                        var machine = controller.layers[1].stateMachine;
                        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name) ?? machine.AddState(clip.name);
                        state.motion = clip;
                        state.speed = clip.length / 0.6f;
                        state.speedParameter = "SwordSpeed";
                        state.speedParameterActive = true;
                    }
                }
                PlayerHitReactionAuthoring.ApplyToController(controller);
                EditorUtility.SetDirty(controller);
            }
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip BuildRoll()
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Mixamo/StandToRoll.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Roll.anim");
            EditorUtility.CopySerialized(source, clip);
            const float from = 0.45f;
            const float to = 1.9f;
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = new AnimationCurve[bindings.Length];
            for (int index = 0; index < bindings.Length; index++)
            {
                AnimationCurve original = AnimationUtility.GetEditorCurve(source, bindings[index]);
                curves[index] = SliceCurve(original, from, to);
            }
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = to - from;
            settings.loopTime = false;
            settings.keepOriginalPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.name = "Roll";
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip Pose(string name, params (string muscle, float ready, float action)[] pose)
        {
            string path = Folder + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Idle.anim");
            if (clip == null) { clip = Object.Instantiate(idle); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(idle, clip);
            clip.name = name;
            var bindings = AnimationUtility.GetCurveBindings(idle);
            clip.ClearCurves();
            foreach (var binding in bindings)
            {
                var original = AnimationUtility.GetEditorCurve(idle, binding);
                AnimationUtility.SetEditorCurve(clip, binding, SliceCurve(original, 0, 0.6f));
            }
            foreach (var entry in pose)
            {
                if (!HumanTrait.MuscleName.Contains(entry.muscle)) throw new InvalidOperationException("Unknown muscle: " + entry.muscle);
                var curve = new AnimationCurve(new Keyframe(0, entry.ready), new Keyframe(0.2f, entry.action),
                    new Keyframe(0.35f, entry.ready), new Keyframe(0.6f, entry.ready));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), entry.muscle), curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = 0.6f;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationCurve SliceCurve(AnimationCurve original, float from, float to)
        {
            var keys = original.keys.Where(key => key.time > from && key.time < to).Select(key =>
            {
                key.time -= from;
                return key;
            }).ToList();
            foreach (float time in new[] { from, to })
            {
                float tangent = (original.Evaluate(time + 0.001f) - original.Evaluate(time - 0.001f)) / 0.002f;
                keys.Add(new Keyframe(time - from, original.Evaluate(time), tangent, tangent));
            }
            return new AnimationCurve(keys.OrderBy(key => key.time).ToArray());
        }
    }
}
