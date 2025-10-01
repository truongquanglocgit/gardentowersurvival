using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal; // URP camera stack


    /// <summary>
    /// Gắn trên TutorialRoot. Tự bind UICam/LiftCam theo scene.
    /// - Ở MainMenu (không có UICam) -> để Canvas = ScreenSpace-Overlay.
    /// - Ở Level (có UICam) -> chuyển Canvas = ScreenSpace-Camera (UICam) + add stack LiftCam.
    /// </summary>
    public class TutorialCameraBinder : MonoBehaviour
    {
        [Header("Refs")]
        public Canvas tutorialCanvas;          // Canvas của TutorialRoot (DontDestroy)
        public Camera cachedUICam;
        public Camera cachedLiftCam;

        [Header("Tags to find")]
        public string worldCamTag = "MainCamera";
        public string uiCamTag = "UICam";
        public string liftCamTag = "LiftCam";

        void Reset()
        {
            tutorialCanvas = GetComponentInChildren<Canvas>(true);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            // bind ngay scene đầu (MainMenu)
            BindForCurrentScene();
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            BindForCurrentScene();
        }

        void BindForCurrentScene()
        {
            if (tutorialCanvas == null)
            {
                tutorialCanvas = GetComponentInChildren<Canvas>(true);
                if (tutorialCanvas == null)
                {
                    Debug.LogWarning("[Tutorial] No tutorialCanvas found to bind.");
                    return;
                }
            }

            // Tìm UICam & LiftCam trong scene mới
            cachedUICam = FindCameraByTag(uiCamTag);
            cachedLiftCam = FindCameraByTag(liftCamTag);
            var worldCam = FindCameraByTag(worldCamTag);

            if (cachedUICam == null || worldCam == null)
            {
                // Không có UICam (ví dụ MainMenu) -> dùng Overlay cho tiện
                tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                tutorialCanvas.worldCamera = null;
                //Debug.Log("[Tutorial] No UICam found → Canvas set to ScreenSpace-Overlay.");
                return;
            }

            // Có UICam: chuyển Canvas qua ScreenSpace-Camera
            tutorialCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            tutorialCanvas.worldCamera = cachedUICam;
        var scaler = tutorialCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // đổi theo chuẩn của bạn
            scaler.matchWidthOrHeight = 0.5f;
        }

        // đảm bảo anchors full
        var rt = tutorialCanvas.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        // Gắn stack: thêm UICam + LiftCam vào WorldCam (URP)
        var worldData = worldCam.GetUniversalAdditionalCameraData();
            var uiData = cachedUICam.GetUniversalAdditionalCameraData();
            if (uiData.renderType != CameraRenderType.Overlay)
                uiData.renderType = CameraRenderType.Overlay;

            if (!worldData.cameraStack.Contains(cachedUICam))
                worldData.cameraStack.Add(cachedUICam);

            if (cachedLiftCam != null)
            {
                var liftData = cachedLiftCam.GetUniversalAdditionalCameraData();
                if (liftData.renderType != CameraRenderType.Overlay)
                    liftData.renderType = CameraRenderType.Overlay;

                if (!worldData.cameraStack.Contains(cachedLiftCam))
                    worldData.cameraStack.Add(cachedLiftCam);
            }

            Debug.Log("[Tutorial] Bound to UICam/LiftCam and stacked on WorldCam.");
        }

        Camera FindCameraByTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return null;
            var gos = GameObject.FindGameObjectsWithTag(tagName);
            foreach (var go in gos)
            {
                var cam = go.GetComponent<Camera>();
                if (cam != null && cam.isActiveAndEnabled) return cam;
            }
            return null;
        }
    }

    static class URPExt
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera cam)
        {
            if (cam == null) return null;
            if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            return data;
        }
    }

