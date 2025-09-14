using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider progressBar;           // Min=0 Max=1
    [SerializeField] private TextMeshProUGUI percentText;  // "0%..100%"
    [SerializeField] private GameObject spinner;           // optional

    [Header("Behavior")]
    [Tooltip("Hiển thị tối thiểu bấy nhiêu giây để tránh 'nháy'")]
    [SerializeField] private float minShowTime = 0.8f;
    [Tooltip("Tốc độ mượt hoá progress UI")]
    [SerializeField] private float uiSmooth = 8f;

    float _uiProgress;

    void Start()
    {
        Time.timeScale = 1f; // đảm bảo không bị pause/slow
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        var op = LoaderBridge.BeginLoadTargetScene();
        if (op == null) yield break;

        float t0 = Time.unscaledTime;

        // Pha 0..0.9 (Unity giữ ~0.9 cho đến khi allowSceneActivation=true)
        while (op.progress < 0.9f)
        {
            float target = Mathf.Clamp01(op.progress / 0.9f);
            _uiProgress = Mathf.MoveTowards(_uiProgress, target, uiSmooth * Time.unscaledDeltaTime);
            UpdateUI(_uiProgress);
            yield return null;
        }

        // Hoàn tất tới 1.0 + chờ minShowTime
        while ((Time.unscaledTime - t0) < minShowTime || _uiProgress < 1f)
        {
            _uiProgress = Mathf.MoveTowards(_uiProgress, 1f, uiSmooth * Time.unscaledDeltaTime);
            UpdateUI(_uiProgress);
            if (spinner) spinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            yield return null;
        }

        op.allowSceneActivation = true; // vào map
    }

    void UpdateUI(float v)
    {
        if (progressBar) progressBar.value = v;
        if (percentText) percentText.text = Mathf.RoundToInt(v * 100f) + "%";
    }
}
