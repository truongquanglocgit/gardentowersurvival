using UnityEngine;

public static class PauseManager
{
    private static int _pauseLocks = 0;
    private static int _slowLocks = 0;
    private static float _prevScale = 1f;

    public static bool IsPaused => _pauseLocks > 0;
    public static bool IsSlowed => _slowLocks > 0;

    /// <summary>
    /// Pause game hoàn toàn (timeScale = 0)
    /// </summary>
    public static void PushPause()
    {
        if (_pauseLocks++ == 0)
        {
            _prevScale = Time.timeScale;
            Time.timeScale = 0f;
            // AudioListener.pause = true;
        }
    }

    public static void PopPause()
    {
        if (_pauseLocks <= 0) return;
        if (--_pauseLocks == 0)
        {
            Time.timeScale = _prevScale;
            // AudioListener.pause = false;
        }
    }

    /// <summary>
    /// Bật slow motion với hệ số (0 < factor < 1). Ví dụ: factor = 0.5f => chạy chậm 50%
    /// </summary>
    public static void PushSlow(float factor)
    {
        if (_slowLocks++ == 0)
        {
            _prevScale = Time.timeScale;
            Time.timeScale = Mathf.Clamp(factor, 0.01f, 1f);
        }
    }

    /// <summary>
    /// Tắt slow motion, trả lại tốc độ trước đó
    /// </summary>
    public static void PopSlow()
    {
        if (_slowLocks <= 0) return;
        if (--_slowLocks == 0)
        {
            Time.timeScale = _prevScale;
        }
    }
}
