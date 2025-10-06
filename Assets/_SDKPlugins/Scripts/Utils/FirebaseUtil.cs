using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using MoreMountains.Tools;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class FirebaseUtil : MMPersistentSingleton<FirebaseUtil>
{
    private async void Start()
    {
        IsFetchComplete = false;
        Dictionary<string, object> defaults = new Dictionary<string, object>();

        // These are the values that are used if we haven't fetched data from the
        // server
        // yet, or if we ask for values that the server doesn't have:
        //defaults.Add(CommonConst.VALUE_SV, CommonConst.URL_SV);
        //defaults.Add(CommonConst.VALUE_LIMIT_SNAP, 10);

        await FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task => { });
        await FetchDataAsync();
    }

    private void OnEnable()
    {
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
    }

    #region Remote Config
    // Start a fetch request.
    // FetchAsync only fetches new data if the current data is older than the provided
    // timespan.  Otherwise it assumes the data is "recent enough", and does nothing.
    // By default the timespan is 12 hours, and for production apps, this is a good
    // number. For this example though, it's set to a timespan of zero, so that
    // changes in the console will always show up immediately.
    public Task FetchDataAsync()
    {
        Debug.Log("Fetching data...");
        Task fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
        return fetchTask.ContinueWithOnMainThread(FetchComplete);
    }

    private void FetchComplete(Task fetchTask)
    {
        if (!fetchTask.IsCompleted)
        {
            Debug.LogError("Retrieval hasn't finished.");
            return;
        }

        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        var info = remoteConfig.Info;
        if (info.LastFetchStatus != LastFetchStatus.Success)
        {
            Debug.LogError($"{nameof(FetchComplete)} was unsuccessful\n{nameof(info.LastFetchStatus)}: {info.LastFetchStatus}");
            return;
        }

        // Fetch successful. Parameter values must be activated to use.
        remoteConfig.ActivateAsync().ContinueWithOnMainThread(task =>
        {
            Debug.Log($"Remote data loaded and ready for use. Last fetch time {info.FetchTime}.");
            IsFetchComplete = true;
            FirebaseInitEvent.Trigger();
        });
    }

    public bool IsFetchComplete { get; set; }

    public FirebaseRemoteConfig RemoteConfig
    {
        get
        {
            return FirebaseRemoteConfig.DefaultInstance;
        }
    }

    public bool IsUseRateNew
    {
        get
        {
            if (IsFetchComplete)
                return RemoteConfig.GetValue(FirebaseRemoteCfgEnum.use_rate_new.ToString()).BooleanValue;
            return false;
        }
    }

    public int LevelShowRate
    {
        get
        {
            if (IsFetchComplete)
                return int.Parse(RemoteConfig.GetValue(FirebaseRemoteCfgEnum.level_show_rate.ToString()).StringValue);
            return -1;
        }
    }

    public AdConfigData AdConfigData
    {
        get
        {
            AdConfigData data = JsonUtility.FromJson<AdConfigData>(RemoteConfig.GetValue(FirebaseRemoteCfgEnum.ad_config.ToString()).StringValue);
            return data;
        }
    }

    public bool IsAdConfigHaveData
    {
        get
        {
            if (IsFetchComplete && AdConfigData != null) return true;
            return false;
        }
    }

    public bool IsShowBanner
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.IsShowBanner;
            return false;
        }
    }

    public bool IsBannerInStart
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.IsBannerInStart;
            return false;
        }
    }

    public bool IsSkipAOAInFirstTimeLogin
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.SkipAOAInFirstTimeLogin;
            return true;
        }
    }

    public bool IsShowAOA
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.IsShowAOA;
            return false;
        }
    }

    public int MaxClickBanner
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.MaxClickBanner;
            return -1;
        }
    }

    public int TimeBetweenShowFull
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.TimeBetweenShowFull;
            return -1;
        }
    }

    public int MaxClickFull
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.MaxClickFull;
            return -1;
        }
    }

    public int MaxClickVideo
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.MaxClickVideo;
            return -1;
        }
    }

    public int MaxTimeLoadAOAInStart
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.MaxTimeLoadAOAInStart;
            return 15;
        }
    }

    public int TimeBetweenRefocusShow
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.TimeBetweenRefocusShow;
            return -1;
        }
    }

    public bool IsRefocusShowAds
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.IsRefocusShowAds;
            return false;
        }
    }
    // true => thời gian chờ OA đầu tiên đã hết, vào gameplay luôn
    public bool IsPassMaxTimeLoadAOAInStart
    {
        get
        {
            if (!MAXUtil.Instance.IsAOAInOpen)
                if (Time.time > MaxTimeLoadAOAInStart)
                {
                    return true;
                }
            return false;
        }
    }

    public int TypeRevenue
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.TypeRevenue;
            return 1;
        }
    }

    public int LevelRequireShowInter
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.LevelRequireShowInter;
            return 3;
        }
    }

    public bool IsShowInter
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.IsShowInter;
            return false;
        }
    }

    public int InitPopupCheckPoint
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.InitPopupCheckPoint;
            return 2;
        }
    }

    public int ShowRating
    {
        get
        {
            if (IsAdConfigHaveData)
                return AdConfigData.ShowRating;
            return 99;
        }
    }

    #endregion

    #region Crashlytics
    private void InitCrashlytics()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                // Create and hold a reference to your FirebaseApp,
                // where app is a Firebase.FirebaseApp property of your application class.
                // Crashlytics will use the DefaultInstance, as well;
                // this ensures that Crashlytics is initialized.
                Firebase.FirebaseApp app = Firebase.FirebaseApp.DefaultInstance;

                // When this property is set to true, Crashlytics will report all
                // uncaught exceptions as fatal events. This is the recommended behavior.
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;

                // Set a flag here for indicating that your project is ready to use Firebase.
            }
            else
            {
                Debug.LogError(System.String.Format(
                  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                // Firebase Unity SDK is not safe to use here.
            }
        });
    }
    #endregion
    #region Log Event MKT Require
    public void LogEventAdImp(string adunitId, string adFlatform, string adSource, string adUnitName,
        string currency,
        double val, string placement, string countryCode, string adFomat)
    {
        Parameter[] parameters =
        {
            new Parameter(FirebaseParamMKTEnum.ad_unit_id.ToString(), adunitId),
            new Parameter(FirebaseParamMKTEnum.ad_platform.ToString(), adFlatform),
            new Parameter(FirebaseParamMKTEnum.ad_source.ToString(), adSource),
            new Parameter(FirebaseParamMKTEnum.ad_unit_name.ToString(), adUnitName),
            new Parameter(FirebaseParamMKTEnum.currency.ToString(), currency),
            new Parameter(FirebaseParamMKTEnum.value.ToString(), val),
            new Parameter(FirebaseParamMKTEnum.placement.ToString(), placement),
            new Parameter(FirebaseParamMKTEnum.country_code.ToString(), countryCode),
            new Parameter(FirebaseParamMKTEnum.ad_format.ToString(), adFomat)
        };
        FirebaseAnalytics.LogEvent(FirebaseEventMKTEnum.ad_impression.ToString(), parameters);

    }

    /// <summary>
    /// event of mkt
    /// level_start, level_passed, level_failed
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="level"></param>
    /// <param name="timePlay"></param>
    /// 
    private readonly Parameter[] parametersLevel = new Parameter[3];
    public void LogEventLevel(FirebaseEventMKTEnum eventName,int countPlay, int level, string timePlay, int newLevel = 0)
    {
        //Event firebase send must have type "string"
        string level_Param = "";
        string level_count_play_Param = "";
        string time_played_Param = "";

        switch(eventName)
        {
            case FirebaseEventMKTEnum.level_passed:
                level_Param = $"passlevel_{level}";
                level_count_play_Param = $"countplay_{countPlay}_passlevel_{level}_newlevel_{newLevel}";
                time_played_Param = $"countplay_{countPlay}_passlevel_{level}_newlevel_{newLevel}_with_{timePlay}";
                break;
            case FirebaseEventMKTEnum.level_failed:
                level_Param = "faillevel" + "_" + level;
                level_count_play_Param = "countplay_" + countPlay + "_faillevel_" + level;
                time_played_Param = "countplay_" + countPlay + "_faillevel_" + level + "_at_" + timePlay;
                break;
            case FirebaseEventMKTEnum.level_start:
                level_Param = $"startlevel_{level}";
                level_count_play_Param = $"countplay_{countPlay}_startlevel_{level}";
                time_played_Param = $"countplay_{countPlay}_startlevel_{level}_at_{timePlay}";
                break;
            case FirebaseEventMKTEnum.level_start_guess:
                level_Param = $"startlevel_{level}";
                level_count_play_Param = $"countplay_{countPlay}_startlevel_{level}";
                time_played_Param = $"countplay_{countPlay}_startlevel_{level}_at_{timePlay}";
                break;
        }
        parametersLevel[0] = new Parameter(nameof(FirebaseParamMKTEnum.level), level_Param);
        parametersLevel[1] = new Parameter(nameof(MyFirebaseParamEnum.level_count_play), level_count_play_Param);
        parametersLevel[2] = new Parameter(nameof(FirebaseParamMKTEnum.time_played), time_played_Param);

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parametersLevel);
#endif
#if UNITY_EDITOR
        Debug.Log(eventName.ToString()+ " - " + "level: "+ level_Param);
        //Debug.Log(eventName.ToString() % JunEx.Colorize.Gold + " - " + "level_count_play: " % JunEx.Colorize.Green + level_count_play_Param);
        //Debug.Log(eventName.ToString() % JunEx.Colorize.Gold + " - " + "time_played: " % JunEx.Colorize.Green + time_played_Param);
        Debug.Log("--------------------------------------");
#endif
    }

    public void LogMyEvent(FirebaseEventMKTEnum eventName)
    {
        Parameter[] parameters =
        {
            new Parameter("",""),
        };
#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parameters);
#endif
    }

    public void LogEventWatchAds(FirebaseEventMKTEnum eventName)
    {
        string param = $"watch ads";

        Parameter[] parameters =
        {
            new Parameter(FirebaseParamMKTEnum.watchads.ToString(), param),
        };

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parameters);
#endif
#if UNITY_EDITOR
        Debug.Log(eventName.ToString() + " - " + "skin: " + param);
        Debug.Log("--------------------------------------");
#endif
    }
    public void LogEventEndWaveAds(FirebaseEventMKTEnum eventName)
    {
        string param = $" end wave reward ";

        Parameter[] parameters =
        {
            new Parameter(FirebaseParamMKTEnum.watchads.ToString(), param),
        };

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parameters);
#endif
#if UNITY_EDITOR
        Debug.Log(eventName.ToString() + " - "  + param);
        Debug.Log("--------------------------------------");
#endif
    }
    public void LogEventChooseMap(FirebaseEventMKTEnum eventName, string nameMap)
    {
        Parameter[] parameters =
        {
            new Parameter(FirebaseParamMKTEnum.choose_map.ToString(), nameMap),
        };

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parameters);
#endif
    }

    public void LogEventUseSkin(FirebaseEventMKTEnum eventName, int id)
    {
        string param = $"use_{id}";

        Parameter[] parameters =
        {
            new Parameter(FirebaseParamMKTEnum.use.ToString(), param),
        };

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parameters);
#endif
#if UNITY_EDITOR
        Debug.Log(eventName.ToString()+ " - " + "skin: "+ param);
        Debug.Log("--------------------------------------");
#endif
    }

    public void LogEventUnlockSkin(FirebaseEventMKTEnum eventName, int id)
    {
        string param = $"unlock_{id}";

        Parameter[] parameters =
        {
            new Parameter(FirebaseParamMKTEnum.unlock.ToString(), param),
        };

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parameters);
#endif
#if UNITY_EDITOR
        Debug.Log(eventName.ToString() + " - " + "skin: "+ param);
        Debug.Log("--------------------------------------");
#endif
    }

    private readonly Parameter[] parametersAdAction = new Parameter[2];
    public void LogEventAdAction(FirebaseEventMKTEnum eventName, string placement)
    {
        string placement_param = placement;
        string placement_lv_param = $"{placement}_lv{0}"; // 0 : check point 0

        parametersAdAction[0] = new Parameter(nameof(FirebaseParamMKTEnum.placement), placement_param);
        parametersAdAction[1] = new Parameter(nameof(FirebaseParamMKTEnum.placement_lv), placement_lv_param);

#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(eventName.ToString(), parametersAdAction);
#endif
#if UNITY_EDITOR
        Debug.Log(eventName.ToString() + "-" + "placement : " + placement);
        Debug.Log(eventName.ToString() + "-" + "placement_lv : " + placement_lv_param);

        Debug.Log(eventName.ToString() + "-" + "placement : " + placement);
        Debug.Log(eventName.ToString() + "-" + "placement_lv : " + placement_lv_param);
        Debug.Log("--------------------------------------");
#endif
    }
    #endregion
}
