using UnityEngine;

public static class GameResetter
{
    public static void ResetBeforeExitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // ⭐ reset toàn bộ runtime static
        WaveRuntime.ResetAll();
        WaveManager.ResetEvents();

        // Pool sống qua scene thì clear, còn nếu PoolManager nằm trong scene thì không cần
        PoolManager.I?.ClearAll();

        // Hủy các singleton DontDestroyOnLoad không cần ở MainMenu (nếu có)
        // DestroyIfExists(GameSession.Instance?.gameObject);
        // DestroyIfExists(PlayerDataManager.Instance?.gameObject);
    }
}
