using Nomad.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.Monsters.Demo
{
    public sealed class MonsterCombatDemo : MonoBehaviour
    {
        [SerializeField] private Health player;
        [SerializeField] private Health monster;
        [SerializeField] private MonsterMeleeAttack attack;
        [SerializeField] private Renderer monsterBody;

        private MaterialPropertyBlock tint;
        private GUIStyle label;
        private bool restarting;

        private void Awake()
        {
            tint = new MaterialPropertyBlock();
        }

        private void Update()
        {
            Color color = monster.IsDead ? Color.gray : attack.Phase == MeleePhase.Windup ?
                new Color(1f, 0.5f, 0.1f) : new Color(0.35f, 0.7f, 0.2f);
            tint.SetColor("_BaseColor", color);
            monsterBody.SetPropertyBlock(tint);

            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
                Restart();
        }

        private void OnGUI()
        {
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 18 };

            GUI.Box(new Rect(12f, 12f, 505f, 185f), GUIContent.none);
            GUILayout.BeginArea(new Rect(25f, 22f, 475f, 165f));
            GUILayout.Label("MELEE COMBAT TEST - placeholder models", label);
            GUILayout.Label($"Player HP {player.CurrentHealth}/{player.MaxHealth}    " +
                $"Monster HP {monster.CurrentHealth}/{monster.MaxHealth}", label);
            GUILayout.Label("WASD move | LMB shoot | Shift roll | R restart", label);
            string state = monster.IsDead ? "MONSTER DEFEATED" : player.IsDead ?
                "PLAYER DEFEATED" : attack.Phase.ToString().ToUpperInvariant();
            GUILayout.Label("Monster: " + state + "   Orange = preparing strike", label);
            if (GUILayout.Button("Restart combat (R)", GUILayout.Height(30f)))
                Restart();
            GUILayout.EndArea();
        }

        private void Restart()
        {
            if (restarting)
                return;

            restarting = true;
            // 전용 씬의 개체/발사체/체력을 함께 재생성하며 공통 Health 계약은 바꾸지 않습니다.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(gameObject.scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
#endif
        }
    }
}
