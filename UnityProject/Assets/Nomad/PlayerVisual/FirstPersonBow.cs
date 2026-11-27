using UnityEngine;

namespace Nomad.PlayerVisual
{
    internal sealed class FirstPersonBow
    {
        private readonly Transform model;
        private readonly LineRenderer bowstring;
        private readonly Transform arrow;
        private readonly Vector3 top;
        private readonly Vector3 bottom;

        public FirstPersonBow(Transform weapon, MeshFilter mesh)
        {
            model = weapon;
            Bounds bounds = mesh.sharedMesh.bounds;
            top = model.InverseTransformPoint(mesh.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)));
            bottom = model.InverseTransformPoint(mesh.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)));

            bowstring = new GameObject("Drawn Bowstring").AddComponent<LineRenderer>();
            bowstring.transform.SetParent(model, false);
            bowstring.positionCount = 3;
            bowstring.useWorldSpace = true;
            bowstring.startWidth = bowstring.endWidth = 0.0025f;
            bowstring.sharedMaterial = mesh.GetComponent<Renderer>().sharedMaterial;
            bowstring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            arrow = new GameObject("Nocked Arrow").transform;
            arrow.SetParent(model, false);
            Material material = mesh.GetComponent<Renderer>().sharedMaterial;
            Part("Arrow Shaft", PrimitiveType.Cylinder, new Vector3(0, 0, 0.33f), new Vector3(0.007f, 0.33f, 0.007f), material)
                .localRotation = Quaternion.Euler(90, 0, 0);
            Part("Arrow Head", PrimitiveType.Cube, new Vector3(0, 0, 0.68f), new Vector3(0.018f, 0.018f, 0.055f), material);
            Part("Arrow Fletching", PrimitiveType.Cube, new Vector3(0, 0, 0.055f), new Vector3(0.035f, 0.003f, 0.075f), material);
            Part("Arrow Fletching Upright", PrimitiveType.Cube, new Vector3(0, 0, 0.055f), new Vector3(0.003f, 0.035f, 0.075f), material);
        }

        private Transform Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(arrow, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        public void Pose(Vector3 nock, Vector3 aim, bool loaded)
        {
            bowstring.SetPosition(0, model.TransformPoint(top));
            bowstring.SetPosition(1, nock);
            bowstring.SetPosition(2, model.TransformPoint(bottom));
            arrow.SetPositionAndRotation(nock, Quaternion.LookRotation(aim - nock, model.up));
            // 화살 치수는 무기의 원본 임포트 배율과 무관하게 미터 단위로 유지합니다.
            Vector3 scale = model.lossyScale;
            arrow.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            arrow.gameObject.SetActive(loaded);
        }
    }
}
