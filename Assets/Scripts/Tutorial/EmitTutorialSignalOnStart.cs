using UnityEngine;

public class EmitTutorialSignalOnStart : MonoBehaviour
{
    [SerializeField] string key = "MatchFinishedWin";
    [SerializeField] bool oncePerSession = true;
    static System.Collections.Generic.HashSet<string> sent = new();

    void Start()
    {
        if (oncePerSession && sent.Contains(key)) return;
        TutorialManager.EmitSignalGlobal(key);
        if (oncePerSession) sent.Add(key);
    }
}
