// TutorialRouter.cs
using UnityEngine;

public static class TutorialRouter
{
    public static TutorialSequenceSO Queued;   // sequence chờ phát sau scene load

    public static void Queue(TutorialSequenceSO seq)
    {
        Queued = seq;
    }

    public static TutorialSequenceSO Consume()
    {
        var s = Queued;
        Queued = null;
        return s;
    }
}
