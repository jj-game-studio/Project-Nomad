using System.Collections.Generic;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    // 폭이 불규칙하게 찢어지고 가지가 갈라지는 검은 틈과 깎인 단면입니다.
    public sealed class BossGroundCracks : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private float started;
        public int BranchCount { get; private set; }
        private readonly List<Vector3> vertices=new();
        private readonly List<Color> colors=new();
        private readonly List<Vector2> uv=new();
        private readonly List<int> triangles=new();

        public static void Create(Transform parent,BossArena arena,Vector3 center,float radius)
        {
            var root=new GameObject("Slam ground fissures");root.transform.SetParent(parent,true);
            root.transform.SetPositionAndRotation(center,Quaternion.identity);
            var cracks=root.AddComponent<BossGroundCracks>();cracks.started=Time.time;
            var random=new System.Random(Mathf.RoundToInt(center.x*41+center.z*73));
            for(int i=0;i<9;i++)
            {
                float angle=i*40+random.Next(-15,16);
                Vector3 heading=Quaternion.Euler(0,angle,0)*Vector3.forward;
                float length=Mathf.Min(radius*Range(random,0.78f,1.05f),arena.DistanceToBoundary(center,heading));
                var path=cracks.Branch(arena,center,heading,length,Range(random,0.6f,1.15f),random);
                if(i%3==0)continue;
                Vector3 branchHeading=Quaternion.Euler(0,(i%2==0?1:-1)*Range(random,28,62),0)*heading;
                cracks.Branch(arena,path[4],branchHeading,length*0.38f,0.35f,random);
            }
            cracks.mesh=new Mesh{name="Torn branching chasms"};
            cracks.mesh.SetVertices(cracks.vertices);cracks.mesh.SetColors(cracks.colors);
            cracks.mesh.SetUVs(0,cracks.uv);cracks.mesh.SetTriangles(cracks.triangles,0);cracks.mesh.RecalculateNormals();
            root.AddComponent<MeshFilter>().sharedMesh=cracks.mesh;
            cracks.material=new Material(Resources.Load<Shader>("BossFissure"));
            root.AddComponent<MeshRenderer>().sharedMaterial=cracks.material;

            var dust=WeaponVfx.CreateParticles(root.transform,"Cracked ground dust",BossFlameVfx.Smoke);
            var main=dust.main;main.startLifetime=new ParticleSystem.MinMaxCurve(0.3f,0.8f);
            main.startSize=new ParticleSystem.MinMaxCurve(0.3f,0.7f);main.startColor=new Color(0.35f,0.26f,0.19f,0.4f);
            WeaponVfx.Fade(dust,0);
            for(int i=0;i<60;i++)
            {
                Vector3 point=cracks.vertices[random.Next(cracks.vertices.Count)];
                dust.Emit(new ParticleSystem.EmitParams{position=root.transform.TransformPoint(point),velocity=Vector3.up*Range(random,1,3)},1);
            }
            BossStoneDebris.Create(parent,center+Vector3.up*0.2f);
            BossRingBurst.Create(parent,arena,center,radius*0.9f,radius,0.3f);
            Destroy(root,6);
        }

        private Vector3[] Branch(BossArena arena,Vector3 origin,Vector3 heading,float length,float width,System.Random random)
        {
            const int steps=9,across=5;
            var path=new Vector3[steps];int first=vertices.Count;
            Vector3 side=Vector3.Cross(Vector3.up,heading);
            for(int row=0;row<steps;row++)
            {
                float t=row/(float)(steps-1);
                path[row]=origin+heading*(length*t)+side*(row==0?0:Range(random,-0.65f,0.65f));
                Vector3 tangent=row==0?heading:(path[row]-path[row-1]).normalized;
                Vector3 normal=Vector3.Cross(Vector3.up,tangent).normalized;
                float left=width*(1-t)*Range(random,0.6f,1.35f),right=width*(1-t)*Range(random,0.6f,1.35f);
                float[] offsets={-left,-left*0.72f,0,right*0.72f,right};
                for(int col=0;col<across;col++)
                {
                    Vector3 point=path[row]+normal*offsets[col];point.y=arena.GroundHeight(point)+0.24f;
                    vertices.Add(transform.InverseTransformPoint(point));
                    bool edge=col==0||col==4;
                    colors.Add(edge?new Color(0.15f,0.105f,0.075f):new Color(0.009f,0.006f,0.004f));
                    uv.Add(new Vector2(col==1||col==3?0.025f:0,0));
                    if(row==0||col==0)continue;
                    int i=first+row*across+col;
                    triangles.Add(i-across-1);triangles.Add(i-1);triangles.Add(i);
                    triangles.Add(i-across-1);triangles.Add(i);triangles.Add(i-across);
                }
            }
            BranchCount++;return path;
        }

        private static float Range(System.Random random,float low,float high)=>Mathf.Lerp(low,high,(float)random.NextDouble());
        private void Update()
        {
            float age=Time.time-started;
            material.SetFloat("_Fade",Mathf.Clamp01((6-age)/1.5f));
            material.SetFloat("_Heat",Mathf.Clamp01(1-age/1.2f));
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
