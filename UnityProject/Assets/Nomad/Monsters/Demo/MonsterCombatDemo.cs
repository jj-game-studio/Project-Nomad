using System.Collections.Generic;
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
        private MonsterHitFeedback hitFeedback;
        private Renderer[] bodies;
        public bool ShowSkillWarnings { get; set; } = true;

        // 경고 표시를 지형에 맞출 때의 샘플 간격과 지면 위 띄움 높이입니다(언덕 Terrain에 묻히지 않게).
        private const float SampleSpacing = 0.5f;
        private const float OutlineLift = 0.07f;
        private const float FillLift = 0.05f;
        private readonly List<Vector3> corners = new List<Vector3>();
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

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
            Transform visual = monster.transform.Find("Body");
            bodies = visual != null ? visual.GetComponentsInChildren<Renderer>() : new[] { monsterBody };
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
            foreach (Renderer body in bodies)
            {
                tint.Clear();
                tint.SetColor("_BaseColor", color);
                Texture texture = body.sharedMaterial.GetTexture("_BaseMap");
                tint.SetTexture("_BaseMap", hitFeedback.IsFlashing ? Texture2D.whiteTexture : texture != null ? texture : Texture2D.whiteTexture);
                body.SetPropertyBlock(tint);
            }
            tint.Clear();
            UpdateWarning();

        }

        private void UpdateWarning()
        {
            if (heavyWarning == null)
                return;

            heavyWarning.enabled = ShowSkillWarnings && !monster.IsDead && attack.Phase == MeleePhase.Windup && attack.IsSkillAttack;
            if (fillRenderer != null)
                fillRenderer.enabled = heavyWarning.enabled;
            if (!heavyWarning.enabled)
            {
                if (warningFill != null)
                    warningFill.transform.localScale = Vector3.zero;
                return;
            }

            Vector3 origin = monster.transform.position;
            corners.Clear();
            if (attack.Skill == MonsterSkill.Charge)
            {
                Vector3 forward = attack.AttackDirection;
                Vector3 side = Quaternion.AngleAxis(90f, Vector3.up) * forward * (attack.ChargeWidth * 0.5f);
                Vector3 end = origin + forward * attack.CurrentAttackRange;
                corners.AddRange(new[] { origin - side, origin + side, end + side, end - side, origin - side });
                SetOutline(origin.y);

                if (warningFill == null)
                    return;

                // 돌진 폭 방향 2칸, 길이 방향은 샘플 간격마다 나눈 격자로 채웁니다.
                float length = attack.CurrentAttackRange * attack.WindupProgress;
                BuildGrid(Divisions(length), 2, (row, rows, column) => origin + forward * (length * row / rows) +
                    side * (column - 1f), origin.y);
                return;
            }

            float range = attack.CurrentAttackRange;
            float angle = attack.CurrentAttackAngle;
            bool circle = angle >= 359.9f;
            if (!circle)
                corners.Add(origin);
            for (int i = 0; i <= 16; i++)
                corners.Add(origin + Direction(-angle * 0.5f + angle * i / 16f) * range);
            if (!circle)
                corners.Add(origin);
            SetOutline(origin.y);

            if (warningFill == null)
                return;

            // 부채꼴은 각도 32칸 x 반지름 방향 샘플 간격 고리로 나눠 언덕을 따라 휘게 합니다.
            float radius = range * attack.WindupProgress;
            BuildGrid(Divisions(radius), 32, (ring, rings, step) =>
                origin + Direction(-angle * 0.5f + angle * step / 32f) * (radius * ring / rings), origin.y);
            tint.SetColor("_BaseColor", attack.IsHeavyAttack ?
                new Color(1f, 0.15f, 0.05f, 0.35f) : new Color(1f, 0.55f, 0.1f, 0.3f));
            fillRenderer.SetPropertyBlock(tint);
        }

        private Vector3 Direction(float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * attack.AttackDirection;

        private static int Divisions(float length) => Mathf.Max(1, Mathf.CeilToInt(length / SampleSpacing));

        // 외곽선 꼭짓점 사이를 샘플 간격으로 나누고 각 점을 지면 위로 올립니다.
        private void SetOutline(float fallbackY)
        {
            points.Clear();
            for (int i = 0; i < corners.Count; i++)
            {
                if (i > 0)
                {
                    Vector3 from = corners[i - 1];
                    int steps = Divisions(Vector3.Distance(from, corners[i]));
                    for (int step = 1; step < steps; step++)
                        points.Add(OnGround(Vector3.Lerp(from, corners[i], (float)step / steps), fallbackY, OutlineLift));
                }

                points.Add(OnGround(corners[i], fallbackY, OutlineLift));
            }

            heavyWarning.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                heavyWarning.SetPosition(i, points[i]);
        }

        // (rows+1) x (columns+1) 격자 꼭짓점을 지면에 맞춰 채움 메시를 다시 만듭니다.
        // 위에서 볼 때 시계 방향으로 감아 기존 채움과 같은 면이 보입니다.
        private void BuildGrid(int rows, int columns, System.Func<int, int, int, Vector3> point, float fallbackY)
        {
            Transform fill = warningFill.transform;
            fill.localPosition = Vector3.zero;
            fill.localRotation = Quaternion.identity;
            fill.localScale = Vector3.one;

            points.Clear();
            for (int row = 0; row <= rows; row++)
            {
                for (int column = 0; column <= columns; column++)
                    points.Add(fill.InverseTransformPoint(OnGround(point(row, rows, column), fallbackY, FillLift)));
            }

            triangles.Clear();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int a = row * (columns + 1) + column;
                    int d = a + columns + 1;
                    triangles.AddRange(new[] { a, d, d + 1, a, d + 1, a + 1 });
                }
            }

            fillMesh.Clear();
            fillMesh.SetVertices(points);
            fillMesh.SetTriangles(triangles, 0);
            fillMesh.RecalculateBounds();
        }

        // 필드 Terrain 위에서는 표면 높이를, Terrain 밖(마을·검토 씬)에서는 기존처럼 몬스터 발 높이를 씁니다.
        private static Vector3 OnGround(Vector3 point, float fallbackY, float lift)
        {
            point.y = fallbackY;
            foreach (UnityEngine.Terrain terrain in UnityEngine.Terrain.activeTerrains)
            {
                Vector3 local = point - terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                    continue;

                point.y = terrain.GetPosition().y + terrain.SampleHeight(point);
                break;
            }

            point.y += lift;
            return point;
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
