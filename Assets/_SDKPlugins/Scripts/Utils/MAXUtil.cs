using JunEx;
using MoreMountains.Tools;
using System;
using UnityEngine;
using static MaxSdkBase;

public class MAXUtil : MoreMountains.Tools.MMPersistentSingleton<MAXUtil>, MMEventListener<FirebaseInitEvent>, MMEventListener<IAPEvent>
{
    private void Start()
    {
        MMEventManager.AddListener<FirebaseInitEvent>(this);
        MMEventManager.AddListener<IAPEvent>(this);
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause || !FirebaseUtil.Instance.IsFetchComplete) return;
        ShowOpenAd();
    }

    public bool IsRemoveAds { get; private set; }
    public bool IsAOAInOpen { get; set; }
    public bool IsInit = false;
    public bool IsInitSDKEvent = false;
    public bool CanShowAds
    {
        get
        {
            if (IsRemoveAds) return false;
            return true;
        }
    }

    private float _currTimeBetweenShowInter = 0;
    public bool CanShowInterAds
    {
        get
        {
            if (Time.time < _currTimeBetweenShowInter || _timeClickInter >= FirebaseUtil.Instance.MaxClickFull)
                return false;
            _currTimeBetweenShowInter = Time.time + FirebaseUtil.Instance.TimeBetweenShowFull;
            return true;
        }
    }

    public void ResetTimeDelayAOAInter()
    {
        _currTimeBetweenShowInter = Time.time + 30;
    }

    public void ResetTimeRefocusShow()
    {
        _currTimeBetweenRefocus = Time.time + FirebaseUtil.Instance.TimeBetweenRefocusShow;
    }

    public bool CanShowRewardAds
    {
        get
        {
            if (_timeClickReward >= FirebaseUtil.Instance.MaxClickVideo)
                return false;
            return true;
        }
    }

    private float _currTimeBetweenRefocus = -1;
    public bool CanShowRefocus
    {
        get
        {
            if (Time.time < _currTimeBetweenRefocus) return false;
            _currTimeBetweenRefocus = Time.time + FirebaseUtil.Instance.TimeBetweenRefocusShow;
            return true;
        }
    }

    public bool IsFirstTimeLogin
    {
        set => PlayerPrefsExtension.SetBool("IS_FIRST_TIME_LOGIN", value);
        get => PlayerPrefsExtension.GetBool("IS_FIRST_TIME_LOGIN", true);
    }

    #region Init MAX
    private void InitMaxSDK()
    {
        if (IsInit) return;
        IsInit = true;
        MaxSdkCallbacks.OnSdkInitializedEvent += SDKInitEvent;
        MaxSdk.InitializeSdk();
    }

    private void SDKInitEvent(SdkConfiguration sdkConfiguration)
    {
        if (IsInitSDKEvent) return;
        IsInitSDKEvent = true;
        InitAdsEvent();

        LoadOpenAd();
        LoadBannerAd();
        LoadInterAd();
        LoadRewardAd();

        // Show banner in start
        if (FirebaseUtil.Instance.IsBannerInStart)
            ShowBannerAd();
        MAXInitEvent.Trigger();
    }

    private void InitAdsEvent()
    {
        // AppOpen
        InitOpenAdEvent();
        // Banner
        InitBannerAdEvent();
        // Inter
        InitInterAdEvent();
        // Reward
        InitRewardAdEvent();
    }

    private int _retryReward = 0, _timeClickReward = 0;
    private void InitRewardAdEvent()
    {
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
        {
            TrackingRevenueUtil.Instance.LogEventAdImp(adUnitId, adInfo, MaxSdk.GetSdkConfiguration().CountryCode);
        };
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (adUnitId, adInfo) => { _retryReward = 0; };
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (adUnitId, adInfo) => { LoadRewardAd(); };
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
        {
            _retryReward++;
            double retryDelay = Math.Pow(2, Math.Min(6, _retryReward));
            Invoke("LoadRewardAd", (float)retryDelay);
        };
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) => { LoadRewardAd(); };
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (adUnitId, reward, adInfo) =>
        {
            _rewardCallback?.Invoke(adInfo.Placement, adUnitId, adInfo, MaxSdk.GetSdkConfiguration().CountryCode);
            MAXActionEvent.Trigger(FirebaseEventMKTEnum.show_rewarded_ads_sucess, adInfo.Placement);
        };
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent += (adUnitId, adInfo) =>
        {
            _timeClickReward++;
            MAXActionEvent.Trigger(FirebaseEventMKTEnum.show_rewarded_ads_click, adInfo.Placement);
        };
    }

    private int _retryInter = 0, _timeClickInter = 0;
    private void InitInterAdEvent()
    {
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
        {
            TrackingRevenueUtil.Instance.LogEventAdImp(adUnitId, adInfo, MaxSdk.GetSdkConfiguration().CountryCode);
        };
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (adUnitId, adInfo) => { _retryInter = 0; };
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (adUnitId, adInfo) =>
        {
            LoadInterAd();
            _interCallback?.Invoke(adInfo.Placement, adUnitId, adInfo, MaxSdk.GetSdkConfiguration().CountryCode);
            MAXActionEvent.Trigger(FirebaseEventMKTEnum.show_interstitial_ads_suscces, adInfo.Placement);
        };
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
        {
            _retryInter++;
            double retryDelay = Math.Pow(2, Math.Min(6, _retryInter));
            Invoke("LoadInterAd", (float)retryDelay);
        };
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) => { LoadInterAd(); };
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent += (adUnitId, adInfo) =>
        {
            _timeClickInter++;
            MAXActionEvent.Trigger(FirebaseEventMKTEnum.show_interstitial_ads_click, adInfo.Placement);
        };
    }

    private int _timeClickedBanner = 0;
    private void InitBannerAdEvent()
    {
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
        {
            TrackingRevenueUtil.Instance.LogEventAdImp(adUnitId, adInfo, MaxSdk.GetSdkConfiguration().CountryCode);
        };
        MaxSdkCallbacks.Banner.OnAdClickedEvent += (adUnitId, adInfo) =>
        {
            if (_timeClickedBanner >= FirebaseUtil.Instance.MaxClickBanner)
            {
                HideBannerAd();
                return;
            }
            _timeClickedBanner++;
        };
    }

    private void InitOpenAdEvent()
    {
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
        {
            TrackingRevenueUtil.Instance.LogEventAdImp(adUnitId, adInfo, MaxSdk.GetSdkConfiguration().CountryCode);
        };
        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += (adUnitId, adInfo) =>
        {
            // skip show first AOA
                        if (FirebaseUtil.Instance.IsSkipAOAInFirstTimeLogin)
                        {
                            if (IsFirstTimeLogin)
                            {
                                MAXOAFirstEvent.Trigger();
                                IsFirstTimeLogin = false;
                                return;
                            }
                        }
                        IsFirstTimeLogin = false;
            if (!IsAOAInOpen)
            {
                IsAOAInOpen = true;
#if ON_ADS
                ShowOpenAd();
#endif
            }
        };
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += (adUnitId, adInfo) =>
        {
            LoadOpenAd();
            MAXOAFirstEvent.Trigger();
        };
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += (adUnitId, errorInfo) => { LoadOpenAd(); };
        MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) => { LoadOpenAd(); };
    }

    private void LoadOpenAd()
    {
        MaxSdk.LoadAppOpenAd(SDKConfig.AppOpenAdUnitId);
    }

    private void LoadBannerAd()
    {
        MaxSdk.CreateBanner(SDKConfig.BannerAdUnitId, new AdViewConfiguration(SDKConfig.BANNER_POSITION));
        MaxSdk.SetBannerBackgroundColor(SDKConfig.BannerAdUnitId, Color.clear);
        MaxSdk.SetBannerWidth(SDKConfig.BannerAdUnitId, 728f);
    }

    private void LoadInterAd()
    {
        MaxSdk.LoadInterstitial(SDKConfig.InterAdUnitId);
    }

    private void LoadRewardAd()
    {
        MaxSdk.LoadRewardedAd(SDKConfig.RewardAdUnitId);
    }
    #endregion

    #region Show Ads
    public void ShowOpenAd()
    {
        if (!FirebaseUtil.Instance.IsShowAOA || !CanShowRefocus) return;
        ResetTimeDelayAOAInter();
        if (MaxSdk.IsAppOpenAdReady(SDKConfig.AppOpenAdUnitId))
        {
            MaxSdk.ShowAppOpenAd(SDKConfig.AppOpenAdUnitId);
        }
        else
            MaxSdk.LoadAppOpenAd(SDKConfig.AppOpenAdUnitId);
    }

    public void ShowBannerAd()
    {
        if (!CanShowAds || !FirebaseUtil.Instance.IsShowBanner) return;
        MaxSdk.ShowBanner(SDKConfig.BannerAdUnitId);
    }

    public void HideBannerAd()
    {
        MaxSdk.HideBanner(SDKConfig.BannerAdUnitId);
    }

    Action<string, string, AdInfo, string> _interCallback;

    public void ShowInterAd(string placment = null, Action<string, string, AdInfo, string> interCallback = null)
    {
        _interCallback = interCallback;
        MAXActionEvent.Trigger(FirebaseEventMKTEnum.show_interstitial_ads, placment);
        if (!CanShowAds || !CanShowInterAds || !FirebaseUtil.Instance.IsShowInter)
        {
            return;
        }
        if (MaxSdk.IsInterstitialReady(SDKConfig.InterAdUnitId))
        {
            ResetTimeRefocusShow();
            MaxSdk.ShowInterstitial(SDKConfig.InterAdUnitId, placment);
        }
        else
        {
            _interCallback?.Invoke(placment, null, null, null);
        }
    }

/*    public bool CanShowInter
    {
        get
        {
            if (!CanShowAds || !CanShowInterAds || !FirebaseUtil.Instance.IsShowInter) return false;
            return MaxSdk.IsInterstitialReady(SDKConfig.InterAdUnitId);
        }
    }*/

    Action<string, string, AdInfo, string> _rewardCallback;
    /// <summary>
    /// rewardCallback : < placement, adUnit, AdInfo, countryCodes >
    /// </summary>
    /// <param name="placment"></param>
    /// <param name="rewardCallback"></param>
    public void ShowRewardAd(string placment = null, Action<string, string, AdInfo, string> rewardCallback = null)
    {
        _rewardCallback = rewardCallback;
        MAXActionEvent.Trigger(FirebaseEventMKTEnum.show_rewarded_ads, placment);
        if (!CanShowRewardAds)
        {
            _rewardCallback?.Invoke(placment, null, null, null);
            return;
        }

        if (MaxSdk.IsRewardedAdReady(SDKConfig.RewardAdUnitId))
        {
            ResetTimeRefocusShow();
            MaxSdk.ShowRewardedAd(SDKConfig.RewardAdUnitId, placment);
        }
        else
            _rewardCallback?.Invoke(placment, null, null, null);
    }

    public void OnMMEvent(FirebaseInitEvent eventType)
    {
        if (!FirebaseUtil.Instance.IsFetchComplete) return;
        InitMaxSDK();
        Debug.Log("MAXUtil-FBInit");
    }

    public void OnMMEvent(IAPEvent eventType)
    {
        if (!eventType.IsSuccess) return;
        IsRemoveAds = true;
        HideBannerAd();
    }
    #endregion
}
