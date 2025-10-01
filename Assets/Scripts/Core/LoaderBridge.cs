using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LoaderBridge
{
    // 🔒 cho TutorialManager chờ
    public static bool IsLoading { get; private set; }

    // optional: nếu bạn muốn UI khác nghe theo
    public static event Action<bool> LoadingStateChanged;

    /// <summary>Tên scene đích (map) sẽ được LoadingScreen đọc.</summary>
    public static string TargetSceneName { get; private set; }

    static bool _hooked;

    public static void LoadWithLoadingScreen(string targetSceneName, string loadingSceneName = "LoadingScene")
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[LoaderBridge] targetSceneName is null/empty.");
            return;
        }

        TargetSceneName = targetSceneName;

        // 🔒 bật cờ đang load
        IsLoading = true;
        LoadingStateChanged?.Invoke(true);

        // hook 1 lần: khi scene đích load xong → thả cờ
        if (!_hooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _hooked = true;
        }

        SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);

    }

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

    // ✅ Unity gọi sau khi scene đích đã được load+activate
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrEmpty(TargetSceneName) && scene.name == TargetSceneName)
        {
            // 🔓 thả cờ, thông báo
            IsLoading = false;
            LoadingStateChanged?.Invoke(false);

            // reset
            TargetSceneName = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _hooked = false;
            
        }
    }
}
