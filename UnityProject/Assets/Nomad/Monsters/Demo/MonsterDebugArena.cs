using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(80)]
    public sealed class MonsterDebugArena : MonoBehaviour
    {
        private MonsterTestPanel panel;
        private Camera view;
        private Transform player;
        private bool follow;

        private void Start()
        {
            panel = GetComponent<MonsterTestPanel>();
            view = Camera.main;
            player = GameObject.Find("Player").transform;
            ResetLineup();
        }

        public void ResetLineup()
        {
            panel.ClearMonsters();
            panel.SetInvincible(true);
            panel.ShowDetection(true);
            panel.SpawnNormalStyle(0, new Vector3(-8f, 0f, 3f));
            panel.SpawnNormalStyle(1, new Vector3(-4f, 0f, 7f));
            panel.SpawnNormalStyle(2, new Vector3(0f, 0f, 11f));
            panel.SetKind(1);
            panel.SpawnAt(new Vector3(6f, 0f, 13f));
            panel.SetKind(2);
            panel.SpawnAt(new Vector3(10f, 0f, 22f));
            panel.SetKind(0);
        }

        private void LateUpdate()
        {
            if (Keyboard.current?.f2Key.wasPressedThisFrame == true) follow = !follow;
            Vector3 center = follow ? player.position : new Vector3(0f, 0f, 8f);
            view.orthographic = true;
            view.orthographicSize = follow ? 10f : 21f;
            view.transform.SetPositionAndRotation(center + new Vector3(0f, 30f, -22f),
                Quaternion.LookRotation(new Vector3(0f, -30f, 22f)));
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12f, Screen.height - 100f, 370f, 88f),
                "MONSTER DEBUG: Sword / Crossbow / Staff / Golem / Dragon\nWASD + shoot | F2 overview/follow | invincible by default");
            if (GUI.Button(new Rect(22f, Screen.height - 40f, 345f, 25f), "Reset five-monster lineup")) ResetLineup();
        }
    }
}
