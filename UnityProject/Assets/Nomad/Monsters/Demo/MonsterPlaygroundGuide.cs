using Nomad.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Nomad.Monsters.Demo
{
    // 몬스터 전용 테스트 씬의 조작 안내와 반복 확인용 초기화입니다.
    public sealed class MonsterPlaygroundGuide : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private MonsterTracker monster;

        private Vector3 playerStart;
        private Vector3 monsterStart;
        private Quaternion monsterRotation;
        private GUIStyle textStyle;

        private void Awake()
        {
            playerStart = player.position;
            monsterStart = monster.transform.position;
            monsterRotation = monster.transform.rotation;
        }

        private void Update()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
                ResetActors();
        }

        private void OnGUI()
        {
            if (textStyle == null)
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };

            GUI.Box(new Rect(12f, 12f, 470f, 155f), GUIContent.none);
            GUILayout.BeginArea(new Rect(25f, 22f, 445f, 140f));
            GUILayout.Label("MONSTER PLAYGROUND", textStyle);
            GUILayout.Label("BLUE: Player    GREEN: Monster", textStyle);
            GUILayout.Label("WASD: Move   Space: Jump   Shift: Roll", textStyle);
            string state = monster.PathBlocked ? "NO PATH" : monster.IsTracking ? "CHASING" : "IDLE";
            GUILayout.Label($"Monster: {state}    Sees player: {monster.HasLineOfSight}", textStyle);
            if (GUILayout.Button("Reset positions (R)", GUILayout.Height(28f)))
                ResetActors();
            GUILayout.EndArea();

            GUI.Box(new Rect(12f, Screen.height - 48f, 660f, 34f),
                "Try: hide behind the red wall, then run up the yellow stairs. Tracking only; no combat.");
        }

        private void ResetActors()
        {
            var movement = player.GetComponent<PlayerMovement>();
            var controller = player.GetComponent<CharacterController>();
            movement.enabled = false;
            controller.enabled = false;
            player.SetPositionAndRotation(playerStart, Quaternion.identity);
            controller.enabled = true;
            movement.enabled = true;

            monster.BindTarget(null);
            var agent = monster.GetComponent<NavMeshAgent>();
            if (agent.isOnNavMesh)
                agent.Warp(monsterStart);
            monster.transform.rotation = monsterRotation;
            monster.BindTarget(player);
            Physics.SyncTransforms();
        }
    }
}
