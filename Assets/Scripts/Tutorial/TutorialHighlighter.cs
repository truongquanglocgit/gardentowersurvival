using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Outline))]
public class TutorialHighlighter : MonoBehaviour
{
    [SerializeField] private Outline outline;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float minAlpha = 0.2f;
    [SerializeField] private float maxAlpha = 0.9f;

    private void Reset()
    {
        outline = GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.8f);
        outline.effectDistance = new Vector2(4f, -4f);
    }

    private void Update()
    {
        if (!outline) return;
        var col = outline.effectColor;
        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f; // 0..1
        col.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        outline.effectColor = col;
    }
}
