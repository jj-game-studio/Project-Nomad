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
        private readonly List<GameObject> labels = new();
        private readonly Dictionary<Renderer, bool> originals = new();
        private VillageFirstPersonCamera owner;
        private UnityEngine.SceneManagement.Scene region;

        public void Initialize(VillageFirstPersonCamera cameraOwner) => owner = cameraOwner;

        private void LateUpdate()
        {
            bool active = owner != null && owner.IsActive;
            if (!active) { SetVisible(false); return; }
            GameManager game = GameManager.Instance;
            if (game == null) return;

            if (region != game.CurrentRegion)
            {
                SetVisible(false);
                foreach (GameObject label in labels) if (label != null) Destroy(label);
                labels.Clear();
                originals.Clear();
                region = game.CurrentRegion;

                foreach (GameObject root in region.GetRootGameObjects())
                foreach (TextMesh source in root.GetComponentsInChildren<TextMesh>())
                {
                    if (source.name != "Trade sign" && !source.text.Contains("필드")) continue;
                    Renderer renderer = source.GetComponent<Renderer>();
                    originals[renderer] = renderer.enabled;
                    string boardName = source.name == "Field direction" ? "Signboard" :
                        source.name == "Field Entrance Label" ? "Entrance Banner" : null;
                    Transform board = boardName != null ? source.transform.parent.Find(boardName) : null;
                    if (board != null) AttachText(board, source.text);
                }
            }
            SetVisible(true);
        }

        private void AttachText(Transform board, string caption)
        {
            // 글자는 판 앞면에 고정합니다. 카메라를 바라보는 회전이나 화면 좌표 변환은 없습니다.
            var label = new GameObject("Attached Field Sign", typeof(RectTransform), typeof(TextMeshPro));
            label.transform.SetParent(board, false);
            Vector3 scale = board.lossyScale;
            // 판 앞면보다 튀어나온 기둥에도 글자가 가려지지 않도록 3cm 띄웁니다.
            label.transform.localPosition = new Vector3(0, 0, -0.5f - 0.03f / scale.z);
            label.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            var text = label.GetComponent<TextMeshPro>();
            text.font = InteractionHints.LabelFont();
            text.text = caption;
            text.rectTransform.sizeDelta = new Vector2(scale.x * 0.9f, scale.y * 0.82f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1;
            text.fontSizeMax = 100;
            text.color = new Color(1f, 0.91f, 0.65f);
            text.raycastTarget = false;
            labels.Add(label);
        }

        private void SetVisible(bool active)
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.enabled = active ? false : pair.Value;
            foreach (GameObject label in labels) if (label != null) label.SetActive(active);
        }

        private void OnDisable() => SetVisible(false);
        private void OnDestroy()
        {
            SetVisible(false);
            foreach (GameObject label in labels) if (label != null) Destroy(label);
        }
    }
}
