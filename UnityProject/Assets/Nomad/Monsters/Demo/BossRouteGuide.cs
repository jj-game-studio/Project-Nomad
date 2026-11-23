using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed class BossRouteGuide : MonoBehaviour
    {
        private BossArena arena;
        private Health player;
        private Health boss;
        private NavMeshPath path;
        private readonly Vector3[] corners = new Vector3[64];
        private readonly Transform[] dashes = new Transform[5];
        private Material material;
        private float nextUpdate;
        public bool Available => arena != null && boss != null && !boss.IsDead && player != null && !player.IsDead;
        public Vector3 Goal => Vector3.Distance(player.transform.position, arena.Center) < BossArena.HalfSize * 1.5f ?
            arena.Center : arena.Entrance;
        public BossArena Arena => arena;

        private void Awake() => path = new NavMeshPath();

        public void Configure(BossArena room, Health observer, Health enemy)
        {
            arena = room;
            player = observer;
            boss = enemy;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.57f, 0.53f, 0.38f) };
            for (int index = 0; index < dashes.Length; index++)
            {
                GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = "Quiet route dash";
                dash.transform.SetParent(transform, false);
                dash.transform.localScale = new Vector3(0.065f, 0.015f, 0.35f);
                dash.GetComponent<Collider>().enabled = false;
                Destroy(dash.GetComponent<Collider>());
                dash.GetComponent<Renderer>().sharedMaterial = material;
                dashes[index] = dash.transform;
                dash.SetActive(false);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.5f;
            bool show = Available && Vector3.Distance(player.transform.position, arena.Center) > BossArena.HalfSize * 1.5f &&
                NavMesh.CalculatePath(player.transform.position, arena.Entrance, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            int count = show ? path.GetCornersNonAlloc(corners) : 0;
            for (int index = 0; index < dashes.Length; index++)
            {
                Vector3 position = Vector3.zero, forward = Vector3.forward;
                bool active = count > 1 && PointAt(count, 1 + index * 0.95f, out position, out forward);
                dashes[index].gameObject.SetActive(active);
                if (!active) continue;
                dashes[index].position = position + Vector3.up * 0.045f;
                dashes[index].rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up));
            }
        }

        private bool PointAt(int count, float distance, out Vector3 position, out Vector3 forward)
        {
            position = forward = Vector3.zero;
            for (int index = 1; index < count; index++)
            {
                float length = Vector3.Distance(corners[index - 1], corners[index]);
                if (length < 0.001f) continue;
                if (distance <= length)
                {
                    position = Vector3.Lerp(corners[index - 1], corners[index], distance / length);
                    forward = corners[index] - corners[index - 1];
                    return true;
                }
                distance -= length;
            }
            return false;
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
