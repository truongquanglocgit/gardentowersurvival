using UnityEngine;
using UnityEngine.SceneManagement;

public static class LoaderBridge
{
    /// <summary>Tên scene đích (map) sẽ được LoadingScreen đọc.</summary>
    public static string TargetSceneName { get; private set; }

    /// <summary>Gọi hàm này để chuyển sang Loading scene trước khi vào scene đích.</summary>
    public static void LoadWithLoadingScreen(string targetSceneName, string loadingSceneName = "LoadingScene")
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[LoaderBridge] targetSceneName is null/empty.");
            return;
        }

        TargetSceneName = targetSceneName;
        SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
    }

    /// <summary>Được LoadingScreen gọi để bắt đầu load scene đích (không tự activate).</summary>
    public static AsyncOperation BeginLoadTargetScene()
    {
        if (string.IsNullOrEmpty(TargetSceneName))
        {
            Debug.LogError("[LoaderBridge] No target scene set. Did you call LoadWithLoadingScreen()?");
            return null;
        }

        var op = SceneManager.LoadSceneAsync(TargetSceneName, LoadSceneMode.Single);
        if (op != null) op.allowSceneActivation = false;
        return op;
    }
}
