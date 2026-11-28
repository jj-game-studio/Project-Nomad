using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Monsters.Demo;
using Nomad.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nomad.Tests
{
    public sealed partial class BossArrivalSceneTests
    {
        [UnityTest]
        public IEnumerator Followup22HealthLinesAndFittedShellFollowWholeModel()
        {
            var boss=scene.Boss; boss.GetComponent<BossArrival>().Skip();
            yield return Wait(0.15f);
            var skins=boss.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name.Contains("fitted carapace")).ToArray();
            Assert.That(skins.Length,Is.GreaterThanOrEqualTo(2));
            Assert.That(skins.All(r=>r.bones.Length>0&&r.sharedMesh!=null),Is.True);
            Assert.That(boss.GetComponentsInChildren<Text>().Any(t=>t.text=="×10"),Is.True);
            Health hp=boss.GetComponent<Health>();
            hp.TryApplyDamage(Mathf.FloorToInt(hp.MaxHealth*0.79f),DamageKind.Fall);
            yield return null;
            Assert.That(boss.EnrageRequested,Is.False);
            hp.TryApplyDamage(Mathf.CeilToInt(hp.MaxHealth*0.02f),DamageKind.Fall);
            yield return null;
            Assert.That(boss.EnrageRequested,Is.True);
            Assert.That(boss.GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),Is.True);
            boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
            yield return null;
            Assert.That(boss.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.name=="Outer fitted carapace"),Is.False);
            Assert.That(boss.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.name=="Inner fitted carapace"),Is.True);
        }

        [UnityTest]
        public IEnumerator Followup22SelectionSeparatesRangedAndMeleeAndRemovesLob()
        {
            var boss=scene.Boss; boss.GetComponent<BossArrival>().Skip();
            var far=new System.Collections.Generic.HashSet<DragonPattern>();
            for(int i=0;i<1000;i++)far.Add(boss.ChooseNextPattern(18));
            Assert.That(far.Contains(DragonPattern.FirePool),Is.False);
            Assert.That(far.Contains(DragonPattern.BodySweep)||far.Contains(DragonPattern.RedSweep),Is.False);
            Assert.That(far.Contains(DragonPattern.TrackingFireball)&&far.Contains(DragonPattern.AimedBreath),Is.True);
            Assert.That(far.Contains(DragonPattern.InnerBlast)&&far.Contains(DragonPattern.OuterBlast),Is.True);
            Assert.That(boss.Tuning.RainWaves,Is.EqualTo(17));
            Assert.That(boss.Tuning.RainFallSeconds,Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Followup22RedSweepCannotBeParriedAndGoldArmsSignal()
        {
            var boss=scene.Boss; boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<PlayerMovement>().enabled=false;
            PlaceHero(boss.transform.position+Vector3.forward*5);
            boss.BeginPattern(DragonPattern.RedSweep);
            Assert.That(boss.Pattern,Is.EqualTo(DragonPattern.RedSweep));
            Assert.That(boss.GetComponent<BossParrySignal>().Armed,Is.False);
            yield return Wait(1.5f);
            PlaceHero(boss.transform.position+Vector3.forward*5);
            boss.BeginPattern(DragonPattern.BodySweep);
            Assert.That(boss.GetComponent<BossParrySignal>().Armed,Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Followup22LavaAvoidsBossAndLeavesVisiblePools()
        {
            var boss=scene.Boss;boss.GetComponent<BossArrival>().Skip();
            boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
            yield return Wait(3.4f);
            boss.BeginPattern(DragonPattern.LavaEruption);
            var eruptions=boss.Effects.GetComponentsInChildren<BossLavaEruption>();
            Assert.That(eruptions.Length,Is.GreaterThanOrEqualTo(4));
            Assert.That(eruptions.All(e=>Vector3.Distance(e.transform.position,boss.transform.position)>9),Is.True);
            yield return Wait(boss.Tuning.LavaEruptionWarning+0.9f);
            Assert.That(boss.Effects.GetComponentsInChildren<BossHazard>().Any(h=>h.Active&&h.Irregular),Is.True);
        }

        [UnityTest]
        public IEnumerator Followup22LineBreathLocksThenAllowsSideEscape()
        {
            var boss=scene.Boss;boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<PlayerMovement>().enabled=false;
            PlaceHero(boss.transform.position+Vector3.forward*15);
            boss.BeginPattern(DragonPattern.AimedBreath);
            yield return Wait(1.25f);
            var line=boss.Effects.GetComponentInChildren<BossLineBreath>();
            Assert.That(line,Is.Not.Null);
            Vector3 heading=line.Heading;
            PlaceHero(scene.Player.transform.position+Vector3.right*8);
            yield return Wait(1.2f);
            Assert.That(line.Fired,Is.True);
            Assert.That(Vector3.Angle(heading,line.Heading),Is.LessThan(1));
            Assert.That(line.Contains(scene.Player.transform.position),Is.False);
            Vector3 far=line.transform.TransformPoint(new Vector3(0,0,25));
            Assert.That(line.Contains(far),Is.False,"불꽃이 도착하기 전에 원거리 선판정하지 않음");
            yield return Wait(0.45f);
            Assert.That(line.Contains(far),Is.True,"불꽃 도달 후 같은 직사각형 안에 피해 적용");
        }

        [UnityTest]
        public IEnumerator Followup22TrackingBallReflectsWithoutBossHpDamage()
        {
            var boss=scene.Boss;boss.GetComponent<BossArrival>().Skip();
            var hp=scene.Player.GetComponent<Health>();
            var ball=BossFireball.Launch(boss.Effects,boss.transform,hp,scene.Player.transform.position+Vector3.forward*3+Vector3.up,scene.Player.transform.position,4,false);
            ball.Homing=true; yield return Wait(0.15f);
            int before=boss.GetComponent<Health>().CurrentHealth;
            Assert.That(ball.Reflect(scene.Player.transform),Is.True);
            Assert.That(ball.Homing,Is.False);
            yield return Wait(1);
            Assert.That(boss.GetComponent<Health>().CurrentHealth,Is.EqualTo(before));
            Assert.That(boss.GetComponent<BossArmor>().RemainingRatio,Is.LessThan(1));
        }

        [UnityTest]
        public IEnumerator Followup22StaffTapIsFreeAndHeldShotCostsMana()
        {
            scene.Equip("weapon-apprentice-staff");
            yield return Wait(0.2f);
            var combat=scene.Player.GetComponent<PlayerEquipmentCombat>();
            var mana=scene.Player.GetComponent<PlayerMana>();
            Vector2 pointer=Camera.main.WorldToScreenPoint(scene.Player.transform.position+Vector3.forward*9+Vector3.up);
            float before=mana.Current;
            Assert.That(combat.TryAttack(pointer),Is.True);
            Assert.That(mana.Current,Is.EqualTo(before));
            Assert.That(combat.LastShotPower,Is.EqualTo(1.1f));
            yield return Wait(1.5f);
            Assert.That(combat.TryBeginCharge(pointer),Is.True);
            // 마우스 자동 취소 경로와 분리해 실제 릴리스 메서드의 유지 시간/비용을 검증합니다.
            combat.enabled=false;
            combat.enabled=true;
            Assert.That(combat.TryBeginCharge(pointer),Is.True);
            float started=Time.time;
            while(Time.time-started<0.65f)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=pointer,buttons=1});
                yield return null;
            }
            before=mana.Current;
            Assert.That(combat.TryReleaseCharge(pointer),Is.True);
            Assert.That(mana.Current,Is.LessThan(before));
            Assert.That(combat.LastShotPower,Is.GreaterThan(2));
        }

        [UnityTest]
        public IEnumerator Followup22UnlimitedResourcesRemainSpendableAndCanBeDisabled()
        {
            var mana=scene.Player.GetComponent<PlayerMana>();var stamina=scene.Player.GetComponent<PlayerStamina>();
            scene.UnlimitedResources=true;yield return null;
            float mp=mana.Current,sp=stamina.Current;
            Assert.That(mana.TrySpend(1000),Is.True);Assert.That(stamina.TrySpend(1000),Is.True);
            Assert.That(mana.Current,Is.EqualTo(mp));Assert.That(stamina.Current,Is.EqualTo(sp));
            scene.UnlimitedResources=false;yield return null;
            Assert.That(mana.TrySpend(1000),Is.False);Assert.That(stamina.TrySpend(1000),Is.False);
        }
    }
}
