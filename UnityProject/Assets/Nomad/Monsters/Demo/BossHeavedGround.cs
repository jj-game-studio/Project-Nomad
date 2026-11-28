using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossHeavedGround : MonoBehaviour
    {
        private readonly List<Transform> plates = new();
        private readonly List<Vector3> positions = new();
        private float since;
        private Material stone;
        private Mesh slab;

        public static void Create(Transform parent, BossArena arena, Vector3 center, float radius)
        {
            var root = new GameObject("Slam uplifted stone islands"); root.transform.SetParent(parent,false);
            var ground = root.AddComponent<BossHeavedGround>(); ground.since = Time.time;
            ground.stone = new Material(Resources.Load<Shader>("BossStone")) {color=new Color(0.3f,0.25f,0.22f)};
            ground.slab = BuildSlab();
            for (int i=0;i<14;i++)
            {
                Vector3 direction = Quaternion.Euler(0,i*137.5f,0)*Vector3.forward;
                float distance = Mathf.Min(radius*0.68f, 2.5f+i%4*2.1f);
                Vector3 point = arena.GroundPoint(center+direction*distance);
                var island=new GameObject("Heaved stone plate");
                island.transform.SetParent(root.transform,false);
                island.AddComponent<MeshFilter>().sharedMesh=ground.slab;
                island.AddComponent<MeshRenderer>().sharedMaterial=ground.stone;
                Transform plate=island.transform;
                var heat=new GameObject("Incandescent slab underside");heat.transform.SetParent(plate,false);
                heat.AddComponent<MeshFilter>().sharedMesh=ground.slab;
                heat.AddComponent<MeshRenderer>().sharedMaterial=BossFlameVfx.Molten;
                heat.transform.localScale=new Vector3(1.06f,0.35f,1.06f);
                heat.transform.localPosition=Vector3.down*0.35f;
                plate.position = point; plate.rotation = Quaternion.Euler(Random.Range(-12,12),i*73,Random.Range(-10,10));
                plate.localScale = new Vector3(Random.Range(1.7f,2.7f),1,Random.Range(1.7f,2.7f));
                ground.plates.Add(plate); ground.positions.Add(point);
            }
            BossRingBurst.Create(parent,arena,center,radius*0.8f,radius,0.4f);
            Destroy(root,4);
        }

        private void Update()
        {
            float t=Time.time-since;
            float lift=t<0.18f?Mathf.SmoothStep(0,0.7f,t/0.18f):Mathf.Lerp(0.7f,-0.8f,Mathf.Clamp01((t-0.4f)/3));
            for(int i=0;i<plates.Count;i++) plates[i].position=positions[i]+Vector3.up*lift;
        }

        private static Mesh BuildSlab()
        {
            Vector2[] corners={new(-1,-0.7f),new(-0.4f,-1),new(0.9f,-0.8f),new(1,0.3f),new(0.3f,1),new(-0.9f,0.7f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
            }
            for(int i=0;i<corners.Length;i++)
            {
                Vector2 p=corners[i],q=corners[(i+1)%corners.Length];
                Vector3 a=new(p.x,0.25f,p.y),b=new(q.x,0.25f,q.y),c=new(p.x,-0.5f,p.y),d=new(q.x,-0.5f,q.y);
                Triangle(Vector3.up*0.25f,b,a);
                Triangle(a,b,c);Triangle(c,b,d);
            }
            var mesh=new Mesh{name="Fractured thick basalt slab"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }

        private void OnDestroy(){if(stone!=null)Destroy(stone);if(slab!=null)Destroy(slab);}
    }
}
