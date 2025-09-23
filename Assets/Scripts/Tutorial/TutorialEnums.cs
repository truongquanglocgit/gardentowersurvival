using UnityEngine;


    public enum StepMode { TapAnywhere, ForceTarget }

    public enum ResolveMode
    {
        None,               // TapAnywhere
        ByRef,              // Kéo thả trực tiếp UI target
        ByName,             // Tìm UI theo tên
        ByTag,              // Tìm UI theo tag
        RuntimeFindByName,  // Tên (UI/World) lúc runtime
        RuntimeFindByTag,   // Tag (UI/World) lúc runtime
        WaitForTowerEvent   // Đợi sự kiện TowerPlaced (tối ưu)
    }

