using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossRingBurst : MonoBehaviour
    {
        private float age, duration;
        private Transform flames;
        public float Age => age;

        public static void Create(Transform parent, BossArena arena, Vector3 center, float inner, float outer, float duration)
        {
            var root = new GameObject("Distance blast fissures and flame wall");
            root.transform.SetParent(parent, false);
            root.transform.position = arena.GroundPoint(center);
            var burst = root.AddComponent<BossRingBurst>(); burst.duration = duration;
            burst.flames = new GameObject("Rising ring flame curtains").transform;

            burst.flames.SetParent(root.transform, false);
            if (inner > 0) burst.Wall(arena, inner, 1, Mathf.Min(0.8f, outer - inner));
            burst.Wall(arena, outer, -1, Mathf.Min(0.8f, outer - inner));
            for (int ray = 0; ray < 24; ray++)
            {
                var line = new GameObject("Ring impact fracture").AddComponent<LineRenderer>();
                line.transform.SetParent(root.transform, false);

                line.sharedMaterial = WeaponVfx.Beam; line.positionCount = 5;
                line.startWidth = 0.18f; line.endWidth = 0.035f;
                line.startColor = new Color(2,0.4f,0.02f); line.endColor = new Color(1,0.08f,0.005f);
                for (int step = 0; step < 5; step++)
                {
                    Vector3 heading = Quaternion.Euler(0,ray*15+Mathf.Sin(step*11+ray)*2,0)*Vector3.forward;
                    line.SetPosition(step,arena.GroundPoint(center+heading*Mathf.Lerp(inner,outer,step/4f))+Vector3.up*0.3f);

                }
            }
        }

        private void Wall(BossArena arena, float radius, int inward, float width)
        {
            const int segments=96, rows=4;
            var vertices=new Vector3[(segments+1)*(rows+1)]; var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*rows*6]; int triangle=0;
            for(int row=0;row<=rows;row++)
            for(int col=0;col<=segments;col++)
            {
                float t=row/(float)rows, turn=col/(float)segments;

                Vector3 heading=new Vector3(Mathf.Sin(turn*Mathf.PI*2),0,Mathf.Cos(turn*Mathf.PI*2));
                Vector3 point=heading*(radius+inward*width*t);
                point.y=arena.GroundHeight(transform.position+point)-transform.position.y+0.28f+t*(1.4f+Mathf.Sin(turn*91)*0.35f);
                int index=row*(segments+1)+col; vertices[index]=point; uv[index]=new Vector2(turn*8,t);
                if(row==rows||col==segments)continue;
                triangles[triangle++]=index;triangles[triangle++]=index+1;triangles[triangle++]=index+segments+1;
                triangles[triangle++]=index+1;triangles[triangle++]=index+segments+2;triangles[triangle++]=index+segments+1;

            }
            var mesh=new Mesh{name="Erupting annular flame wall",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();
            var wall=new GameObject("Flame ring boundary");wall.transform.SetParent(flames,false);
            BossFlameVfx.Attach(wall,mesh,BossFlameVfx.Flame);
        }

        private void Update()
        {
            age+=Time.deltaTime;
            flames.localScale=new Vector3(1,Mathf.Max(0.12f,Mathf.Sin(Mathf.Clamp01(age/duration)*Mathf.PI)),1);
            if(age>=duration)Destroy(gameObject);
        }
    }
}
