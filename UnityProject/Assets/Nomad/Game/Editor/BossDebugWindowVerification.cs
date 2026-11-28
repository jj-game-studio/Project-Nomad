using System;
using System.Linq;
using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Game.Editor
{
    // 이 검증 전용 인자를 준 자체 실행 Editor만 재생/종료합니다. 공용 검토본에서는 실행하지 않습니다.
    public static class BossDebugWindowVerification
    {
        private static double deadline;
        private static bool reported;

        public static void Run()
        {
            if (!Environment.GetCommandLineArgs().Contains("-nomadBossWindowCheck"))
                throw new InvalidOperationException("자체 창 검증 실행 인자가 필요합니다.");

            EditorSceneManager.OpenScene("Assets/Nomad/Monsters/BossDebug.unity");
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            var debug = UnityEngine.Object.FindFirstObjectByType<BossDebugBootstrap>();
            if (!reported && EditorApplication.isPlaying && debug != null && debug.Ready)
            {
                debug.GodMode = true;
                debug.Boss.AutomaticPatterns = false;
                debug.Boss.AutomaticRockfall = false;
                BossEncounterDebugWindow.Open();
                reported = true;
                Debug.Log("BOSS_FLOATING_WINDOW_READY scene=" + debug.gameObject.scene.name);
            }
            if (EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Tick;
            EditorApplication.Exit(reported ? 0 : 1);
        }
    }
}
