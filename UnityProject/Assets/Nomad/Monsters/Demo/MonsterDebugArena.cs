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
            // 플레이어가 소환 자리에 서 있으면 한 종류만 충돌 검사에서 빠질 수 있어 시작 위치도 복원합니다.
            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            player.position = new Vector3(0f, 0.05f, -5f);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            panel.SetInvincible(true);
            panel.ShowDetection(true);
            panel.SpawnNormalStyle(0, new Vector3(-7f, 0f, 4f));
            panel.SpawnNormalStyle(1, new Vector3(0f, 0f, 4f));
            panel.SpawnNormalStyle(2, new Vector3(7f, 0f, 4f));
            panel.SetKind(1);
            panel.SpawnAt(new Vector3(-7f, 0f, 16f));
            panel.SetKind(2);
            panel.SpawnAt(new Vector3(7f, 0f, 21f));
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
