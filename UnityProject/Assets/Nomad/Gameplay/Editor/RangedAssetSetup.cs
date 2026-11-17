using System.Linq;
using Nomad.Monsters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class RangedAssetSetup
    {
        private const string Output = "Assets/ThirdParty/Nomad/Monsters/Candidates";
        private const string Skeletons = "Assets/ThirdParty/Nomad/Monsters/Skeletons";
        private const string Weapons = "Assets/ThirdParty/KayKit/Skeletons/Weapons";

        public static void Apply(MonsterAppearanceSet catalog)
        {
            foreach (string name in new[] { "Staff", "Crossbow", "Arrow" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath($"{Weapons}/Skeleton_{name}.fbx");
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            Material skeleton = AssetDatabase.LoadAssetAtPath<Material>(Skeletons + "/Skeleton.mat");
            foreach (GameObject prefab in catalog.normal)
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                Transform hand = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "hand.r");
                if (hand != null)
                {
                    Transform previous = hand.Find("Ranged weapon");
                    if (previous != null) Object.DestroyImmediate(previous.gameObject);
                    bool mage = prefab.name.Contains("Mage");
                    var weapon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        Weapons + (mage ? "/Skeleton_Staff.fbx" : "/Skeleton_Crossbow.fbx")), hand);
                    weapon.name = "Ranged weapon";
                    weapon.transform.localPosition = Vector3.zero;
                    weapon.transform.localRotation = Quaternion.identity;
                    foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = Enumerable.Repeat(skeleton, renderer.sharedMaterials.Length).ToArray();
                }
                else Debug.LogWarning("Weapon hand not found: " + prefab.name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            catalog.arrow = Bolt("Arrow", Color.white, Weapons + "/Skeleton_Arrow.fbx", skeleton);
            catalog.magicBolt = Bolt("Magic bolt", new Color(0.15f, 0.7f, 1f), null, null);
            catalog.fireBolt = Bolt("Fire bolt", new Color(1f, 0.25f, 0.03f), null, null);
            catalog.rockBolt = Bolt("Rock bolt", new Color(0.45f, 0.38f, 0.3f),
                "Assets/ThirdParty/Synty_Prototype/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Rock_01.prefab", null);
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject Bolt(string name, Color color, string source, Material shared)
        {
            var root = new GameObject(name);
            Material material = shared;
            if (material == null)
            {
                string path = Output + "/" + name + ".mat";
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
            }
            GameObject model = source != null ? Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source)) :
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.transform.SetParent(root.transform, false);
            Renderer[] meshes = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = meshes[0].bounds;
            foreach (Renderer mesh in meshes)
            {
                bounds.Encapsulate(mesh.bounds);
                mesh.sharedMaterials = Enumerable.Repeat(material, mesh.sharedMaterials.Length).ToArray();
            }
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.transform.localScale *= (name == "Arrow" ? 0.65f : 0.35f) / size;
            model.transform.localPosition = -bounds.center * model.transform.localScale.x;
            if (name == "Arrow" && bounds.size.y > bounds.size.z)
                model.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            if (name != "Arrow")
            {
                TrailRenderer trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.time = 0.2f;
                trail.startWidth = 0.18f;
                trail.endWidth = 0f;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
