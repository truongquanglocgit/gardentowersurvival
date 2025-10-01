using MoreMountains.Tools;

public class MAXActionListener : MMPersistentSingleton<MAXActionListener>, MMEventListener<MAXActionEvent>
{
    private void Start()
    {
        MMEventManager.AddListener(this);
    }

    public void OnMMEvent(MAXActionEvent eventType)
    {
        LogActionEvent(eventType);
    }

    private void LogActionEvent(MAXActionEvent actionEvent)
    {
        FirebaseUtil.Instance.LogEventAdAction(actionEvent.EventName, actionEvent.Placement);
    }
}
