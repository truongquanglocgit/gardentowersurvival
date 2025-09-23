using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tutorial/Sequence", fileName = "TutorialSequence")]
public class TutorialSequenceSO : ScriptableObject
{
    public TutorialStepSO[] steps;
}
