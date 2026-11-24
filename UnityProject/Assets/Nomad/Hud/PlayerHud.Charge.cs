using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Hud
{
    public sealed partial class PlayerHud
    {
        private RectTransform chargeGauge;
        private RectTransform chargeFill;
        private TMP_Text chargeLabel;
        public bool ChargeGaugeVisible => chargeGauge != null && chargeGauge.gameObject.activeInHierarchy;
        public float ChargeFraction => chargeFill == null ? 0 : chargeFill.anchorMax.x;

        private void BuildChargeGauge()
        {
            chargeGauge = Rect("Charge Gauge", root.transform, new Vector2(250, 30));
            chargeGauge.anchorMin = chargeGauge.anchorMax = new Vector2(0.5f, 0);
            chargeGauge.anchoredPosition = new Vector2(0, 265);
            Surface("Charge Panel", chargeGauge, new Vector2(250, 30));

            RectTransform track = Rect("Charge Track", chargeGauge, new Vector2(180, 16), new Vector2(-25, 0));
            Image background = track.gameObject.AddComponent<Image>();
            background.color = new Color32(43, 49, 57, 255);
            background.raycastTarget = false;

            chargeFill = Rect("Charge Fill", track, Vector2.zero);
            chargeFill.anchorMin = Vector2.zero;
            chargeFill.anchorMax = Vector2.one;
            chargeFill.offsetMin = chargeFill.offsetMax = Vector2.zero;
            Image fill = chargeFill.gameObject.AddComponent<Image>();
            fill.color = Amber;
            fill.raycastTarget = false;

            chargeLabel = Label("Charge Amount", chargeGauge, "", new Vector2(40, 24), new Vector2(100, 0), 14);
            chargeGauge.gameObject.SetActive(false);
        }

        private void UpdateChargeGauge()
        {
            bool visible = combat != null && combat.IsCharging;
            chargeGauge.gameObject.SetActive(visible);
            if (!visible) return;
            float fraction = combat.ChargeProgress;
            chargeFill.anchorMax = new Vector2(fraction, 1);
            bool full = fraction >= 1;
            chargeFill.GetComponent<Image>().color = full ? new Color(1f, 0.92f, 0.45f) : Amber;
            chargeLabel.text = $"{Mathf.RoundToInt(fraction * 100)}%";
            chargeLabel.color = Color.white;
        }
    }
}
