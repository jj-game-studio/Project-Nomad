using System.Collections.Generic;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    // 불지대의 평면 용암 재질을 사용하지 않습니다. 수직 분출 후 갈라진 잔열만 남습니다.
    public sealed class BossLavaEruption : MonoBehaviour
    {
        private float burstAt, expires, radius;
        private bool erupted;
        private static Material column;

        private readonly List<Transform> jets = new();
        public int JetCount => jets.Count;

        public static void Create(Transform parent, Vector3 point, float warning, float radius)
        {
            var root=new GameObject("Lava fissure eruption");root.transform.SetParent(parent,false);
            root.transform.position=point+Vector3.up*0.2f;
            var eruption=root.AddComponent<BossLavaEruption>();
            eruption.burstAt=Time.time+warning;eruption.expires=eruption.burstAt+4.1f;eruption.radius=radius;
        }

        private void Burst()
        {
            BossStoneDebris.Create(transform,transform.position);
            if(column==null)column=new Material(Resources.Load<Shader>("BossEruptionFlame"));
            for(int index=0;index<3;index++)
            {
                Vector3 offset=Quaternion.Euler(0,index*120,0)*Vector3.forward*radius*0.25f;
                var ribbon = new GameObject("Eruption rising flame ribbon").AddComponent<LineRenderer>();
                ribbon.transform.SetParent(transform,false);
                ribbon.useWorldSpace=false; ribbon.sharedMaterial=column;
                ribbon.positionCount=16; ribbon.widthMultiplier=radius*(0.8f-index*0.12f);
                ribbon.startColor=new Color(2.6f,1.2f,0.08f); ribbon.endColor=new Color(1.5f,0.12f,0.005f,0);
                ribbon.widthCurve=new AnimationCurve(new Keyframe(0,0.9f),new Keyframe(0.25f,1),new Keyframe(1,0));
                for(int step=0;step<16;step++)
                {
                    float t=step/15f;
                    ribbon.SetPosition(step,offset*(1-t)+new Vector3(Mathf.Sin(t*12+index)*0.18f,t*(7-index),Mathf.Cos(t*15+index)*0.15f));
                }
                jets.Add(ribbon.transform);

            }
            for(int index=0;index<12;index++)
            {
                var crack=new GameObject("Eruption glowing fissure").AddComponent<LineRenderer>();
                crack.transform.SetParent(transform,false);crack.useWorldSpace=false;crack.sharedMaterial=WeaponVfx.Beam;
                crack.positionCount=5;crack.startWidth=0.26f;crack.endWidth=0.02f;
                crack.startColor=new Color(2.3f,0.6f,0.015f);crack.endColor=new Color(1,0.12f,0.005f);

                for(int step=0;step<5;step++)crack.SetPosition(step,Quaternion.Euler(0,index*30+Mathf.Sin(step*7+index)*9,0)*Vector3.forward*(radius*step/4f));
            }
            var plume=WeaponVfx.CreateParticles(transform,"Molten eruption jet",WeaponVfx.Glow);
            plume.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=plume.main;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(0.5f,0.9f);
            main.startSize=new ParticleSystem.MinMaxCurve(0.12f,0.45f);main.startSpeed=new ParticleSystem.MinMaxCurve(7,12);
            main.startColor=new Color(2.5f,0.45f,0.015f);main.gravityModifier=1.5f;

            var shape=plume.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=20;shape.radius=radius*0.3f;
            plume.transform.localRotation=Quaternion.Euler(-90,0,0);
            var emit=plume.emission;emit.enabled=true;emit.rateOverTime=0;
            emit.SetBursts(new[]{new ParticleSystem.Burst(0,95),new ParticleSystem.Burst(0.2f,60)});
            WeaponVfx.Fade(plume,0.1f);plume.Play();
            var smoke=WeaponVfx.CreateParticles(transform,"Eruption stone smoke",BossFlameVfx.Smoke);
            var smokeMain=smoke.main;smokeMain.startLifetime=1;smokeMain.startSize=new ParticleSystem.MinMaxCurve(0.6f,1.1f);
            smokeMain.startColor=new Color(0.3f,0.19f,0.12f,0.4f);
            var smokeEmission=smoke.emission;smokeEmission.enabled=false;
            WeaponVfx.Fade(smoke,0);
            for(int i=0;i<45;i++)smoke.Emit(new ParticleSystem.EmitParams{position=transform.position,velocity=Random.insideUnitSphere*3+Vector3.up*3},1);
        }

        private void Update()
        {
            if(!erupted&&Time.time>=burstAt){erupted=true;Burst();}
            if(erupted)
            {
                float t=Time.time-burstAt;
                foreach(Transform jet in jets)
                    if(jet!=null){jet.localScale=new Vector3(1,Mathf.Max(0.1f,Mathf.Sin(Mathf.Clamp01(t/1.25f)*Mathf.PI)),1);if(t>1.25f)jet.gameObject.SetActive(false);}
            }
            if(Time.time>=expires)Destroy(gameObject);

        }
    }
}
