using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;

public class Experiment2 : GameController {
    [Header("Experiment 2")]
    [SerializeField] private NoiNangRes resultBook;
    [SerializeField] private XiLanhController xiLanhController;
    [SerializeField] private DongHo dongHo;
    

    private float currentTemp;
    private bool isRunning;
    private Coroutine experimentRoutine;



    [ProButton]
    public override void StartExperiment() {
        if (experimentRoutine != null)
            StopCoroutine(experimentRoutine);

        resultBook.Restart();
        isRunning = true;
        currentTemp = 25f;

        experimentRoutine = StartCoroutine(RunExperiment());
    }

    [ProButton]
    public override void StopExperiment() {
        isRunning = false;
        if (experimentRoutine != null)
            StopCoroutine(experimentRoutine);
        experimentRoutine = null;
    }

    private IEnumerator RunExperiment() {
        float randomScaleTemp = Random.Range(0.8f, 5f);
        while (isRunning) {
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
