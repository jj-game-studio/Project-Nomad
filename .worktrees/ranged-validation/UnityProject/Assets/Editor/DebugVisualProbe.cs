using System;
using System.IO;
using System.Linq;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class DebugVisualProbe
{
    const string Key="Nomad.Debug.VisualProbe";
    static int stage, frame=-1;
    static bool placed;
    static double deadline;
    static MonsterIdentity[] actors;
    static MonsterIdentity current;
    static Transform player;
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Nomad/Monsters/MonsterDebug.unity");
        SessionState.SetBool(Key,true); Hook(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Hook()
    {
        EditorApplication.update-=Tick;
        if(!SessionState.GetBool(Key,false)) return;
        stage=0; placed=false; deadline=EditorApplication.timeSinceStartup+80;
        EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("visual timeout");
            if(!EditorApplication.isPlaying || Time.time<1f || frame==Time.frameCount) return;
            frame=Time.frameCount;
            if(stage==0)
            {
                actors=Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None);
                if(actors.Length!=5) return;
                Save("monster-debug-overview");
                Object.FindFirstObjectByType<MonsterDebugArena>().enabled=false;
                player=GameObject.Find("Player").transform;
                player.GetComponent<Nomad.Player.PlayerMovement>().enabled=false;
                foreach(var a in actors) a.gameObject.SetActive(false);
                stage=1;
            }
            if(!placed)
            {
                current=stage<=3 ? actors.Single(a=>a.Rank==MonsterRank.Normal && (int)a.Weapon==stage-1) : actors.Single(a=>a.Rank==(stage==4 ? MonsterRank.Elite : MonsterRank.Boss));
                current.gameObject.SetActive(true);
                current.GetComponent<NavMeshAgent>().Warp(new Vector3(3,0,-2));
                current.GetComponent<NavMeshAgent>().speed=0;
                current.transform.rotation=Quaternion.identity;
                current.GetComponent<MonsterTracker>().Configure(20,30,0);
                player.position=new Vector3(3,0,stage==1 ? -0.6f : 4f);
                Physics.SyncTransforms();
                Camera.main.orthographicSize=stage>3 ? 4f : 2.3f;
                Camera.main.transform.position=current.transform.position+new Vector3(3,2.5f,5);
                Camera.main.transform.LookAt(current.transform.position+Vector3.up*(stage>3 ? 1.8f : 1f));
                placed=true; if(stage==4) Save("monster-golem-front-idle");
                return;
            }
            var attack=current.GetComponent<MonsterMeleeAttack>();
            if(attack.Phase!=MeleePhase.Windup || attack.WindupProgress<0.8f) return;
            Save(stage<=3 ? "monster-weapon-"+current.Weapon : stage==4 ? "monster-golem-front-throw" : "monster-dragon-stable");
            current.gameObject.SetActive(false);
            stage++; placed=false;
            if(stage>5)
            {
                Debug.Log("DEBUG_VISUAL_PASS: overview, three weapon poses, golem front arms and stable dragon captured");
                SessionState.SetBool(Key,false);
                EditorApplication.update-=Tick;
                EditorApplication.Exit(0);
            }
        }
        catch(Exception e) {Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static void Save(string name)
    {
        var camera=Camera.main;
        var rt=new RenderTexture(1200,700,24);
        var previous=RenderTexture.active;
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(1200,700,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1200,700),0,0); image.Apply();
        File.WriteAllBytes("Artifacts/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=previous;
        Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);
    }
}