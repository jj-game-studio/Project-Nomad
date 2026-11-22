using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Nomad.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    // 게임 코드는 Assembly-CSharp에 있어 테스트도 asmdef 없이 Assembly-CSharp-Editor에 둡니다.
    internal static class TestKit
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(name, Fields);
            Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{name} 필드가 없습니다.");
            field.SetValue(owner, value);
        }

        public static ItemDefinition Item(string id, int maxStack = 99, EquipmentType type = EquipmentType.None,
            int attack = 0, int defense = 0)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = id;
            SetField(item, "itemId", id);
            SetField(item, "displayName", id);
            SetField(item, "maxStack", maxStack);
            SetField(item, "equipmentType", type);
            SetField(item, "attackPower", attack);
            SetField(item, "defense", defense);
            return item;
        }
    }

    // Awake·OnEnable·isActiveAndEnabled에 의존하는 컴포넌트는 픽스처마다 플레이 모드에 한 번 진입해 검사합니다.
    // 프로젝트는 도메인 리로드를 끈 상태라 진입 비용이 작습니다.
    public abstract class PlayModeFixture
    {
        private readonly List<Object> created = new();

        [UnityOneTimeSetUp]
        public IEnumerator EnterPlay()
        {
            yield return new EnterPlayMode();
        }

        [UnityOneTimeTearDown]
        public IEnumerator ExitPlay()
        {
            yield return new ExitPlayMode();
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object target in created)
            {
                if (target != null)
                    Object.DestroyImmediate(target);
            }

            created.Clear();
        }

        protected T Track<T>(T target) where T : Object
        {
            created.Add(target);
            return target;
        }

        // 직렬화 필드를 채운 뒤 활성화해야 Awake가 설정값을 읽으므로 비활성 상태로 만듭니다.
        protected GameObject Inactive(string name)
        {
            var gameObject = Track(new GameObject(name));
            gameObject.SetActive(false);
            return gameObject;
        }

        protected ItemDefinition Item(string id, int maxStack = 99, EquipmentType type = EquipmentType.None,
            int attack = 0, int defense = 0)
        {
            return Track(TestKit.Item(id, maxStack, type, attack, defense));
        }

        protected static PlayerInventory AddInventory(GameObject owner, params ItemDefinition[] items)
        {
            var inventory = owner.AddComponent<PlayerInventory>();
            TestKit.SetField(inventory, "items", items);
            return inventory;
        }
    }
}
