using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nomad.Equipment
{
    public sealed class PlayerWeaponSkill : MonoBehaviour
    {
        private PlayerEquipmentCombat combat;
        private Text label;

        private void Start()
        {
            combat = GetComponent<PlayerEquipmentCombat>();
            var canvas = new GameObject("Weapon skill HUD", typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 25;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var go = new GameObject("Skill status", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvas.transform, false);
            label = go.GetComponent<Text>();
            label.font = Resources.Load<Font>("ShopFonts/Jua-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.color = new Color(1, 0.85f, 0.4f);
            label.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(25, 155);
            rect.sizeDelta = new Vector2(420, 36);
        }

        private void Update()
        {
            if (combat == null) return;
            if (Keyboard.current?.cKey.wasPressedThisFrame == true && Mouse.current != null)
                combat.TryUseSkill(Mouse.current.position.ReadValue());
            string skill = combat.CurrentWeapon?.UnlockedSkill;
            label.text = string.IsNullOrEmpty(skill) ? "" : combat.SkillRemaining > 0
                ? $"C · {skill}  {combat.SkillRemaining:0.0}초" : $"C · {skill}";
        }
    }
}
