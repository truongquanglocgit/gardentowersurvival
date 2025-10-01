using MoreMountains.Tools;

public struct MAXActionEvent
{
    public FirebaseEventMKTEnum EventName;
    public string Placement;
    public static MAXActionEvent e;
    public static void Trigger(FirebaseEventMKTEnum eventName, string placement)
    {
        e.EventName = eventName;
        e.Placement = placement;
        MMEventManager.TriggerEvent(e);
    }
}
