using System.Collections;
using UnityEngine;
using UnityEngine.UI;


    /// <summary>
    /// Tạo 1 Image highlight (halo) trên tutorialCanvas, canh theo RectTarget.
    /// - Put this component on a GameObject under TutorialCanvas (same canvas used by TutorialManager).
    /// - Provide a halo sprite (soft round) or rounded rect.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HighlightOverlay : MonoBehaviour
    {
        [Header("Refs")]
        public Canvas targetCanvas;      // set to tutorial canvas in inspector
        public Image haloImage;          // image to show (sprite should be soft round/oval)
        public bool matchTargetSize = true;

        [Header("Animation")]
        public float pulseSpeed = 2f;
        public float minAlpha = 0.25f;
        public float maxAlpha = 0.9f;
        public float sizePaddingX = 16f;
        public float sizePaddingY = 12f;

        RectTransform _rt;
        Coroutine _pulseCoroutine;

        void Awake()
        {
            _rt = transform as RectTransform;
            if (haloImage != null) haloImage.raycastTarget = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            // nothing here; animation via coroutine
        }

        /// <summary>
        /// Show highlight matching the given target RectTransform (screen-space).
        /// </summary>
        public void ShowFor(RectTransform target, Vector2 padding)
        {
            if (target == null || targetCanvas == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            // Convert target's rect center to canvas local pos
            Camera cam = targetCanvas.worldCamera; // null for ScreenSpace-Overlay
            Vector3 worldCenter = target.TransformPoint(target.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
            RectTransform canvasRect = targetCanvas.transform as RectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, cam, out Vector2 localPoint);

            _rt.anchoredPosition = localPoint;

            if (matchTargetSize)
            {
                // determine pixel size of target on screen -> convert to canvas local scale
                Vector3[] corners = new Vector3[4];
                target.GetWorldCorners(corners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                float width = Mathf.Abs(max.x - min.x) + padding.x + sizePaddingX;
                float height = Mathf.Abs(max.y - min.y) + padding.y + sizePaddingY;

                // Convert pixel width/height to canvas local units (anchored size)
                // For ScreenSpace-Overlay, anchoredSize ~= pixel size
                _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }

            // Start pulse
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseRoutine());
        }

        IEnumerator PulseRoutine()
        {
            if (haloImage == null) yield break;
            var canvasGroup = haloImage.canvasRenderer;
            while (true)
            {
                float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
                float a = Mathf.Lerp(minAlpha, maxAlpha, t);
                Color c = haloImage.color;
                c.a = a;
                haloImage.color = c;
                yield return null;
            }
        }

        public void Hide()
        {
            if (_pulseCoroutine != null) { StopCoroutine(_pulseCoroutine); _pulseCoroutine = null; }
            gameObject.SetActive(false);
        }
    }

