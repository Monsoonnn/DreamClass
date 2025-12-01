using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGuideData", menuName = "Guide System/Guide Data", order = 0)]
public class GuideData : ScriptableObject {
    [Header("Guide Info")]
    public string guideID;

    [Header("All Steps in Sequence")]
    public List<StepData> steps = new List<StepData>();

    [Header("Summary")]

    [TextArea(2, 5)] public string summary;

}
