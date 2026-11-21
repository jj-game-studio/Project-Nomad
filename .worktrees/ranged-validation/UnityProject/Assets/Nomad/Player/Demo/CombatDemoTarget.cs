using Nomad.Combat;
using UnityEngine;

namespace Nomad.Player
{
    public sealed class CombatDemoTarget : MonoBehaviour
    {
        [SerializeField] private Health health;

        private Renderer targetRenderer;
        private int lastHealth = -1;
        private MaterialPropertyBlock color;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            color = new MaterialPropertyBlock();
            RefreshColor();
        }

        private void Update()
        {
            if (lastHealth != health.CurrentHealth)
                RefreshColor();
        }

        private void RefreshColor()
        {
            lastHealth = health.CurrentHealth;
            float remaining = (float)health.CurrentHealth / health.MaxHealth;
            Color value = Color.Lerp(new Color(0.2f, 0.2f, 0.2f), new Color(0.15f, 0.65f, 0.9f), remaining);
            color.SetColor("_BaseColor", value);
            targetRenderer.SetPropertyBlock(color);
        }
    }
}
