using MoreMountains.Tools;

public struct IAPEvent
{
    public string ProductID;
    public bool IsSuccess;

    public static IAPEvent e;
    public static void Trigger(string productID, bool isSuccess)
    {
        e.ProductID = productID;
        e.IsSuccess = isSuccess;
        MMEventManager.TriggerEvent(e);
    }
}
