using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    // 원본 캐릭터는 수정하지 않고 팔에 가중치가 있는 삼각형만 프로젝트 소유 메시로 저장합니다.
    public static class FirstPersonArmsAuthoring
    {
        public const string Folder = "Assets/Nomad/PlayerVisual/Resources";
        public const string PrefabPath = Folder + "/VillageFirstPersonArms.prefab";

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/PlayerVisual/KingdomSoldierVisual.prefab");
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "VillageFirstPersonArms";
                Animator animator = root.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Update(0f);
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var excluded = new HashSet<Renderer>();
                foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    LOD[] levels = group.GetLODs();
                    for (int level = 1; level < levels.Length; level++)
                        foreach (Renderer renderer in levels[level].renderers) excluded.Add(renderer);
                    Object.DestroyImmediate(group);
                }
                int count = 0;
                int triangles = 0;
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || excluded.Contains(renderer))
                    {
                        Object.DestroyImmediate(renderer);
                        continue;
                    }
                    Mesh original = renderer.sharedMesh;
                    if (original == null) continue;
                    BoneWeight[] weights = original.boneWeights;
                    var allowed = new bool[weights.Length];
                    for (int v = 0; v < weights.Length; v++)
                    {
                        BoneWeight w = weights[v];
                        float armWeight = Weight(w.boneIndex0, w.weight0) + Weight(w.boneIndex1, w.weight1) +
                            Weight(w.boneIndex2, w.weight2) + Weight(w.boneIndex3, w.weight3);
                        allowed[v] = armWeight >= 0.6f;
                    }

                    Mesh mesh = Object.Instantiate(original);
                    mesh.name = "First Person Arms " + count;
                    int kept = 0;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        int[] indices = original.GetTriangles(sub);
                        var selected = new List<int>();
                        for (int i = 0; i < indices.Length; i += 3)
                            if (allowed[indices[i]] && allowed[indices[i + 1]] && allowed[indices[i + 2]])
                            {
                                selected.Add(indices[i]);
                                selected.Add(indices[i + 1]);
                                selected.Add(indices[i + 2]);
                            }
                        mesh.SetTriangles(selected, sub, false);
                        kept += selected.Count / 3;
                    }

                    if (kept == 0)
                    {
                        Object.DestroyImmediate(mesh);
                        Object.DestroyImmediate(renderer);
                        continue;
                    }
                    string path = Folder + "/FirstPersonArmsMesh" + count + ".asset";
                    Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) AssetDatabase.CreateAsset(mesh, path);
                    else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); mesh = saved; }
                    renderer.sharedMesh = mesh;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.updateWhenOffscreen = true;
                    renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 6f);
                    triangles += kept;
                    count++;

                    float Weight(int index, float value)
                    {
                        if (index < 0 || index >= renderer.bones.Length) return 0f;
                        Transform bone = renderer.bones[index];
                        return bone != null && (bone == left || bone.IsChildOf(left) || bone == right || bone.IsChildOf(right)) ? value : 0f;
                    }
                }
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(renderer);
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                if (triangles == 0) throw new InvalidOperationException("팔 메시 삼각형이 없습니다.");

                foreach (string oldPath in Directory.GetFiles(Folder, "FirstPersonArmsMesh*.asset"))
                {
                    string number = Path.GetFileNameWithoutExtension(oldPath).Replace("FirstPersonArmsMesh", "");
                    if (int.TryParse(number, out int index) && index >= count) AssetDatabase.DeleteAsset(oldPath.Replace('\\', '/'));
                }
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"FIRST_PERSON_ARMS_BUILT meshes={count} triangles={triangles} rightHand={animator.GetBoneTransform(HumanBodyBones.RightHand).rotation.eulerAngles}");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
