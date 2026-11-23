using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nomad.Editor
{
    public static class GameViewClarityVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("배치 전용 검사입니다.");
            EditorApplication.delayCall += Verify;
        }

        private static void Verify()
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow view = EditorWindow.GetWindow(type);
                PropertyInfo low = type.GetProperty("lowResolutionForAspectRatios", flags);
                PropertyInfo scale = type.GetProperty("zoomAreaScale", flags);
                MethodInfo zoom = type.GetMethod("SnapZoom", flags);
                if (low == null || scale == null || zoom == null) throw new InvalidOperationException("GameView API 변경");

                low.SetValue(view, true);
                zoom.Invoke(view, new object[] { 1.5f });
                GameViewClarity.Apply();
                Vector2 actual = (Vector2)scale.GetValue(view);
                if ((bool)low.GetValue(view) || Vector2.Distance(actual, Vector2.one) > 0.01f)
                    throw new InvalidOperationException("저해상도 해제/1x 적용 실패: " + actual);

                // 다시 열린 레이아웃과 Play 전환에서도 같은 설정을 적용할 수 있어야 합니다.
                GameViewClarity.Apply();
                Debug.Log("GAME_VIEW_CLARITY_PASS lowResolution=false scale=" + actual);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("GAME_VIEW_CLARITY_FAIL " + exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
