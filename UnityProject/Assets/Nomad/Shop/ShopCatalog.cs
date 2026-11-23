using System;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Shop
{
    [CreateAssetMenu(menuName = "Nomad/Shop/Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        public enum Category { Materials, Equipment, Supplies }

        [Serializable]
        public sealed class Offer
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField] private Category category;
            [SerializeField] private string displayName;
            [SerializeField, TextArea(1, 3)] private string description;
            [SerializeField, Min(1)] private int buyPrice = 10;
            [SerializeField, Min(0)] private int sellPrice = 5;
            [SerializeField, Min(0)] private int initialStock = 10;
            // 고철처럼 상점이 사들이기만 하는 판매 전용 상품입니다. 구매할 수 없고 판매해도 재고가 늘지 않습니다.
            [SerializeField] private bool sellOnly;

            public ItemDefinition Item => item;
            public Category Group => category;
            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? item.DisplayName : displayName;
            public string Description => description;
            public int BuyPrice => buyPrice;
            public int SellPrice => sellPrice;
            public int InitialStock => initialStock;
            public bool SellOnly => sellOnly;
        }

        [SerializeField] private string merchantName = "마을 상점";
        [SerializeField, Min(0)] private float restockSeconds = 300;
        [SerializeField] private Offer[] offers = Array.Empty<Offer>();
        public string MerchantName => merchantName;
        public float RestockSeconds => restockSeconds;
        public int Count => offers.Length;
        public Offer GetOffer(int index) => offers[index];
    }
}
