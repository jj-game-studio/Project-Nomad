using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nomad.Editor
{
    [InitializeOnLoad]
    public static class GameViewClarity
    {
        private static readonly Type ViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static GameViewClarity()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall += Apply;
            };
        }

        [MenuItem("Nomad/Display/Game View Native Resolution (1x)")]
        public static void Apply()
        {
            if (ViewType == null) return;
            foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(ViewType))
            {
                try
                {
                    // Unity의 저해상도 화면비 옵션은 HiDPI 배율만큼 확대해 글자까지 픽셀화합니다.
                    ViewType.GetProperty("lowResolutionForAspectRatios", Flags)?.SetValue(view, false);
                    ViewType.GetMethod("SnapZoom", Flags)?.Invoke(view, new object[] { 1f });
                    ((EditorWindow)view).Repaint();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Game 뷰 고해상도 설정을 적용하지 못했습니다: " + exception.GetBaseException().Message);
                }
            }
        }
    }
}
