using UnityEngine;
using UnityEngine.Playables;

public class CutsceneTrigger : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector cutsceneDirector;

    public System.Action OnCutsceneEnd; // delegate để bắt sự kiện ngoài

    void Awake()
    {
        if (cutsceneDirector != null)
        {
            // Gắn sự kiện khi cutscene chạy xong
            cutsceneDirector.stopped += OnCutsceneStopped;
        }
    }

    public void PlayCutscene()
    {
        if (cutsceneDirector != null)
        {
            cutsceneDirector.Play();
        }
    }

    private void OnCutsceneStopped(PlayableDirector director)
    {
        // Gọi callback khi cutscene kết thúc
        Debug.Log("🎬 Cutscene finished!");
        OnCutsceneEnd?.Invoke();
    }
}
