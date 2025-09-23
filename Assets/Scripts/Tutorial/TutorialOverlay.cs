using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


    [RequireComponent(typeof(Image))]
    public class TutorialOverlay : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private Image dimImage;
        [Range(0f, 1f)] public float dimAlpha = 0.75f;

        private readonly HashSet<RectTransform> _whitelist = new();

        private void Reset()
        {
            dimImage = GetComponent<Image>();
            if (dimImage != null)
            {
                dimImage.color = new Color(0f, 0f, 0f, dimAlpha);
                dimImage.raycastTarget = true;
            }
        }

        public void SetDim(bool enabled)
        {
            if (!dimImage) return;
            var c = dimImage.color;
            c.a = enabled ? dimAlpha : 0f;
            dimImage.color = c;
            dimImage.raycastTarget = enabled;   // bật tắt chặn click theo dim
        }

        // ⭐ THÊM MỚI: Bật/tắt blocker trong suốt (không dim nhưng vẫn chặn click UI)
        public void SetBlockerOnly(bool enabled)
        {
            if (!dimImage) return;
            var c = dimImage.color;
            c.a = 0f;                            // không còn màu xám
            dimImage.color = c;
            dimImage.raycastTarget = enabled;    // true=chặn toàn bộ, false=không chặn
        }

        // (tùy chọn) set alpha thủ công
        public void SetDimAlpha(float alpha)
        {
            if (!dimImage) return;
            var c = dimImage.color;
            c.a = Mathf.Clamp01(alpha);
            dimImage.color = c;
            dimImage.raycastTarget = alpha > 0f;
        }

        public void ClearWhitelist() => _whitelist.Clear();
        public void AddWhitelist(RectTransform rt) { if (rt) _whitelist.Add(rt); }

        // LƯU Ý: logic đã đảo để vùng whitelist cho click lọt qua
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (dimImage == null || dimImage.raycastTarget == false) return true;

            // nếu chạm lên vùng whitelisted -> overlay KHÔNG ăn raycast -> cho lọt xuống
            foreach (var rt in _whitelist)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, sp, eventCamera))
                    return false; // <-- quan trọng
            }
            // ngoài whitelist -> overlay ăn raycast để CHẶN
            return true;
        }
    }

