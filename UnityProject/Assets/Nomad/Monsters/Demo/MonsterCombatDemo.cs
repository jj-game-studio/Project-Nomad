using Nomad.Combat;
using UnityEngine;

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

        private MaterialPropertyBlock tint;
        private Color idleColor;
        private Mesh fillMesh;
        private Renderer fillRenderer;
        private float fillAngle = -1f;
        private MonsterHitFeedback hitFeedback;

        public void ConfigureDisplay(Health victim, Health owner, MonsterMeleeAttack melee,
            Renderer body, LineRenderer outline, MeshFilter fill)
        {
            player = victim;
            monster = owner;
            attack = melee;
            monsterBody = body;
            heavyWarning = outline;
            warningFill = fill;
        }

        private void Awake()
        {
            tint = new MaterialPropertyBlock();
            hitFeedback = monster.GetComponent<MonsterHitFeedback>();
            if (hitFeedback == null)
                hitFeedback = monster.gameObject.AddComponent<MonsterHitFeedback>();
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
            if (hitFeedback != null && hitFeedback.IsFlashing)
                color = Color.white;
            tint.SetColor("_BaseColor", color);
            monsterBody.SetPropertyBlock(tint);
            UpdateWarning();

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
            if (heavyWarning != null)
                Destroy(heavyWarning.gameObject);
            if (warningFill != null)
                Destroy(warningFill.gameObject);
        }

    }
}
