using MoreMountains.Tools;

public struct MAXInitEvent
{
    public static MAXInitEvent e;
    public static void Trigger()
    {
        MMEventManager.TriggerEvent(e);
    }
}
