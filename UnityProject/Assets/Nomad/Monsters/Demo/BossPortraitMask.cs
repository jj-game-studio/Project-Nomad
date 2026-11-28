using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossPortraitMask : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper h)
        {
            h.Clear(); float radius=rectTransform.rect.width*0.5f;
            h.AddVert(Vector3.zero,Color.white,new Vector2(0.5f,0.5f));
            for(int i=0;i<=64;i++)
            {
                float angle=i*Mathf.PI*2/64;
                Vector2 v=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                h.AddVert(v*radius,Color.white,v*0.5f+Vector2.one*0.5f);
                if(i>0)h.AddTriangle(0,i,i+1);
            }
        }
    }
}
