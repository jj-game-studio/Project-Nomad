using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Monsters.Demo
{
    // 현재 머리 자세를 한 번 촬영하고 촬영용 레이어는 같은 호출 안에서 즉시 복원합니다.
    public sealed class BossPortrait : MonoBehaviour
    {
        private RenderTexture texture;
        private GameObject studio;

        public void Capture(DragonBossCombat boss)
        {
            studio = new GameObject("Boss portrait camera");
            var view = studio.AddComponent<Camera>();
            Vector3 head = boss.Mouth.position;
            Vector3 front = Vector3.ProjectOnPlane(boss.Mouth.position-boss.transform.position,Vector3.up).normalized;
            if(front.sqrMagnitude<0.1f)front=boss.transform.forward;
            Vector3 side=Vector3.Cross(Vector3.up,front);
            view.transform.position = head + front * 6 + side * 3 + Vector3.up * 1.5f;
            view.transform.LookAt(head + front * 0.2f);
            view.orthographic = true; view.orthographicSize = 2.2f;
            view.nearClipPlane = 0.1f; view.farClipPlane = 12;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(0.06f,0.035f,0.025f);
            view.cullingMask = 1 << 31;
            texture = new RenderTexture(256,256,16); view.targetTexture = texture;
            var sources = new List<Renderer>();
            var layers = new List<int>();
            var hidden = new List<bool>();
            var enabled = new List<bool>();
            foreach (var source in boss.transform.Find("Body").GetComponentsInChildren<Renderer>())
            {
                if (source is not SkinnedMeshRenderer && source is not MeshRenderer) continue;
                if (source.name.Contains("outline") || source.name.Contains("carapace")) continue;
                sources.Add(source); layers.Add(source.gameObject.layer);
                hidden.Add(source.forceRenderingOff); enabled.Add(source.enabled);
            }
            try
            {
                foreach (var source in sources)
                {
                    source.gameObject.layer = 31;
                    source.forceRenderingOff = false;
                    source.enabled = true;
                }
                view.Render();
            }
            finally
            {
                for (int i=0;i<sources.Count;i++)
                {
                    sources[i].gameObject.layer = layers[i];
                    sources[i].forceRenderingOff = hidden[i];
                    sources[i].enabled = enabled[i];
                }
                view.enabled = false;
            }
            gameObject.AddComponent<RawImage>().texture = texture;
            GetComponent<RawImage>().raycastTarget = false;
            studio.SetActive(false);
        }

        private void OnDestroy()
        {
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (studio != null) Destroy(studio);
        }
    }
}
