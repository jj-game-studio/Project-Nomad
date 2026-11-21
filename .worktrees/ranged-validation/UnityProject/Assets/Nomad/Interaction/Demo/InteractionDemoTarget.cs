using UnityEngine;

namespace Nomad.Interaction.Demo
{
    public sealed class InteractionDemoTarget : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionId;
        [SerializeField] private Collider detectionCollider;

        public string InteractionId => interactionId;
        public Vector3 InteractionPoint => detectionCollider.bounds.center;

        public bool CanInteract(InteractionContext context) => detectionCollider != null && detectionCollider.enabled;

        public void Interact(InteractionContext context)
        {
            if (context.Inventory.Add("scrap", 1) == 1)
                gameObject.SetActive(false);
        }
    }
}
