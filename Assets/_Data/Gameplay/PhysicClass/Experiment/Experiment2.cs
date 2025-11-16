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



    [ProButton]
    public override void StartExperiment() {
        if (experimentRoutine != null)
            StopCoroutine(experimentRoutine);

        resultBook.Restart();
        isExperimentRunning = true;
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
        base.StopExperiment();
    }

    private IEnumerator RunExperiment() {
        float randomScaleTemp = Random.Range(0.8f, 5f);
        while (isExperimentRunning) {
            // Đọc thể tích hiện tại (0–100 ml)
            float volume = xiLanhController.CurrentVolume;

            // Giả lập: càng nén khí (thể tích nhỏ) thì nhiệt độ càng cao
            // (volume giảm thì temp tăng)
            // Generate random temperature between 22.00 and 25.00
            float temp = Random.Range(22f, 25f);

            float compressionRatio = 1f - (volume / 100f);
            float targetTemp = temp + compressionRatio * randomScaleTemp; 

            currentTemp = Mathf.Lerp(currentTemp, targetTemp, Time.deltaTime * 2f);

            if (dongHo != null)
                dongHo.valueText.text = $"{currentTemp:F1}°C";

            yield return null; // chờ frame tiếp theo
        }
    }

    public override string GetExperimentName() {
        return "NoiNang";
    }
}
