using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class RewardPanel : MonoBehaviour
{
    [Header("UI")]
    public Slider timerSlider;          // slider đếm ngược về 0
    public float timerDuration = 3f;    // thời gian hiện reward
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public Image rewardImage;           // hình reward
    public TMP_Text rewardText;         // tên/miêu tả + số lượng
    public TMP_Text freeCountsText;     // danh sách “Tower: xN”
    public bool autoCloseOnFinish = true;
    public bool pauseOnOpen = true;

    private Coroutine timerCo;
    private bool _pauseTaken;

    void OnEnable()
    {
        if (pauseOnOpen && !_pauseTaken)
        {
            PauseManager.PushPause();
            _pauseTaken = true;
        }

        if (timerSlider)
        {
            timerSlider.value = 1f; // sẽ lerp về 0
            if (timerCo != null) StopCoroutine(timerCo);
            timerCo = StartCoroutine(TimerRoutine());
        }
    }

    void OnDisable()
    {
        if (timerCo != null)
        {
            StopCoroutine(timerCo);
            timerCo = null;
        }
        if (_pauseTaken)
        {
            PauseManager.PopPause();
            _pauseTaken = false;
        }
    }

    public void ShowReward(Sprite icon, string title, string freeCountsText)
    {
        if (icon && rewardImage) rewardImage.sprite = icon;
        if (rewardText) rewardText.text = title;
        if (this.freeCountsText) this.freeCountsText.text = freeCountsText ?? "";

        gameObject.SetActive(true);
    }

    public void SetFreeCountsText(string txt)
    {
        if (freeCountsText) freeCountsText.text = txt ?? "";
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private IEnumerator TimerRoutine()
    {
        float t = 0f;
        while (t < timerDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / timerDuration);
            float eased = ease != null ? ease.Evaluate(k) : k;
            // slider về 0
            if (timerSlider) timerSlider.value = 1f - eased;
            yield return null;
        }
        if (autoCloseOnFinish) ClosePanel();
        timerCo = null;
    }
}
