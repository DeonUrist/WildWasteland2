using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace GameStarterBootstrap
{
    public static class PackageInstaller
    {
        static AddRequest request;
        static double deadline;

        public static void Install()
        {
            Debug.Log("[GameStarter] Installing com.unity.test-framework");
            request = Client.Add("com.unity.test-framework");
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                Debug.LogError("[GameStarter] Test Framework resolution timed out.");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success)
            {
                Debug.LogError("[GameStarter] " + request.Error?.message);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[GameStarter] Installed " + request.Result.name + "@" + request.Result.version);
            EditorApplication.Exit(0);
        }
    }
}
