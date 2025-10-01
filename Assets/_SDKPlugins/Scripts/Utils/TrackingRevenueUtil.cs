using MoreMountains.Tools;
using static MaxSdkBase;

public class TrackingRevenueUtil : MMPersistentSingleton<TrackingRevenueUtil>
{
    public void LogEventAdImp(string adUnitId, AdInfo adInfo, string countryCode)
    {
        string adFlatform = "Applovin";
        string adSource = adInfo.NetworkName;
        string adUnitName = adInfo.AdUnitIdentifier;
        string currency = SDKConfig.CURRENCY;
        string placementVal = adInfo.Placement == null ? "" : adInfo.Placement;
        string adFormat = adInfo.AdFormat;

        FirebaseUtil.Instance.LogEventAdImp(adUnitId, adFlatform, adSource, adUnitName, currency, adInfo.Revenue, placementVal, countryCode, adFormat);
        AppflyerUtil.Instance.LogEventAdImp(adUnitId, adFlatform, adSource, adUnitName, currency, adInfo.Revenue, placementVal, countryCode, adFormat, adInfo);
    }
}
