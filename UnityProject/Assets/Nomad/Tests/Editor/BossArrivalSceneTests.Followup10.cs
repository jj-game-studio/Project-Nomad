using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Monsters.Demo;
using Nomad.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed partial class BossArrivalSceneTests
    {
        private void EnableRealDodges(float distance)
        {
            scene.Boss.GetComponent<BossArrival>().Skip();
            scene.GodMode=false;
            scene.Player.GetComponent<Health>().IsCombatImmune=false;
            scene.Player.GetComponent<PlayerStats>().RandomSource=()=>0.99f;
            scene.Player.GetComponent<PlayerMovement>().enabled=true;
            PlaceHero(scene.Boss.transform.position-Vector3.forward*distance);
        }

        [UnityTest]
        public IEnumerator Followup10InnerAndOuterBlastsCanBeJumpedButHitGroundedPlayer()
        {
            EnableRealDodges(6);yield return Wait(0.1f);
            var hp=scene.Player.GetComponent<Health>();
            foreach(var pattern in new[]{DragonPattern.InnerBlast,DragonPattern.OuterBlast})
            {
                float distance=pattern==DragonPattern.InnerBlast?6:10;
                PlaceHero(scene.Boss.transform.position-Vector3.forward*distance);
                scene.Boss.BeginPattern(pattern);int before=hp.CurrentHealth;
                yield return Wait(scene.Boss.Remaining-0.25f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return Wait(0.27f);
                Assert.That(scene.Player.transform.position.y-scene.Arena.GroundHeight(scene.Player.transform.position),Is.GreaterThan(0.55f));
                yield return Wait(0.65f);
                Assert.That(hp.CurrentHealth,Is.EqualTo(before),pattern+" 실제 점프 회피");
                scene.Boss.BeginPattern(pattern);yield return Wait(1.5f);
                Assert.That(hp.CurrentHealth,Is.LessThan(before),pattern+" 지상에는 피해");
                hp.RestoreFull();yield return Wait(0.5f);
            }
        }

        [UnityTest]
        public IEnumerator Followup10ThreeAlternatingWavesAlsoAllowRealJumps()
        {
            EnableRealDodges(9);yield return Wait(0.1f);
            var boss=scene.Boss;var hp=scene.Player.GetComponent<Health>();int before=hp.CurrentHealth;
            boss.BeginPattern(DragonPattern.OrbitFlames);
            float impact=Time.time+boss.Remaining;
            for(int round=0;round<3;round++)
            {
                PlaceHero(boss.transform.position-Vector3.forward*(round==1?12:9));
                while(Time.time<impact-0.25f)yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                while(Time.time<impact+0.5f)yield return null;
                Assert.That(hp.CurrentHealth,Is.EqualTo(before),"교대 폭발 "+round);
                impact+=BossRingSequence.WarningSeconds+BossRingSequence.BlastSeconds+BossRingSequence.GapSeconds;
            }
        }

        [UnityTest]
        public IEnumerator Followup10LaserWaitsOneSecondAfterLockAndRealRollEscapes()
        {
            EnableRealDodges(15);yield return Wait(0.1f);
            var boss=scene.Boss;var hp=scene.Player.GetComponent<Health>();int before=hp.CurrentHealth;
            boss.BeginPattern(DragonPattern.AimedBreath);
            var line=boss.Effects.GetComponentInChildren<BossLineBreath>();
            while(!line.Locked)yield return null;
            Assert.That(line.Fired,Is.False);
            Assert.That(line.FiresAt-Time.time,Is.InRange(0.95f,1.01f));
            Vector3 heading=line.Heading;
            foreach(Vector3 point in Enumerable.Range(0,line.GetComponent<LineRenderer>().positionCount).Select(i=>line.GetComponent<LineRenderer>().GetPosition(i)))
                Assert.That(point.y-scene.Arena.GroundHeight(point),Is.GreaterThan(0.3f));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A,Key.LeftShift));yield return Wait(0.05f);
            Assert.That(scene.Player.GetComponent<PlayerMovement>().IsRolling,Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            while(Time.time<line.FiresAt+0.55f)yield return null;
            Assert.That(line.Fired,Is.True);Assert.That(Vector3.Angle(heading,line.Heading),Is.LessThan(0.1f));
            Assert.That(scene.Player.GetComponent<PlayerMovement>().IsRolling,Is.False);
            Assert.That(line.Contains(scene.Player.transform.position),Is.False);
            Assert.That(hp.CurrentHealth,Is.EqualTo(before));
            var beam=line.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Solid flame beam core");
            Assert.That(beam.widthMultiplier,Is.EqualTo(BossLineBreath.HalfWidth*2));
            Assert.That(beam.positionCount,Is.GreaterThan(10));
            Assert.That(beam.widthCurve.Evaluate(1),Is.EqualTo(1),"사거리 끝까지 폭 유지");
        }

        [UnityTest]
        public IEnumerator Followup10TenSecondDeadlineOverridesQueuedNonParryAttack()
        {
            var boss=scene.Boss;boss.GetComponent<BossArrival>().Skip();
            scene.Player.GetComponent<PlayerMovement>().enabled=false;PlaceHero(boss.transform.position-Vector3.forward*15);
            TestKit.SetField(boss,"lastParryPatternAt",Time.time-10.1f);
            for(int i=0;i<100;i++)Assert.That(DragonBossCombat.IsParryPattern(boss.ChooseNextPattern(15)),Is.True);
            TestKit.SetField(boss,"queuedPattern",(DragonPattern?)DragonPattern.AimedBreath);
            boss.AutomaticPatterns=true;yield return Wait(0.3f);boss.AutomaticPatterns=false;
            Assert.That(DragonBossCombat.IsParryPattern(boss.Pattern),Is.True);
            Assert.That(boss.AttacksStarted,Is.EqualTo(1));Assert.That(boss.ParryPatternGap,Is.LessThan(0.3f));
        }

        [UnityTest]
        public IEnumerator Followup10LateSlamParryCancelsGraceDamageAndBreaksArmorOnce()
        {
            EnableRealDodges(5);scene.Player.GetComponent<PlayerMovement>().enabled=false;
            var boss=scene.Boss;var hp=scene.Player.GetComponent<Health>();int before=hp.CurrentHealth;
            boss.BeginPattern(DragonPattern.Circle);var signal=boss.GetComponent<BossParrySignal>();
            while(Time.time<signal.ImpactAt+0.1f)yield return null;
            Assert.That(hp.CurrentHealth,Is.EqualTo(before));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(0.6f);
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().Successes,Is.EqualTo(1));
            Assert.That(hp.CurrentHealth,Is.EqualTo(before));
            Assert.That(boss.GetComponent<BossArmor>().RemainingRatio,Is.EqualTo(0.55f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Followup10OrdinaryHomingOrbContactAcceptsLateParry()
        {
            EnableRealDodges(8);scene.Player.GetComponent<PlayerMovement>().enabled=false;
            scene.Player.transform.rotation=Quaternion.identity;
            var boss=scene.Boss;var hp=scene.Player.GetComponent<Health>();int before=hp.CurrentHealth;
            Vector3 center=scene.Player.transform.position+Vector3.up*1.3f;
            var ball=BossFireball.Launch(boss.Effects,boss.transform,hp,center+Vector3.forward,center-Vector3.forward,0.05f,false,radius:1);
            yield return Wait(0.18f);Assert.That(hp.CurrentHealth,Is.EqualTo(before));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(0.8f);
            Assert.That(scene.Player.GetComponent<PlayerBossParry>().Reflections,Is.EqualTo(1));
            Assert.That(hp.CurrentHealth,Is.EqualTo(before));
            Assert.That(boss.GetComponent<Health>().CurrentHealth,Is.EqualTo(boss.GetComponent<Health>().MaxHealth));
        }

        [UnityTest]
        public IEnumerator Followup10EnragedMeleeGoldPulseFollowsCompletedWarning()
        {
            var boss=scene.Boss;boss.GetComponent<BossArrival>().Skip();scene.EnterEnrage();
            yield return Wait(BossEnrageTransition.Seconds+0.1f);
            scene.Player.GetComponent<PlayerMovement>().enabled=false;
            PlaceHero(boss.transform.position-Vector3.forward*5);
            float began=Time.time;boss.BeginPattern(DragonPattern.BodySweep);
            var signal=boss.GetComponent<BossParrySignal>();
            while(!signal.Flashing)yield return null;
            Assert.That(Time.time-began,Is.GreaterThanOrEqualTo(0.1f));
            var warning=boss.Effects.GetComponentInChildren<BossHazard>();
            Mesh mesh=warning.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertices.Max(v=>new Vector2(v.x,v.z).magnitude),Is.EqualTo(boss.Tuning.BodySweepRadius).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator Followup10EnrageAcceleratesAttacksButKeepsLaserLockTime()
        {
            var boss=scene.Boss;boss.GetComponent<BossArrival>().Skip();scene.EnterEnrage();
            yield return Wait(BossEnrageTransition.Seconds+0.1f);
            Assert.That(boss.Tempo,Is.EqualTo(1.5f));
            boss.BeginPattern(DragonPattern.AimedBreath);
            var line=boss.Effects.GetComponentInChildren<BossLineBreath>();
            while(!line.Locked)yield return null;
            Assert.That(line.FiresAt-Time.time,Is.InRange(0.95f,1.01f));
            Assert.That(line.Fired,Is.False);
        }
    }
}
