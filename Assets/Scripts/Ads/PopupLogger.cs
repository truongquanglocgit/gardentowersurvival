using UnityEngine;
using System.Collections.Generic;

public class PopupLogger : MonoBehaviour
{
    private static Queue<string> logQueue = new Queue<string>();
    private static bool showPopup = false;
    private static string currentMessage = "";
    private const int MaxLogCount = 20; // tránh tràn bộ nhớ

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string msg = $"[{type}] {logString}";
        if (type == LogType.Exception || type == LogType.Error)
            msg += $"\n{stackTrace}";

        logQueue.Enqueue(msg);
        if (logQueue.Count > MaxLogCount) logQueue.Dequeue();

        currentMessage = msg;
        showPopup = true;
    }

    private void OnGUI()
    {
        if (!showPopup) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 24,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.red }
        };

        // Khung popup giữa màn hình
        Rect rect = new Rect(20, 20, Screen.width - 40, Screen.height / 3);
        GUI.Box(rect, currentMessage, boxStyle);

        if (GUI.Button(new Rect(rect.x + rect.width - 120, rect.y + rect.height - 60, 100, 40), "OK"))
        {
            showPopup = false;
        }
    }

    // Optional: Hiển thị toàn bộ log trong danh sách
    public static string GetAllLogs()
    {
        return string.Join("\n\n", logQueue.ToArray());
    }
}
