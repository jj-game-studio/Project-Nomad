using UnityEngine;

namespace Nomad.Inventory
{
    [CreateAssetMenu(menuName = "Nomad/Inventory/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStack = 99;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int MaxStack => maxStack;
    }
}
