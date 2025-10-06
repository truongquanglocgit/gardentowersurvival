using DG.Tweening;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class FirstSceneLoad : MonoBehaviour, MMEventListener<MAXOAFirstEvent>
{
    public static FirstSceneLoad Instance;
    public string SceneName;
    public float Duration = 1f;
    public Image Bar;
    public bool IsChangeScene = false;
    public bool IsReadyChangeScene = false;
    [SerializeField] float _currentTime = 0;
    Tween tween = null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        MMEventManager.AddListener(this);
        Bar.fillAmount = 0;
        tween = DOTween.To(() => Bar.fillAmount, x => Bar.fillAmount = x, .8f, Duration).OnComplete(() =>
        {
            if (!FirebaseUtil.Instance.IsShowAOA)
                LoadFinish();
        }
        );
    }

    private void OnDisable()
    {
        MMEventManager.RemoveListener(this);
    }

    private void Update()
    {
        if (Time.time > Duration + 5f)
            if ((FirebaseUtil.Instance.IsPassMaxTimeLoadAOAInStart))
            {
                if (!IsChangeScene)
                    LoadFinish();
            }
    }

    public void LoadFinish()
    {
        IsChangeScene = true;
        if (tween != null) tween.Kill();
        MAXUtil.Instance.IsAOAInOpen = true;
        DOTween.To(() => Bar.fillAmount, x => Bar.fillAmount = x, 1f, 1f).OnComplete(() =>
        {
            MAXUtil.Instance.ShowBannerAd();
            SceneManager.LoadScene(SceneName);
        });
    }

    public void OnMMEvent(MAXOAFirstEvent eventType)
    {
        LoadFinish();
    }
}
