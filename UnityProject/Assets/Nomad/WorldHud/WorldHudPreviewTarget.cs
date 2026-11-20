using Nomad.Interaction;
using UnityEngine;

namespace Nomad.WorldHud
{
    public sealed class WorldHudPreviewTarget : MonoBehaviour, IInteractable
    {
        public string InteractionId => gameObject.name;
        public Vector3 InteractionPoint => transform.position;
        public int Interactions { get; private set; }
        public bool CanInteract(InteractionContext context) => isActiveAndEnabled && Interactions == 0;

        public void Interact(InteractionContext context)
        {
            Interactions++;
            GetComponent<Renderer>().material.color = new Color(.2f, .3f, .3f);
        }
    }
}
