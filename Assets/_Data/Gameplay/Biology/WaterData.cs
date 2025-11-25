using UnityEngine;

/// <summary>
/// Data chứa thông tin nước từ mỗi loại PouringCup
/// </summary>
[System.Serializable]
public class WaterData
{
    [SerializeField] public string groupName; // Tên nhóm (vd: "Nutrient A", "Vitamin B")

    [SerializeField] public Material liquidColor; 
    [SerializeField] public Texture2D texture30; // Texture khi absorb 30%
    [SerializeField] public Texture2D texture80; // Texture khi absorb 80%

    
    
    public WaterData(string name, Material liquidColor, Texture2D tex30, Texture2D tex80)
    {
        this.groupName = name;
        this.liquidColor = liquidColor;
        this.texture30 = tex30;
        this.texture80 = tex80;
    }
}
