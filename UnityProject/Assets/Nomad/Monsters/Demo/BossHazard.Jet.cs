using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        private Material fanMaterial;
        private void CreateContinuousJet()
        {
            const int rings=20,segments=48,layers=3;
            var vertices=new Vector3[(rings+1)*(segments+1)*layers];
            var uv=new Vector2[vertices.Length];var triangles=new int[rings*segments*6*layers];
            int write=0;
            for(int layer=0;layer<layers;layer++)
            for(int row=0;row<=rings;row++)
            for(int col=0;col<=segments;col++)
            {
                float along=row/(float)rings,across=col/(float)segments;
                Vector3 heading=Quaternion.Euler(0,Mathf.Lerp(-angle*0.5f+0.5f,angle*0.5f-0.5f,across),0)*Vector3.forward;
                Vector3 point=heading*Mathf.Lerp(InnerRadius+0.1f,Radius-0.12f,along);
                Vector3 world=transform.TransformPoint(point);
                point.y=(GroundHeight!=null?GroundHeight(world):transform.position.y)-transform.position.y+Mathf.Lerp(1.5f,0.9f,along)+layer*0.3f;
                int i=layer*(rings+1)*(segments+1)+row*(segments+1)+col;
                vertices[i]=point;uv[i]=new Vector2(across,along);
                if(row==rings||col==segments)continue;
                triangles[write++]=i;triangles[write++]=i+segments+1;triangles[write++]=i+1;
                triangles[write++]=i+1;triangles[write++]=i+segments+1;triangles[write++]=i+segments+2;
            }
            var mesh=new Mesh{name="Continuous layered breath volume",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();fanMaterial=new Material(BossFlameVfx.Flame);fanMaterial.SetFloat("_Fan",1);
            var root=new GameObject("Coherent breath flame volume");root.transform.SetParent(transform,false);
            BossFlameVfx.Attach(root,mesh,fanMaterial);
        }
    }
}
