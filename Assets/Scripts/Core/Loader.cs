using UnityEngine;
using UnityEngine.SceneManagement;

public static class Loader
{
    public enum Scene
    {
        
        MainMenu,
        Map1,
        Map2,
        Map3,
        LoadingScene      // << scene loading riêng
    }

    static Scene _targetScene;
    static float _requestedAt;

    /// <summary>Gọi từ bất cứ đâu để chuyển scene kèm màn hình loading.</summary>
    public static void Load(Scene target)
    {
        _targetScene = target;
        _requestedAt = Time.unscaledTime;
        SceneManager.LoadScene(Scene.LoadingScene.ToString(), LoadSceneMode.Single);
    }

    /// <summary>Được gọi trong LoadingScreen để thực hiện load scene đích.</summary>
    public static AsyncOperation LoadTargetAsync(out Scene target)
    {
        target = _targetScene;
        var op = SceneManager.LoadSceneAsync(_targetScene.ToString(), LoadSceneMode.Single);
        op.allowSceneActivation = false;
        return op;
    }

    public static float SecondsSinceRequested() => Time.unscaledTime - _requestedAt;
}
