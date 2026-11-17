using System;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Shop
{
    [CreateAssetMenu(menuName = "Nomad/Shop/Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Offer
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField, Min(1)] private int buyPrice = 10;
            [SerializeField, Min(0)] private int sellPrice = 5;
            [SerializeField, Min(0)] private int initialStock = 10;

            public ItemDefinition Item => item;
            public int BuyPrice => buyPrice;
            public int SellPrice => sellPrice;
            public int InitialStock => initialStock;
        }

        [SerializeField] private Offer[] offers = Array.Empty<Offer>();
        public int Count => offers.Length;
        public Offer GetOffer(int index) => offers[index];
    }
}
