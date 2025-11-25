using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Experiment 3: Vận chuyển nước trong thân
/// Thí nghiệm từ SGK Sinh học lớp 11 - Kết nối tri thức
/// 
/// Cơ chế: Can thiệp vào MeshRenderer Material -> thay đổi baseMap với các texture
/// Start: Tạo mới MeshRenderer để không ảnh hưởng Material gốc
/// 
/// Các bước thí nghiệm:
/// 1. DoNuoc: 3 Glass đủ nước >80f
/// 2. DatBongHoa: 3 Glass có Flower
/// 3. QuanSat: 3 Flower hấp thụ nước >80%
/// </summary>
public class Experiment3 : GameController
{
    [Header("Experiment 3 - Water Transport")]
    public List<GlassController> glassList = new List<GlassController>();
    public List<FlowerController> flowerList = new List<FlowerController>();
    public List<PouringCup> cupList = new List<PouringCup>();

    [Header("Experiment Steps")]
    public bool doNuocCompleted = false;
    public bool datBongHoaCompleted = false;
    public bool quanSatCompleted = false;

    [Header("Initial Setup")]
    [SerializeField] private float initialCupWaterAmount = 100f;
    [SerializeField] private Texture2D flowerDryTexture; // Texture khô ban đầu cho flowers

    [Header("Debug")]
    [SerializeField] private bool debugSteps = true;

    void Update()
    {
        CheckExperimentSteps();
    }

    /// <summary>
    /// Kiểm tra các bước thí nghiệm
    /// </summary>
    private void CheckExperimentSteps()
    {
        if(doNuocCompleted && datBongHoaCompleted && quanSatCompleted)
            return; // All steps completed
        // Bước 1: DoNuoc - Tất cả Glass đủ nước >80f
        if (!doNuocCompleted)
        {
            if (CheckDoNuocStep())
            {
                
                guideStepManager.CompleteStep("DoNuoc");
                doNuocCompleted = true;
            }
        }

        // Bước 2: DatBongHoa - Tất cả Glass có Flower
        if (doNuocCompleted && !datBongHoaCompleted)
        {
            if (CheckDatBongHoaStep())
            {
                guideStepManager.CompleteStep("DatBongHoa");
                datBongHoaCompleted = true;
            }
        }

        // Bước 3: QuanSat - Tất cả Flower hấp thụ >80%
        if (datBongHoaCompleted && !quanSatCompleted)
        {
            if (CheckQuanSatStep())
            {
                guideStepManager.CompleteStep("QuanSat");
                this.StartExperiment();
                quanSatCompleted = true;
            }
        }
    }

    /// <summary>
    /// Kiểm tra bước DoNuoc: Tất cả Glass đủ nước >80f
    /// </summary>
    private bool CheckDoNuocStep()
    {
        if (glassList.Count < 3) return false;

        foreach (GlassController glass in glassList)
        {
            if (glass == null || glass.GetCurrentAmount() <= 80f)
            {
                return false;
            }
        }

        if (debugSteps)
            Debug.Log("[Experiment3] DoNuoc step ready - All glasses have >80f water");

        return true;
    }

    /// <summary>
    /// Kiểm tra bước DatBongHoa: Tất cả Glass có Flower
    /// </summary>
    private bool CheckDatBongHoaStep()
    {
        if (glassList.Count < 3) return false;

        foreach (GlassController glass in glassList)
        {
            if (glass == null || glass.connectedFlower == null)
            {
                return false;
            }
        }

        if (debugSteps)
            Debug.Log("[Experiment3] DatBongHoa step ready - All glasses have flowers");

        return true;
    }

    /// <summary>
    /// Kiểm tra bước QuanSat: Tất cả Flower hấp thụ >80%
    /// </summary>
    private bool CheckQuanSatStep()
    {
        if (flowerList.Count < 3) return false;

        foreach (FlowerController flower in flowerList)
        {
            if (flower == null || flower.GetWaterAbsorptionPercentage() <= 80f)
            {
                return false;
            }
        }

        if (debugSteps)
            Debug.Log("[Experiment3] QuanSat step ready - All flowers absorbed >80%");

        return true;
    }

    /// <summary>
    /// Restart tất cả cups - khởi tạo lại lượng nước
    /// </summary>
    private void RestartCups()
    {
        foreach (PouringCup cup in cupList)
        {
            if (cup != null)
            {
                cup.SetAmount(initialCupWaterAmount);
                cup.RestartPosition();
                cup.gameObject.SetActive(true);
                if (debugSteps)
                    Debug.Log($"[Experiment3] Reset cup {cup.name} water to {initialCupWaterAmount}f");
            }
        }
    }

    /// <summary>
    /// Restart tất cả glasses - xóa trạng thái và waterData
    /// </summary>
    private void RestartGlasses()
    {
        foreach (GlassController glass in glassList)
        {
            if (glass != null)
            {
                // Xóa nước và waterData
                glass.EmptyGlass();

                glass.EnableInteractions();
                glass.SetFlowerNearby(false);
                
                // Reset trạng thái
                glass.connectedFlower = null;
                
                if (debugSteps)
                    Debug.Log($"[Experiment3] Reset glass {glass.name} - cleared water and waterData");
            }
        }
    }

    /// <summary>
    /// Restart tất cả flowers - về texture khô và xóa trạng thái
    /// </summary>
    private void RestartFlowers()
    {
        foreach (FlowerController flower in flowerList)
        {
            if (flower != null)
            {
                // Reset absorption state
                flower.OnUnsnapped();
                
                // Enable physics
                flower.EnablePhysics();
                flower.snapTrigger.EnableHandInteractions();
                flower.RestartPosition();
                
                // Reset SnapTrigger state
                SnapTrigger snapTrigger = flower.GetComponentInChildren<SnapTrigger>();
                if (snapTrigger != null)
                {
                    snapTrigger.Unsnap();
                }

                flower.RestartDryTexture();
                
                if (debugSteps)
                    Debug.Log($"[Experiment3] Reset flower {flower.name} - cleared state and set dry texture");
            }
        }
    }



    [ProButton]
    public override void SetupExperiment()
    {
        base.SetupExperiment();
        
        Debug.Log($"[Experiment3] Setup experiment: {GetExperimentName()}");
        
        // Reset experiment steps
        doNuocCompleted = false;
        datBongHoaCompleted = false;
        quanSatCompleted = false;
        
        // Reset cups - khởi tạo lại lượng nước
        RestartCups();
        
        // Reset glasses - xóa trạng thái và waterData
        RestartGlasses();
        
        // Reset flowers - về texture khô và xóa trạng thái
        RestartFlowers();
        
        isExperimentRunning = false;
        
        if (debugSteps)
            Debug.Log("[Experiment3] Experiment setup completed");
    }


    [ProButton]
    public override void StartExperiment()
    {  
        base.StartExperiment();
    }


    [ProButton]
    public override void StopExperiment()
    {
        base.StopExperiment();
        
        if (debugSteps)
            Debug.Log("[Experiment3] Experiment stopped");
    }
    
    public override string GetExperimentName()
    {
        return "VanChuyenNuocOThanCay";
    }

    public override bool IsExperimentRunning()
    {
        return isExperimentRunning;
    }
}
