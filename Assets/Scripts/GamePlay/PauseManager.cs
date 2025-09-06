using UnityEngine;

public static class PauseManager
{
    private static int _locks = 0;
    private static float _prevScale = 1f;

    public static bool IsPaused => _locks > 0;

    public static void PushPause()
    {
        if (_locks++ == 0)
        {
            _prevScale = Time.timeScale;
            Time.timeScale = 0f;          // dừng game
            // AudioListener.pause = true; // nếu muốn dừng cả âm thanh
        }
    }

    public static void PopPause()
    {
        if (_locks <= 0) return;
        if (--_locks == 0)
        {
            Time.timeScale = _prevScale;  // trả lại tốc độ trước đó
            // AudioListener.pause = false;
        }
    }
}
