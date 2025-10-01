using MoreMountains.Tools;

public struct FirebaseInitEvent
{
    public static FirebaseInitEvent e;
    public static void Trigger()
    {
        MMEventManager.TriggerEvent(e);
    }
}
