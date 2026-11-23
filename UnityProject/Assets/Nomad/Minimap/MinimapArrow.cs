using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Minimap
{
    public sealed class MinimapArrow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            mesh.AddVert(new Vector3(0, 7, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(-5, -5, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(0, -2, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(5, -5, 0), color, Vector2.zero);
            mesh.AddTriangle(0, 2, 1);
            mesh.AddTriangle(0, 3, 2);
        }
    }
}
