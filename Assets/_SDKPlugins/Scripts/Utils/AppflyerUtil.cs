#if USE_APPSFLYER
using AppsFlyerSDK;
#endif
using MoreMountains.Tools;
using System.Collections.Generic;
using UnityEngine;
using static MaxSdkBase;

public class AppflyerUtil : MMPersistentSingleton<AppflyerUtil>, MMEventListener<MAXInitEvent>
{

    private void OnEnable()
    {
#if USE_APPSFLYER
        MMEventManager.AddListener(this);
#endif
    }

    void OnDisable()
    {
#if USE_APPSFLYER
        MMEventManager.RemoveListener(this);
#endif
    }

    public void LogEventAdImp(string adUnitId, string adFlatform, string adSource, string adUnitName,
        string currency,
        double val, string placement, string countryCode, string adFomat, MaxSdk.AdInfo adInfo)
    {
#if USE_APPSFLYER
        Dictionary<string, string> additionalParams = new Dictionary<string, string>()
        {
            { AdRevenueScheme.COUNTRY, countryCode },
            { FirebaseParamMKTEnum.ad_unit_id.ToString(), adUnitId },
            { FirebaseParamMKTEnum.ad_platform.ToString(), adFlatform },
            { FirebaseParamMKTEnum.ad_source.ToString(), adSource },
            { FirebaseParamMKTEnum.ad_unit_name.ToString(), adUnitName },
            { AFInAppEvents.CONTENT_TYPE, placement },
            { FirebaseParamMKTEnum.country_code.ToString(), countryCode },
            { FirebaseParamMKTEnum.ad_format.ToString(), adFomat }
        };

        switch (FirebaseUtil.Instance.TypeRevenue)
        {
            //case 3:
            //    var logRevenue = new AFAdRevenueData(adSource, MediationNetwork.ApplovinMax, currency, val);
            //    AppsFlyer.logAdRevenue(logRevenue, additionalParams);
            //    break;
            case 0:
                AppsFlyer.sendEvent(AppsFlyerEventMKTEnum.af_ad_revenue.ToString(), additionalParams);
                break;
            case 1:
                AppsFlyerAdRevenue.logAdRevenue(adInfo.NetworkName, AppsFlyerAdRevenueMediationNetworkType.AppsFlyerAdRevenueMediationNetworkTypeApplovinMax,
                    adInfo.Revenue, "USD",
                    additionalParams);
                break;
        }
#endif
    }

    public void OnMMEvent(MAXInitEvent eventType)
    {
#if USE_APPSFLYER
        AppsFlyer.initSDK(SDKConfig.APPFLYER_DEVKEY, SDKConfig.APPFLYER_APPID);
        AppsFlyer.startSDK();
        AppsFlyerAdRevenue.start();
        Debug.Log("AppflyerUtil-FBInit");
#endif
    }
}
