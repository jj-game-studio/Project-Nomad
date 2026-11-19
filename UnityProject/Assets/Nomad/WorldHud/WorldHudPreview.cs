using Nomad.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.WorldHud
{
    // 검토 씬에서만 값을 바꿉니다. 구르기 소모나 회복 규칙을 추가하지 않습니다.
    public sealed class WorldHudPreview : MonoBehaviour
    {
        private CharacterStaminaHud stamina;
        private float value = 60;

        private void Start()
        {
            stamina = GetComponent<CharacterStaminaHud>();
            stamina.SetStamina(value, 100);
            Canvas canvas = WorldHudCanvas.Create("World HUD Preview Controls", transform);
            var label = new GameObject("Instructions", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(canvas.transform, false);
            label.font = Resources.Load<TMP_FontAsset>("NomadHudFont");
            label.fontSize = 22;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.text = "WORLD HUD PREVIEW\nWASD move / Shift roll / E interact\nT: stamina -20   R: full   F: insufficient\nPreview data only - no stamina gameplay rules";
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 1);
            label.rectTransform.pivot = new Vector2(0, 1);
            label.rectTransform.anchoredPosition = new Vector2(24, -24);
            label.rectTransform.sizeDelta = new Vector2(760, 160);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tKey.wasPressedThisFrame) value = Mathf.Max(0, value - 20);
            if (keyboard.rKey.wasPressedThisFrame) value = 100;
            if (keyboard.fKey.wasPressedThisFrame) stamina.NotifyInsufficient();
            stamina.SetStamina(value, 100);
        }
    }
}
