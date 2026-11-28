using System;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Nomad.Game.Editor
{
    public static class BossFollowupVerification
    {
        private const string Key="Nomad.Boss22Verify";
        private static readonly DragonPattern[] Patterns={DragonPattern.Circle,DragonPattern.RadialFireballs,DragonPattern.Breath,
            DragonPattern.Rain,DragonPattern.BodySweep,DragonPattern.RedSweep,DragonPattern.OrbitFlames,
            DragonPattern.TrackingFireball,DragonPattern.AimedBreath,DragonPattern.InnerBlast,DragonPattern.OuterBlast,DragonPattern.LavaEruption};
        private static readonly float[] CaptureAt={1.9f,1.95f,2.25f,3f,1.1f,1.1f,1.4f,1.15f,2.8f,1.4f,1.4f,1.3f};
        private static DragonBossCombat boss;
        private static BossArena arena;
        private static PlayerEquipmentCombat player;
        private static float since;
        private static int stage,index,frame;
        private static bool requestedField;
        private static bool capturedLock;
        private static double deadline;
        private static string error;

        public static void RunDebug()=>Begin("debug","Assets/Nomad/Monsters/BossDebug.unity");
        public static void RunField()=>Begin("field","Assets/Nomad/Game/Game.unity");
        public static void RunTerrain()=>Begin("terrain","Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity");
        private static void Begin(string mode,string scene)
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("전용 검증 Editor에서만 실행합니다.");
            EditorSceneManager.OpenScene(scene);
            SessionState.SetBool(Key,true);SessionState.SetString(Key+".mode",mode);
            Resume();EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=OnLog;Application.logMessageReceived+=OnLog;
        }

        private static void OnLog(string message,string trace,LogType type)
        {
            if(type==LogType.Exception&&trace.Contains("UnityEditor.Search.SearchDatabase")&&!trace.Contains("Nomad."))return;
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=message;
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup<deadline,"시간 초과 stage="+stage);
                Require(error==null,error);
                if(!Application.isPlaying||Time.frameCount==frame)return;
                frame=Time.frameCount;
                if(SessionState.GetString(Key+".mode","debug")=="field")
                {
                    var manager=GameManager.Instance;
                    if(manager==null||manager.IsLoading)return;
                    if(!requestedField){requestedField=manager.LoadRegion("Field");return;}
                }
                if(boss==null)
                {
                    boss=Object.FindFirstObjectByType<DragonBossCombat>();
                    player=Object.FindFirstObjectByType<PlayerEquipmentCombat>();
                    if(boss==null||player==null)return;
                    var debug=Object.FindFirstObjectByType<BossDebugBootstrap>();
                    if(debug!=null&&!debug.Ready){boss=null;return;}
                    arena=Object.FindFirstObjectByType<BossArena>();
                    foreach(var monster in Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None))
                        if(monster.Rank!=MonsterRank.Boss)monster.GetComponent<Health>().TryApplyDamage(int.MaxValue);
                    boss.AutomaticPatterns=false;boss.AutomaticRockfall=false;
                    boss.GetComponent<BossArrival>().Skip();
                    player.GetComponent<PlayerMovement>().enabled=false;
                    player.GetComponent<Health>().IsInvulnerable=true;
                    Time.captureFramerate=60;Application.runInBackground=true;
                    Place(8);since=Time.time;return;
                }
                if(stage==0)
                {
                    if(Time.time<since+0.3f)return;
                    Capture("FittedArmor",true);
                    stage=1;
                }
                if(stage==1)
                {
                    if(boss.Phase==DragonPhase.Stunned&&boss.Remaining>0)return;
                    boss.GetComponent<NavMeshAgent>().Warp(arena.Center);
                    Place(Patterns[index]==DragonPattern.BodySweep||Patterns[index]==DragonPattern.RedSweep?5:9);
                    boss.BeginPattern(Patterns[index]);
                    capturedLock=false;
                    Require(boss.Pattern==Patterns[index],"패턴 실행 "+Patterns[index]);
                    since=Time.time;stage=2;return;
                }
                if(stage==2)
                {
                    if(Patterns[index]==DragonPattern.AimedBreath&&!capturedLock&&Time.time>since+1.35f)
                    {
                        var line=boss.Effects.GetComponentInChildren<BossLineBreath>();
                        Require(line.Locked&&!line.Fired,"레이저 방향 고정 후 발사 대기");
                        Capture("AimedBreathLocked");capturedLock=true;
                    }
                    if(Time.time<since+CaptureAt[index])return;
                    Capture(Patterns[index].ToString());
                    if(Patterns[index]==DragonPattern.Circle)
                        Require(boss.Effects.GetComponentInChildren<BossGroundCracks>()?.GetComponent<MeshFilter>()?.sharedMesh.vertexCount>500,"검은 분기 균열 메시");
                    if(Patterns[index]==DragonPattern.AimedBreath)
                        Require(boss.Effects.GetComponentInChildren<BossLineBreath>()?.Fired==true,"직선 브레스 발사");
                    boss.Stun(0.03f);since=Time.time;
                    index++;
                    if(index==6)
                    {
                        boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
                        stage=3;return;
                    }
                    stage=index<Patterns.Length?1:4;return;
                }
                if(stage==3)
                {
                    if(Time.time<since+0.18f)return;
                    Capture("FirstBreak",true);stage=1;return;
                }
                if(stage==4)
                {
                    if(boss.Phase==DragonPhase.Stunned&&boss.Remaining>0)return;
                    boss.DropRocks();since=Time.time;stage=5;return;
                }
                if(stage==5)
                {
                    if(Time.time<since+2)return;
                    Require(arena.RockCount>0&&arena.RockCount<=6,"실제 낙석/개수 상한");Capture("Rockfall");
                    boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
                    since=Time.time;stage=6;return;
                }
                if(stage==6)
                {
                    if(Time.time<since+0.18f)return;
                    Capture("SecondBreak",true);
                    var hp=boss.GetComponent<Health>();hp.TryApplyDamage(hp.CurrentHealth-Mathf.FloorToInt(hp.MaxHealth*0.2f),DamageKind.Fall);
                    since=Time.time;stage=7;return;
                }
                if(stage==7)
                {
                    if(Time.time<since+4.6f)return;
                    Require(boss.Enraged,"남은2줄 광폭");Capture("EnrageTwoLines");
                    boss.GetComponent<Health>().TryApplyDamage(int.MaxValue,DamageKind.Fall);
                    since=Time.time;stage=8;return;
                }
                if(stage==8)
                {
                    if(Time.time<since+2.25f)return;
                    Require(GameObject.Find("Boss final ash explosion")!=null,"사망 폭발");Capture("DeathBurst");
                    Debug.Log("BOSS_FOLLOWUP22_PASS mode="+SessionState.GetString(Key+".mode","debug")+" patterns="+Patterns.Length+" armor=2 enrage=20% rocks=pass death=pass");
                    Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }

        private static void Place(float distance)
        {
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;
            player.transform.position=arena.GroundPoint(boss.transform.position-arena.Forward*distance)+Vector3.up*0.03f;
            controller.enabled=true;player.transform.LookAt(boss.transform.position);Physics.SyncTransforms();
        }

        private static void Capture(string name,bool close=false)
        {
            var camera=Camera.main;Vector3 old=camera.transform.position;Quaternion rotation=camera.transform.rotation;float size=camera.orthographicSize;
            var impulse=camera.GetComponent<HitCameraImpulse>();bool shake=impulse!=null&&impulse.enabled;
            if(shake)impulse.enabled=false;
            try
            {
                camera.transform.position=close?boss.transform.position+new Vector3(6,7,-7):arena.Center+new Vector3(12,34,-20);
                camera.transform.LookAt(close?boss.transform.position+Vector3.up*1.5f:arena.Center);
                camera.orthographicSize=close?5:25;
                Nomad.Minimap.Editor.MinimapVerification.Capture("follow22-"+SessionState.GetString(Key+".mode","debug")+"-"+name+".png");
            }
            finally{camera.transform.SetPositionAndRotation(old,rotation);camera.orthographicSize=size;if(shake&&impulse!=null)impulse.enabled=true;}
        }

        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Finish(int code)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            EditorApplication.Exit(code);
        }
    }
}
