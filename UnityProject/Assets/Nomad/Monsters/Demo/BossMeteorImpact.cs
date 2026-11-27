using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossMeteorImpact : MonoBehaviour
    {
        private float age;
        public static void Create(Transform parent, Vector3 point, float radius)
        {
            var root=new GameObject("Meteor impact crater and ejecta");root.transform.SetParent(parent,false);root.transform.position=point;

            root.AddComponent<BossMeteorImpact>();BossStoneDebris.Create(root.transform,point);
            for(int index=0;index<3;index++)
            {
                Vector3 offset=Quaternion.Euler(0,index*120,0)*Vector3.forward*radius*0.3f;
                BossFlameVfx.Jet(root.transform,offset,offset*2+Vector3.up*(2+index*0.3f),radius*0.45f,0.02f);
            }
            var sparks=WeaponVfx.CreateParticles(root.transform,"Meteor impact molten fragments",WeaponVfx.Glow);

            var main=sparks.main;main.startLifetime=0.65f;main.startSize=0.16f;main.startSpeed=5;main.gravityModifier=1;
            main.startColor=new Color(3,0.9f,0.035f);
            var shape=sparks.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Hemisphere;shape.radius=radius*0.5f;
            sparks.transform.localRotation=Quaternion.Euler(-90,0,0);WeaponVfx.Fade(sparks,0);sparks.Emit(45);
        }
        private void Update()
        {
            age+=Time.deltaTime;

            foreach(Transform child in transform)if(child.name=="Volumetric flame jet")child.localScale=new Vector3(1,Mathf.Max(0.05f,1-age*2.5f),1);
            if(age>0.45f)foreach(Transform child in transform)if(child.name=="Volumetric flame jet")child.gameObject.SetActive(false);
            if(age>1.1f)Destroy(gameObject);
        }
    }
}
