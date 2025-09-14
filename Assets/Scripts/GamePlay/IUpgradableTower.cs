// IUpgradableTower.cs
public interface IUpgradableTower
{
    // Thông tin hiển thị
    string TowerName { get; }
    string TargetingModeDisplay { get; } // có thể trả "" nếu không dùng

    // Cấp & sao
    int Level { get; }
    int MaxLevel { get; }
    bool CanUpgrade { get; }

    // Chỉ số hiện tại
    float CurrentDamage { get; }
    float CurrentFireRate { get; }

    // Chỉ số ở level bất kỳ (UI dùng để xem trước cấp tiếp theo)
    float GetDamageAtLevel(int level);
    float GetFireRateAtLevel(int level);

    // Kinh tế
    int GetUpgradeCost();
    int GetSellPrice();

    // Hành động
    bool Upgrade();           // true nếu nâng cấp thành công
    void SellAndDestroy();
}
