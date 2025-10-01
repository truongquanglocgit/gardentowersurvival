using System;

[Serializable]
public class AdConfigData
{
    public bool IsShowBanner; //x
    public bool IsBannerInStart; //x
    public int MaxClickBanner; //x
    public int TimeBetweenShowFull; //x
    public int MaxClickFull; //x
    public int MaxClickVideo; //x
    public bool IsShowAOA; //x
    public int MaxTimeLoadAOAInStart; //x
    public int TimeBetweenRefocusShow; //x
    public bool IsRefocusShowAds;
    public bool IsShowInter;
    public bool SkipAOAInFirstTimeLogin; //x
    public int TypeRevenue;
    public int LevelRequireShowInter;
    public int InitPopupCheckPoint;
    public int DistancePopupCheckPoint;
    public int ShowRating;
    public int QuantityAI;
    public float TimeToClosePopupTele;
    public float TimeToShowPopupJetpack;
    public bool IsMapConfig;
    public bool BossConfig;
    public bool ABTest = false;
    public bool Test2;
    public bool IsUIHack;
    public int NumNoAds;
    public bool TestHeart;

    public bool TestIsUnLockSkin;

    public bool TestIslimitSkin;

    public bool IsOpenLevel0;

    public int MelonShow = 99;
    public int SchoolShow = 99;
    public int SquidShow = 99;
    public int FamilyShow = 99;

    public string ListBtn = "1,2,3,4,5,6";

    public float SpeedSpawn;

    public bool HideSkin;

    public bool HideHeart;
}
