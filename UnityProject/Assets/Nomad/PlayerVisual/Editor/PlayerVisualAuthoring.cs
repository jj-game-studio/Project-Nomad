using System;
using System.IO;
using System.Linq;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    // 외형은 별도 프리팹, 기능은 기존 PlayerRig의 Variant로 유지합니다.
    internal static class PlayerVisualAuthoring
    {
        internal const string Folder = "Assets/Nomad/PlayerVisual";
        internal const string VisualPath = Folder + "/KingdomSoldierVisual.prefab";
        internal const string RigPath = Folder + "/KingdomPlayerRig.prefab";
        internal const string ScenePath = Folder + "/PlayerVisualPreview.unity";
        internal const string BaseRigPath = "Assets/Nomad/Game/Prefabs/PlayerRig.prefab";
        private const string SourcePath = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Soldier_Male_01.prefab";

        [MenuItem("Nomad/Player Visual/Build Preview")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            RenamePreset(Folder + "/PrototypeMaleVisual.prefab", VisualPath);
            RenamePreset(Folder + "/PrototypePlayerRig.prefab", RigPath);
            PlayerAnimationAuthoring.Build();
            BuildVisual();
            BuildRig();
            BuildScene();
            Validate();
            Debug.Log("PLAYER_VISUAL_BUILT");
        }

        [MenuItem("Nomad/Player Visual/Open Preview")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        private static void BuildVisual()
        {
            var root = new GameObject("KingdomSoldierVisual");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
                Require(source != null, "Source character prefab is missing");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                {
                    ConfigureAvatar(animator);
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    // 프로젝트 소유 Controller로 기존 에셋의 동작을 재생합니다.
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimationAuthoring.ControllerPath);
                }

                // 높이를 기존 캡슐에 맞추되, 애니메이션의 발 기준인 원본 루트 높이는 유지합니다.
                Bounds bounds = BoundsOf(root);
                Require(bounds.size.y > 0.01f, "Source model has no visible height");
                model.transform.localScale *= 1.8f / bounds.size.y;
                bounds = BoundsOf(root);
                model.transform.localPosition -= new Vector3(bounds.center.x, 0f, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ConfigureAvatar(Animator animator)
        {
            var description = animator.avatar.humanDescription;
            // 원본 Avatar는 바닥 Root를 골반으로 지정합니다. 실제 Hips에 매핑해 접지를 복구합니다.
            var human = description.human;
            for (int i = 0; i < human.Length; i++)
                if (human[i].humanName == "Hips") human[i].boneName = "Hips";
            description.human = human;
            description.skeleton = animator.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            }).ToArray();

            var avatar = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
            Require(avatar.isValid && avatar.isHuman, "Could not build player Avatar");
            avatar.name = "KingdomPlayerAvatar";
            string path = Folder + "/KingdomPlayerAvatar.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (saved == null) AssetDatabase.CreateAsset(avatar, path);
            else
            {
                EditorUtility.CopySerialized(avatar, saved);
                Object.DestroyImmediate(avatar);
                avatar = saved;
            }
            animator.avatar = avatar;
        }

        private static void BuildRig()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BaseRigPath);
            Require(source != null, "Base PlayerRig is missing");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                rig.name = "KingdomPlayerRig";
                var player = rig.GetComponentInChildren<PlayerMovement>();
                var body = player.transform.Find("Player Body");
                Require(body != null, "Expected placeholder body is missing");
                body.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(body.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), player.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                var animation = player.gameObject.AddComponent<PlayerAnimation>();
                var data = new SerializedObject(animation);
                data.FindProperty("animator").objectReferenceValue = visual.GetComponentInChildren<Animator>();
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
            }
            finally { Object.DestroyImmediate(rig); }
        }

        private static void RenamePreset(string previousPath, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(previousPath) == null)
                return;

            // 기존 참조를 보존하도록 GUID를 유지한 채 이름만 변경합니다.
            string error = AssetDatabase.MoveAsset(previousPath, path);
            Require(string.IsNullOrEmpty(error), error);
        }

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.68f, 0.75f);
            var light = new GameObject("Preview Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(20f, 0.5f, 20f));
            Box("Wall", new Vector3(0f, 1f, 4f), new Vector3(6f, 2f, 0.5f));
            Box("Step", new Vector3(3f, 0.15f, 0f), new Vector3(1.5f, 0.3f, 3f));

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            var player = rig.GetComponentInChildren<PlayerMovement>();
            player.transform.position = Vector3.up * 0.02f;
            rig.GetComponentInChildren<TopDownCamera>().ResetTracking();
            Label("Instructions", new Vector3(-4f, 0.02f, -3f),
                "PLAYER MODEL PREVIEW\nWASD: move | Space: jump | Shift: roll\nARPG idle/run + Mixamo jump/roll", 0.07f);

            // 정면·측면은 움직이는 플레이어와 분리해 외형 크기와 방향을 비교합니다.
            PreviewModel("Front Reference", new Vector3(-3f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            PreviewModel("Side Reference", new Vector3(-5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void PreviewModel(string name, Vector3 position, Quaternion rotation)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath));
            visual.name = name;
            visual.transform.SetPositionAndRotation(position, rotation);
        }

        private static void Box(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
        }

        private static void Label(string name, Vector3 position, string text, float size)
        {
            var label = new GameObject(name).AddComponent<TextMesh>();
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            label.text = text;
            label.characterSize = size;
            label.fontSize = 64;
            label.color = Color.white;
        }

        [MenuItem("Nomad/Player Visual/Validate Presets")]
        public static void Validate()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            Require(visual != null && rig != null, "Generated prefabs are missing");
            Require(PrefabUtility.GetPrefabAssetType(rig) == PrefabAssetType.Variant, "Player preset must inherit PlayerRig");
            Require(PrefabUtility.GetCorrespondingObjectFromSource(rig) == AssetDatabase.LoadAssetAtPath<GameObject>(BaseRigPath), "Unexpected base rig");

            var player = rig.GetComponentInChildren<PlayerMovement>();
            Require(!player.transform.Find("Player Body").gameObject.activeSelf, "Placeholder is still visible");
            Require(player.transform.Find("Visual") != null, "Visual is missing");
            Require(player.GetComponent<CharacterController>().height == 1.8f, "Collision size changed");
            Require(visual.GetComponentsInChildren<Collider>(true).Length == 0, "Visual must not add colliders");
            Require(visual.GetComponentsInChildren<Animator>(true).All(a => !a.applyRootMotion), "Root motion must be off");
            var animator = visual.GetComponentInChildren<Animator>();
            Require(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman, "Invalid character Avatar");
            Require(animator.runtimeAnimatorController != null, "Locomotion Controller is missing");
            Require(player.GetComponent<PlayerAnimation>() != null, "Animation driver is missing");
            Require(visual.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.sharedMesh != null), "Skinned mesh is missing");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(visual);
            try
            {
                Bounds bounds = BoundsOf(instance);
                Require(Mathf.Abs(bounds.size.y - 1.8f) < 0.02f && bounds.min.y > -0.15f && bounds.min.y < 0.02f, $"Height/feet mismatch: {bounds}");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported), "Missing or unsupported material");
            }
            finally { Object.DestroyImmediate(instance); }
            Debug.Log("PLAYER_VISUAL_ASSETS_PASS: inherited rig, model, height, feet, collider and material checks");
        }

        internal static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Require(renderers.Length > 0, "No active renderer");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
