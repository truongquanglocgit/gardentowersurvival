using UnityEngine;
using System.Collections.Generic;

public class SFXManager : MonoBehaviour
{
    public static SFXManager I;

    [Header("Pool")]
    [SerializeField] int poolSize = 24;
    [SerializeField] float maxDistance = 30f;

    [Header("Defaults")]
    [SerializeField] bool defaultSpatial = true; // 3D mặc định

    readonly Queue<AudioSource> pool = new();
    Transform root;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        root = new GameObject("[SFXPool]").transform;
        root.SetParent(transform, false);

        for (int i = 0; i < poolSize; i++)
            pool.Enqueue(CreateSource());
    }

    AudioSource CreateSource()
    {
        var go = new GameObject("SFXSrc");
        go.transform.SetParent(root, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = defaultSpatial ? 1f : 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f; // tránh pitch bất thường
        return src;
    }

    AudioSource Get()
    {
        if (pool.Count == 0) pool.Enqueue(CreateSource());
        return pool.Dequeue();
    }

    System.Collections.IEnumerator ReturnAfter(AudioSource src, float secs)
    {
        // dùng realtime để không bị ảnh hưởng Time.timeScale
        float t = 0f;
        while (t < secs) { t += Time.unscaledDeltaTime; yield return null; }
        pool.Enqueue(src);
    }

    public void PlayOneShot(AudioClip clip, Vector3 pos, float volume = 1f, bool spatial = true, float pitch = 1f)
    {
        if (!clip) return;
        var src = Get();
        src.spatialBlend = spatial ? 1f : 0f;
        src.transform.position = pos;
        src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        src.PlayOneShot(clip, Mathf.Clamp01(volume));
        StartCoroutine(ReturnAfter(src, clip.length / Mathf.Abs(src.pitch)));
    }
}
