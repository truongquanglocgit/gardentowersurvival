using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class MainMenuBGM : MonoBehaviour
{
    [Header("Music")]
    [Tooltip("Nhạc nền cho Main Menu")]
    public AudioClip menuMusic;

    [Range(0f, 1f)]
    public float defaultVolume = 0.6f;

    [Header("UI")]
    [Tooltip("Kéo slider trong Canvas vào đây (optional)")]
    public Slider volumeSlider;

    private AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.loop = true;
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f; // 2D sound
    }

    void Start()
    {
        if (menuMusic)
        {
            _audio.clip = menuMusic;
            _audio.volume = defaultVolume;
            _audio.Play();
        }

        if (volumeSlider)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = defaultVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    public void SetVolume(float value)
    {
        if (_audio) _audio.volume = Mathf.Clamp01(value);
    }
}
