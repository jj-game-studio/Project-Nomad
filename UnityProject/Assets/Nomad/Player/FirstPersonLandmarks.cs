using System.Collections.Generic;
using Nomad.Game;
using Nomad.WorldHud;
using TMPro;
using UnityEngine;

namespace Nomad.Player
{
    [DefaultExecutionOrder(400)]
    public sealed class FirstPersonLandmarks : MonoBehaviour
    {
        private sealed class Label
        {
            public TextMesh Source;
            public TMP_Text Text;
        }
        private readonly List<Label> labels = new();
        private readonly Dictionary<Renderer, bool> originals = new();
        private VillageFirstPersonCamera owner;
        private Canvas canvas;
        private int region;
        public void Initialize(VillageFirstPersonCamera cameraOwner) => owner = cameraOwner;

        private void LateUpdate()
        {
            if (owner == null || !owner.IsActive)
            {
                if (canvas != null) canvas.gameObject.SetActive(false);
                Restore();
                return;
            }
            GameManager game = GameManager.Instance;
            if (game == null) return;
            if (canvas == null) canvas = WorldHudCanvas.Create("First Person Landmarks", transform);
            if (region != game.CurrentRegion.handle)
            {
                Restore();
                foreach (Label label in labels) if (label.Text != null) Destroy(label.Text.gameObject);
                labels.Clear();
                region = game.CurrentRegion.handle;
                foreach (GameObject root in game.CurrentRegion.GetRootGameObjects())
                foreach (TextMesh source in root.GetComponentsInChildren<TextMesh>())
                {
                    if (source.name != "Trade sign" && !source.text.Contains("필드")) continue;
                    Renderer renderer = source.GetComponent<Renderer>();
                    originals[renderer] = renderer.enabled;
                    if (source.name == "Trade sign") continue;
                    var rect = (RectTransform)new GameObject("Field Entrance Hint", typeof(RectTransform)).transform;
                    rect.SetParent(canvas.transform, false);
                    rect.sizeDelta = new Vector2(220, 40);
                    TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
                    text.font = InteractionHints.LabelFont();
                    text.text = "필드 출입구";
                    text.fontSize = 23;
                    text.alignment = TextAlignmentOptions.Center;
                    text.color = new Color(1f, 0.9f, 0.65f);
                    text.outlineWidth = 0.2f;
                    text.outlineColor = Color.black;
                    text.raycastTarget = false;
                    labels.Add(new Label { Source = source, Text = text });
                }
            }
            canvas.gameObject.SetActive(true);
            foreach (var pair in originals) if (pair.Key != null) pair.Key.enabled = false;
            foreach (Label label in labels)
            {
                bool visible = label.Source != null && Vector3.Distance(transform.position, label.Source.transform.position) < 14f &&
                    WorldHudCanvas.Place(canvas, label.Text.rectTransform, GetComponent<Camera>(),
                        label.Source.transform.position + Vector3.up * 0.3f, Vector2.zero);
                label.Text.gameObject.SetActive(visible);
            }
        }

        private void Restore()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.enabled = pair.Value;
        }
        private void OnDisable() { Restore(); if (canvas != null) canvas.gameObject.SetActive(false); }
        private void OnDestroy() { Restore(); if (canvas != null) Destroy(canvas.gameObject); }
    }
}
