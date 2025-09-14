using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Clip sẽ phát khi ấn nút. Bỏ trống thì không phát.")]
    public AudioClip clickSound;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Nguồn phát âm. Nếu để trống sẽ auto tạo AudioSource 2D.")]
    public AudioSource audioSource;

    void Awake()
    {
        // Gắn event click
        GetComponent<Button>().onClick.AddListener(PlayClickSound);

        // Nếu chưa có audioSource -> tự tạo 2D AudioSource
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }
    }

    void PlayClickSound()
    {
        if (clickSound && audioSource)
        {
            audioSource.PlayOneShot(clickSound, volume);
        }
    }
}
