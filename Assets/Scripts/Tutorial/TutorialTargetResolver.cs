using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

    public static class TutorialTargetResolver
    {
        /// <summary>
        /// Tìm RectTransform theo nhiều chế độ. Trả về null nếu không thấy trong thời gian cho phép.
        /// </summary>
        public static IEnumerator ResolveTargetRectCoroutine(
            Canvas canvas, TutorialStepSO step, System.Action<RectTransform> onDone)
        {
            RectTransform result = null;
            float t = 0f;

            if (step.resolveMode == ResolveMode.None || step.mode == StepMode.TapAnywhere)
            {
                onDone?.Invoke(null);
                yield break;
            }

            // 1) ByRef: có ngay
            if (step.resolveMode == ResolveMode.ByRef && step.uiTargetRef != null)
            {
                onDone?.Invoke(step.uiTargetRef);
                yield break;
            }

            // 2) WaitForTowerEvent: đợi tín hiệu from PlacementSystem (tối ưu)
            if (step.resolveMode == ResolveMode.WaitForTowerEvent)
            {
                bool gotIt = false;
                void Handler(MonoBehaviour mb)
                {
                    // nhận TowerController hoặc root; cố gắng tìm nút UI hoặc chính mb RectTransform
                    var rt = (mb as Component)?.GetComponentInChildren<RectTransform>();
                    if (rt != null && rt.gameObject.activeInHierarchy)
                    {
                        result = rt;
                        gotIt = true;
                    }
                }

                TutorialSignals.TowerPlaced += Handler;
                // chờ trong khoảng timeout
                while (!gotIt && t < step.findTimeout)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                TutorialSignals.TowerPlaced -= Handler;

                onDone?.Invoke(result);
                yield break;
            }

            // Helper: scan toàn bộ RectTransform trong scene (kể cả inactive)
            RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>()
                .Where(r => r.hideFlags == HideFlags.None).ToArray();

            // 3) ByName / RuntimeFindByName
            if (step.resolveMode == ResolveMode.ByName || step.resolveMode == ResolveMode.RuntimeFindByName)
            {
                while (result == null && t < step.findTimeout)
                {
                    result = allRects.FirstOrDefault(r => r.name.Equals(step.targetName));
                    if (result != null && result.gameObject.activeInHierarchy) break;
                    t += Time.unscaledDeltaTime;
                    yield return null;
                    allRects = Resources.FindObjectsOfTypeAll<RectTransform>()
                        .Where(r => r.hideFlags == HideFlags.None).ToArray();
                }
                onDone?.Invoke(result);
                yield break;
            }

            // 4) ByTag / RuntimeFindByTag (ưu tiên UI, nếu không có thử WorldObject có RectTransform)
            if (step.resolveMode == ResolveMode.ByTag || step.resolveMode == ResolveMode.RuntimeFindByTag)
            {
                while (result == null && t < step.findTimeout)
                {
                    // UI theo tag
                    result = allRects.FirstOrDefault(r => r.CompareTag(step.targetTag));
                    if (result != null && result.gameObject.activeInHierarchy) break;

                    // Thử world object có RectTransform (UI in world space)
                    var go = GameObject.FindGameObjectWithTag(step.targetTag);
                    if (go != null)
                    {
                        result = go.GetComponentInChildren<RectTransform>();
                        if (result != null && result.gameObject.activeInHierarchy) break;
                    }

                    t += Time.unscaledDeltaTime;
                    yield return null;
                    allRects = Resources.FindObjectsOfTypeAll<RectTransform>()
                        .Where(r => r.hideFlags == HideFlags.None).ToArray();
                }
                onDone?.Invoke(result);
                yield break;
            }

            // Mặc định: không tìm thấy
            onDone?.Invoke(null);
        }

        /// <summary>
        /// Nếu target là container, tìm Button con theo tên (không phân biệt hoa thường một phần).
        /// </summary>
        public static Button FindChildButtonByName(RectTransform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName)) return null;
            var buttons = parent.GetComponentsInChildren<Button>(true);
            return buttons.FirstOrDefault(b => b.name.Equals(childName) ||
                                               b.name.ToLower().Contains(childName.ToLower()));
        }
    }

