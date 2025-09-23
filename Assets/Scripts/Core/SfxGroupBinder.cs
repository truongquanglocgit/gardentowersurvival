using UnityEngine;
using UnityEngine.Audio;

public class SfxGroupBinder : MonoBehaviour
{
    [Tooltip("Kéo group SFX từ MasterMixer vào đây")]
    public AudioMixerGroup sfxGroup;

    [Tooltip("Gán cho cả AudioSource đang disable trong children")]
    public bool includeInactiveChildren = true;

    void Awake()
    {
        if (sfxGroup == null) return;

        var sources = GetComponentsInChildren<AudioSource>(includeInactiveChildren);
        foreach (var src in sources)
        {
            // Nếu có nhạc riêng trong prefab thì tự loại bỏ theo tag hoặc tên (tuỳ bạn).
            src.outputAudioMixerGroup = sfxGroup;
        }
    }
}
