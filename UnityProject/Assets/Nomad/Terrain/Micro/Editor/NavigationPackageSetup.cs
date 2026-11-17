using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class NavigationPackageSetup
    {
        private static AddRequest request;
        private static double deadline;

        public static void Install()
        {
            request = Client.Add("com.unity.ai.navigation@2.0.16");
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            bool success = request.IsCompleted && request.Status == StatusCode.Success;
            if (success) Debug.Log("NAVIGATION_PACKAGE_PASS: " + request.Result.packageId);
            else Debug.LogError(request.IsCompleted ? request.Error.message : "Navigation package timeout");
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
