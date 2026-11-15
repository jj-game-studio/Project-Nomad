using Nomad.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(200)]
    public sealed class MonsterCombatDemo : MonoBehaviour
    {
        [SerializeField] private Health player;
        [SerializeField] private Health monster;
        [SerializeField] private MonsterMeleeAttack attack;
        [SerializeField] private Renderer monsterBody;
        [SerializeField] private LineRenderer heavyWarning;
        [SerializeField] private MeshFilter warningFill;
        [SerializeField] private string opponentLabel = "Basic monster";

        private MaterialPropertyBlock tint;
        private GUIStyle label;
        private bool restarting;
        private Color idleColor;
        private Mesh fillMesh;
        private Renderer fillRenderer;
        private float fillAngle = -1f;

        private void Awake()
        {
            tint = new MaterialPropertyBlock();
            idleColor = monsterBody.sharedMaterial.GetColor("_BaseColor");
            if (warningFill != null)
            {
                fillMesh = new Mesh { name = "Attack warning fill" };
                warningFill.sharedMesh = fillMesh;
                fillRenderer = warningFill.GetComponent<Renderer>();
                fillRenderer.enabled = false;
            }
        }

        private void Update()
        {
            Color color = monster.IsDead ? Color.gray : attack.Phase == MeleePhase.Windup ?
                (attack.IsHeavyAttack ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.5f, 0.1f)) : idleColor;
            tint.SetColor("_BaseColor", color);
            monsterBody.SetPropertyBlock(tint);
            UpdateWarning();

            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
                Restart();
        }

        private void OnGUI()
        {
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 18 };

            GUI.Box(new Rect(12f, 12f, 505f, 185f), GUIContent.none);
            GUILayout.BeginArea(new Rect(25f, 22f, 475f, 165f));
            GUILayout.Label("MELEE COMBAT TEST - " + opponentLabel, label);
            GUILayout.Label($"Player HP {player.CurrentHealth}/{player.MaxHealth}    " +
                $"Monster HP {monster.CurrentHealth}/{monster.MaxHealth}   Speed {attack.AttackSpeed:0.#}x", label);
            GUILayout.Label("WASD move | LMB shoot | Shift roll | R restart", label);
            string state = monster.IsDead ? "MONSTER DEFEATED" : player.IsDead ?
                "PLAYER DEFEATED" : attack.Phase.ToString().ToUpperInvariant();
            if (attack.Phase == MeleePhase.Windup)
                state += $" {Mathf.RoundToInt(attack.WindupProgress * 100f)}%";
            string hint = attack.IsHeavyAttack ? "   HEAVY: dodge the red sector" : "   Orange = preparing strike";
            GUILayout.Label("Attack: " + state + hint, label);
            if (GUILayout.Button("Restart combat (R)", GUILayout.Height(30f)))
                Restart();
            GUILayout.EndArea();
        }

        private void UpdateWarning()
        {
            if (heavyWarning == null)
                return;

            heavyWarning.enabled = !monster.IsDead && attack.Phase == MeleePhase.Windup;
            if (fillRenderer != null)
                fillRenderer.enabled = heavyWarning.enabled;
            if (!heavyWarning.enabled)
            {
                if (warningFill != null)
                    warningFill.transform.localScale = Vector3.zero;
                return;
            }

            Vector3 origin = monster.transform.position + Vector3.up * 0.04f;
            bool circle = attack.CurrentAttackAngle >= 359.9f;
            heavyWarning.positionCount = circle ? 17 : 19;
            if (!circle)
                heavyWarning.SetPosition(0, origin);
            for (int i = 0; i <= 16; i++)
            {
                float angle = -attack.CurrentAttackAngle * 0.5f + attack.CurrentAttackAngle * i / 16f;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * attack.AttackDirection;
                heavyWarning.SetPosition(circle ? i : i + 1, origin + direction * attack.CurrentAttackRange);
            }
            if (!circle)
                heavyWarning.SetPosition(18, origin);

            if (warningFill == null)
                return;

            if (!Mathf.Approximately(fillAngle, attack.CurrentAttackAngle))
                BuildFill(attack.CurrentAttackAngle);
            warningFill.transform.SetPositionAndRotation(origin - Vector3.up * 0.015f,
                Quaternion.LookRotation(attack.AttackDirection));
            warningFill.transform.localScale = Vector3.one * (attack.CurrentAttackRange * attack.WindupProgress);
            tint.SetColor("_BaseColor", attack.IsHeavyAttack ?
                new Color(1f, 0.15f, 0.05f, 0.35f) : new Color(1f, 0.55f, 0.1f, 0.3f));
            fillRenderer.SetPropertyBlock(tint);
        }

        private void BuildFill(float angle)
        {
            fillAngle = angle;
            var vertices = new Vector3[34];
            var triangles = new int[96];
            for (int i = 0; i <= 32; i++)
                vertices[i + 1] = Quaternion.AngleAxis(-angle * 0.5f + angle * i / 32f, Vector3.up) * Vector3.forward;
            for (int i = 0; i < 32; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (fillMesh != null)
                Destroy(fillMesh);
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
