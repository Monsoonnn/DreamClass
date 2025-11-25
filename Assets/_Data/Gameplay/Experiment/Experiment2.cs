using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;

public class Experiment2 : GameController {
    [Header("Experiment 2")]
    [SerializeField] private NoiNangRes resultBook;
    [SerializeField] private XiLanhController xiLanhController;
    [SerializeField] private DongHo dongHo;
    

    private float currentTemp;
    private Coroutine experimentRoutine;

    public override void SetupExperiment() {
        base.SetupExperiment();
        dongHo.Restart();
        resultBook.Restart();
    }


    [ProButton]
    public override void StartExperiment() {
        base.StartExperiment();
        if (experimentRoutine != null)
            StopCoroutine(experimentRoutine);

        resultBook.Restart();
        currentTemp = 25f;

        experimentRoutine = StartCoroutine(RunExperiment());
    }

    [ProButton]
    public override void StopExperiment() {
        if (experimentRoutine != null)
            StopCoroutine(experimentRoutine);
        NotifyExperimentCompleted();
        experimentRoutine = null;
        Debug.Log($"[Experiment2] {this.gameObject.name} Experiment stopped!");

        // Gọi base.StopExperiment() trực tiếp, không cần delay
        base.StopExperiment();
    }

    private IEnumerator RunExperiment() {
        float randomScaleTemp = Random.Range(0.8f, 5f);
        float updateTimer = 0f;
        const float UPDATE_INTERVAL = 0.1f; // Update mỗi 100ms thay vì mỗi frame
        
        while (isExperimentRunning) {
            updateTimer += Time.deltaTime;
            
            if (updateTimer >= UPDATE_INTERVAL) {
                // Đọc thể tích hiện tại (0–100 ml)
                float volume = xiLanhController.CurrentVolume;

                // Giả lập: càng nén khí (thể tích nhỏ) thì nhiệt độ càng cao
                // (volume giảm thì temp tăng)
                // Generate random temperature between 22.00 and 25.00
                float temp = Random.Range(22f, 25f);

                float compressionRatio = 1f - (volume / 100f);
                float targetTemp = temp + compressionRatio * randomScaleTemp; 

                currentTemp = Mathf.Lerp(currentTemp, targetTemp, updateTimer * 2f);
                
                updateTimer = 0f;
            }

            yield return null; // chờ frame tiếp theo nhưng tính toán ít hơn
        }
    }

    public override string GetExperimentName() {
        return "NoiNang";
    }
    
    public float GetCurrentTemp() {
        return currentTemp;
    }
}
