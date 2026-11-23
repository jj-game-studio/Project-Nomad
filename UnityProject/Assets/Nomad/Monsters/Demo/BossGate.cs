using Nomad.Game;
using Nomad.Interaction;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossGate : MonoBehaviour, IInteractable, IInteractionAction, IInteractionRange
    {
        public Vector3 Destination { get; private set; }
        public string InteractionId => "boss-gate-" + GetEntityId().GetHashCode();
        public string InteractionAction => entry && arena != null && arena.IsLocked ? "엘리트 처치 후 입장" : label;
        public Vector3 InteractionPoint => transform.position + Vector3.up;
        public float InteractionRange => 2.5f;

        public static BossGate Create(Vector3 point, Vector3 destination, Quaternion rotation, Transform parent, string label)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(point, rotation);
            var gate = root.AddComponent<BossGate>();
            gate.Destination = destination;
            gate.label = label;
            gate.arena = parent.GetComponent<BossArena>();
            gate.entry = label == "보스 구역 입장";
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.8f;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.4f, 0.43f, 0.36f) };
            for (int index = 0; index < 3; index++)
            {
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = index == 2 ? new Vector3(0, 2.7f, 0) : new Vector3(index == 0 ? -1.25f : 1.25f, 1.3f, 0);
                part.transform.localScale = index == 2 ? new Vector3(3, 0.4f, 0.65f) : new Vector3(0.5f, 2.6f, 0.65f);
                part.GetComponent<Collider>().enabled = false;
                Destroy(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = material;
            }
            gate.material = material;
            return gate;
        }

        private Material material;
        private BossArena arena;
        private bool entry;
        private string label;

        public bool CanInteract(InteractionContext context) => context.Player != null &&
            Vector3.Distance(context.Player.position, transform.position) <= InteractionRange + 0.5f;

        public void Interact(InteractionContext context)
        {
            if (!CanInteract(context) || (entry && arena != null && arena.IsLocked)) return;
            Quaternion facing = transform.rotation;
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.Player == context.Player.gameObject)
                manager.PlacePlayer(Destination + Vector3.up * 0.05f, facing);
            else
            {
                CharacterController controller = context.Player.GetComponent<CharacterController>();
                bool enabled = controller != null && controller.enabled;
                if (enabled) controller.enabled = false;
                context.Player.SetPositionAndRotation(Destination + Vector3.up * 0.05f, facing);
                if (enabled) controller.enabled = true;
                Physics.SyncTransforms();
            }
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
