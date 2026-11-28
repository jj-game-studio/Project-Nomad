using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossLineBreath : MonoBehaviour
    {
        public const float Range = 26, HalfWidth = 1.25f;
        public const float AimSeconds = 1.15f, LockSeconds = 1f, TravelSeconds = 0.45f;
        private const int Segments = 32;
        private DragonBossCombat boss;
        private Health target;
        private BossArena arena;
        private float fireAt, endAt, nextHit;
        private LineRenderer warning, beam;
        private Mesh fill;
        private MeshRenderer fillRenderer;
        private Material beamMaterial;
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 3];
        private ParticleSystem fire;
        public bool Fired { get; private set; }
        public bool Locked => Time.time >= fireAt - LockSeconds;
        public float FiresAt => fireAt;
        public Vector3 Heading => transform.forward;

        public static BossLineBreath Create(Transform parent, DragonBossCombat owner, BossArena room, Health player, float delay)
        {
            var root = new GameObject("Locked straight breath");
            root.transform.SetParent(parent, false);
            var attack = root.AddComponent<BossLineBreath>();
            attack.boss = owner; attack.target = player; attack.arena = room;
            attack.fireAt = Time.time + delay; attack.endAt = attack.fireAt + 1.3f / owner.Tempo;
            root.transform.SetPositionAndRotation(room.GroundPoint(owner.Mouth.position),owner.transform.rotation);
            attack.BuildWarning();
            return attack;
        }

        private void BuildWarning()
        {
            warning = gameObject.AddComponent<LineRenderer>();
            warning.sharedMaterial = WeaponVfx.Beam;
            warning.loop = true; warning.positionCount = (Segments + 1) * 2;
            warning.widthMultiplier = 0.1f;

            var root = new GameObject("Ground fitted laser warning"); root.transform.SetParent(transform,false);
            fill = new Mesh {name="Laser range and width"};
            var triangles = new int[Segments * 12];
            int write=0;
            for(int row=0;row<Segments;row++)
            for(int col=0;col<2;col++)
            {
                int i=row*3+col;
                triangles[write++]=i;triangles[write++]=i+3;triangles[write++]=i+1;
                triangles[write++]=i+1;triangles[write++]=i+3;triangles[write++]=i+4;
            }
            fill.vertices=vertices;fill.triangles=triangles;
            root.AddComponent<MeshFilter>().sharedMesh=fill;
            fillRenderer=root.AddComponent<MeshRenderer>();fillRenderer.sharedMaterial=WeaponVfx.Plain;
            RefreshWarning();
        }

        public void SetHeading(Vector3 heading)
        {
            if(!Locked && heading.sqrMagnitude>0.01f)transform.rotation=Quaternion.LookRotation(heading);
        }

        public bool Contains(Vector3 point)
        {
            Vector3 local=transform.InverseTransformPoint(point);
            float reach=Fired?Range*Mathf.Clamp01((Time.time-fireAt)/TravelSeconds):Range;
            return Mathf.Abs(local.x)<=HalfWidth && local.z>=0 && local.z<=reach && Mathf.Abs(local.y)<3;
        }

        private void RefreshWarning()
        {
            // 긴 모서리의 중간도 지면에 투영해 굴곡 아래로 선이 내려가지 않습니다.
            for(int row=0;row<=Segments;row++)
            for(int col=0;col<3;col++)
            {
                Vector3 local=new((col-1)*HalfWidth,0,Range*row/Segments);
                Vector3 world=transform.TransformPoint(local);world.y=arena.GroundHeight(world)+0.38f;
                vertices[row*3+col]=transform.InverseTransformPoint(world);
            }
            fill.vertices=vertices;fill.RecalculateBounds();
            for(int row=0;row<=Segments;row++)
            {
                warning.SetPosition(row,transform.TransformPoint(vertices[row*3]));
                warning.SetPosition(Segments+1+row,transform.TransformPoint(vertices[(Segments-row)*3+2]));
            }
            Color color=Locked?new Color(2.5f,0.22f,0.015f):new Color(1.2f,0.06f,0.01f);
            warning.widthMultiplier=Locked?0.16f:0.09f;
            warning.startColor=warning.endColor=color;
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",new Color(color.r,color.g,color.b,Locked?0.22f:0.1f));
            fillRenderer.SetPropertyBlock(tint);
        }

        private void Fire()
        {
            Fired=true;warning.enabled=false;fillRenderer.enabled=false;
            fire=WeaponVfx.CreateParticles(transform,"Focused breath jet",WeaponVfx.Glow);
            var emission=fire.emission;emission.enabled=false;
            var main=fire.main;main.startLifetime=TravelSeconds;main.startSpeed=0;
            main.startColor=new Color(2.8f,0.65f,0.03f);
            WeaponVfx.Fade(fire,0);WeaponVfx.Shrink(fire);

            beam=new GameObject("Solid flame beam core").AddComponent<LineRenderer>();
            beam.transform.SetParent(transform,false);
            beamMaterial=new Material(Resources.Load<Shader>("BossLaserFlame"));beam.sharedMaterial=beamMaterial;
            beam.positionCount=17;beam.widthMultiplier=HalfWidth*2;
            beam.widthCurve=new AnimationCurve(new Keyframe(0,0.5f),new Keyframe(0.08f,1),new Keyframe(1,1));
            beam.startColor=new Color(2,0.5f,0.02f);beam.endColor=new Color(1,0.1f,0.005f,0.6f);
            BossCombatImpact.Pulse(0.16f);
        }

        private void Update()
        {
            if(boss==null||target==null||target.IsDead){Destroy(gameObject);return;}
            if(!Fired)
            {
                if(!Locked)transform.position=arena.GroundPoint(boss.Mouth.position);
                RefreshWarning();
                if(Time.time<fireAt)return;
                Fire();
            }
            float reach=(Range-0.6f)*Mathf.Clamp01((Time.time-fireAt)/TravelSeconds);
            Vector3 endpoint=transform.position+transform.forward*reach+Vector3.up*1.2f;
            for(int i=0;i<beam.positionCount;i++)beam.SetPosition(i,Vector3.Lerp(boss.Mouth.position,endpoint,i/(float)(beam.positionCount-1)));
            for(int i=0;i<Mathf.CeilToInt(Time.deltaTime*550);i++)
            {
                float side=Random.Range(-0.65f,0.65f);
                Vector3 start=boss.Mouth.position+transform.right*side;
                Vector3 end=transform.position+transform.forward*(Range-0.6f)+transform.right*side+Vector3.up*1.2f;
                fire.Emit(new ParticleSystem.EmitParams{position=start,velocity=(end-start)/TravelSeconds,startSize=Random.Range(0.8f,1.2f)},1);
            }
            if(Time.time>=nextHit)
            {
                nextHit=Time.time+0.5f;
                if(Contains(target.transform.position))target.TryApplyDamage(boss.ScaleDamage(16));
            }
            if(Time.time>=endAt)Destroy(gameObject);
        }

        private void OnDestroy(){if(fill!=null)Destroy(fill);if(beamMaterial!=null)Destroy(beamMaterial);}
    }
}
