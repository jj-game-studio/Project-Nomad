using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    // 생성 필드 밖에 붙이는 작은 보스 전용 구역입니다. 원본 NavMesh·지형은 변경하지 않습니다.
    public sealed partial class BossArena : MonoBehaviour
    {
        public const float HalfSize = 22f;
        public Vector3 Center { get; private set; }
        public Vector3 Entrance { get; private set; }
        public bool UsesGate { get; private set; }
        public Vector3 Forward { get; private set; }
        public Vector3 Arrival => Center - Forward * (HalfSize - 3);
        public bool Connected => navigation.valid && (standalone || UsesGate || link.valid);
        private bool standalone;

        private NavMeshData data;
        private NavMeshDataInstance navigation;
        private NavMeshLinkInstance link;
        private TextMesh sign;
        private readonly List<Material> materials = new();

        public bool Build(Vector3 entrance, Vector3 outward, float edgeDistance, float floorHeight = float.NaN, bool useGate = false)
            => BuildArea(entrance, outward, edgeDistance, floorHeight, useGate, false);

        public bool BuildStandalone(Vector3 entrance, Vector3 outward) => BuildArea(entrance, outward, 0, float.NaN, false, true);

        private bool BuildArea(Vector3 entrance, Vector3 outward, float edgeDistance, float floorHeight, bool useGate, bool standalone)
        {
            this.standalone = standalone;
            UsesGate = useGate;
            Entrance = entrance;
            outward = Vector3.ProjectOnPlane(outward, Vector3.up).normalized;
            Forward = outward;
            Center = entrance + outward * (edgeDistance + 3f + HalfSize);
            Center = new Vector3(Center.x, float.IsNaN(floorHeight) ? entrance.y : floorHeight, Center.z);
            Material stone = Material(new Color(0.27f, 0.30f, 0.29f));
            Material trim = Material(new Color(0.61f, 0.49f, 0.27f));
            Quaternion rotation = Quaternion.LookRotation(outward);
            Floor("Boss arena floor", Center, new Vector3(HalfSize * 2, 0.6f, HalfSize * 2), rotation, stone);
            BuildGround(stone);
            Vector3 rampStart = entrance - outward * 0.5f;
            Vector3 rampEnd = entrance + outward * Mathf.Max(0.5f, edgeDistance - 0.5f);
            rampEnd.y = Center.y;
            Vector3 rampDirection = rampEnd - rampStart;
            if (!useGate && !standalone) Floor("Approach ramp", (rampStart + rampEnd) * 0.5f,
                new Vector3(4, 0.6f, rampDirection.magnitude), Quaternion.LookRotation(rampDirection), stone);
            Vector3 bridgeEnd = entrance + outward * (edgeDistance + 5f);
            bridgeEnd.y = Center.y;
            if (!useGate && !standalone) Floor("Approach bridge", (rampEnd + bridgeEnd) * 0.5f,
                new Vector3(4, 0.6f, Vector3.Distance(rampEnd, bridgeEnd)), rotation, stone);

            // 경계 표식은 바닥 위 장식이며 진입·회피 동선을 막지 않습니다.
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            for (int index = 0; index < 4; index++)
            {
                Vector3 corner = Center + right * (index % 2 == 0 ? -HalfSize + 1 : HalfSize - 1) + outward * (index < 2 ? -HalfSize + 1 : HalfSize - 1);
                Decor("Arena pillar", GroundPoint(corner) + Vector3.up * 1.2f, new Vector3(0.8f, 2.4f, 0.8f), rotation, trim);
            }
            for (int index = 0; index < 2; index++)
                Decor("Entrance marker", Center - outward * (HalfSize - 1) + right * (index == 0 ? -3 : 3) + Vector3.up * 1.5f,
                    new Vector3(0.65f, 3, 0.65f), rotation, trim);

            sign = new GameObject("Boss arena sign").AddComponent<TextMesh>();
            sign.transform.SetParent(transform, false);
            sign.transform.position = Center - outward * (HalfSize - 1) + Vector3.up * 2.8f;
            sign.font = Resources.Load<Font>("ShopFonts/Jua-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<Renderer>().sharedMaterial = sign.font.material;
            sign.text = "용의 제단";
            sign.anchor = TextAnchor.MiddleCenter;
            sign.fontSize = 48;
            sign.characterSize = 0.025f;
            sign.color = new Color(1, 0.85f, 0.5f);
            Dress(rotation, stone, trim);

            // 통로의 필드 안쪽은 기존 NavMesh를 그대로 사용하고 외곽 부분만 별도로 굽습니다.
            var sources = new List<NavMeshBuildSource>();
            sources.Add(GroundSource());
            float navLength = 4.4f;
            Vector3 navCenter = entrance + outward * (edgeDistance + 0.6f + navLength * 0.5f);
            navCenter.y = Center.y;
            if (!useGate && !standalone) sources.Add(BoxSource(navCenter,
                new Vector3(4, 0.6f, navLength), rotation));
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            var bounds = new Bounds(Center, new Vector3(HalfSize * 2 + 12, 12, HalfSize * 2 + 12));
            bounds.Encapsulate(entrance);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
                return false;

            navigation = NavMesh.AddNavMeshData(data);
            if (standalone) return NavMesh.SamplePosition(Center, out _, 1f, NavMesh.AllAreas);
            if (useGate)
            {
                if (!NavMesh.SamplePosition(Center, out NavMeshHit centerHit, 1f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(entrance, out _, 1f, NavMesh.AllAreas)) return false;
                Center = centerHit.position;
                BossGate.Create(Entrance, Arrival, rotation, transform, "보스 구역 입장");
                BossGate.Create(Arrival, Entrance, rotation * Quaternion.Euler(0, 180, 0), transform, "필드로 돌아가기");
                return Connected;
            }
            Vector3 outer = entrance + outward * (edgeDistance + 2f);
            outer.y = Center.y;
            if (!NavMesh.SamplePosition(entrance, out NavMeshHit start, 1f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(outer, out NavMeshHit end, 1f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(Center, out NavMeshHit boss, 1f, NavMesh.AllAreas))
                return false;

            link = NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = start.position, endPosition = end.position, width = 1f,
                bidirectional = true, costModifier = -1, agentTypeID = settings.agentTypeID
            });
            Center = boss.position;
            var path = new NavMeshPath();
            return Connected && NavMesh.CalculatePath(start.position, Center, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete;
        }

        private static NavMeshBuildSource BoxSource(Vector3 top, Vector3 size, Quaternion rotation) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box, size = size,
            transform = Matrix4x4.TRS(top - Vector3.up * size.y * 0.5f, rotation, Vector3.one), area = 0
        };

        private void Floor(string name, Vector3 top, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject box = Decor(name, top - rotation * Vector3.up * size.y * 0.5f, size, rotation, material);
            box.AddComponent<BoxCollider>();
        }

        private GameObject Decor(string name, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(transform, false);
            box.transform.SetPositionAndRotation(position, rotation);
            box.transform.localScale = size;
            // 바닥만 물리 충돌에 참여합니다. 장식은 보행·NavMesh 예약에 섞지 않습니다.
            Collider original = box.GetComponent<Collider>();
            original.enabled = false;
            Destroy(original);
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private Material Material(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            materials.Add(material);
            return material;
        }

        private void LateUpdate()
        {
            if (sign != null && Camera.main != null)
                sign.transform.rotation = Camera.main.transform.rotation;
        }

        private void OnDisable()
        {
            ClearEncounter();
            if (link.valid) link.Remove();
            if (navigation.valid) navigation.Remove();
        }

        private void OnDestroy()
        {
            ClearProgression();
            ClearEncounter();
            if (data != null) Destroy(data);
            if (groundMesh != null) Destroy(groundMesh);
            if (groundTexture != null) Destroy(groundTexture);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
