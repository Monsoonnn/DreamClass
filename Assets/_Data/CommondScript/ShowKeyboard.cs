using TMPro;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
public class ShowKeyboard : NewMonobehavior {
    [SerializeField] private TMP_InputField m_InputField;
    public float distance = 0.5f;
    public float verticalOffset = -0.5f;
    public Transform positionSource;

    protected override void Start() {
        m_InputField = GetComponent<TMP_InputField>();
        positionSource = Camera.main.transform;
        // Open keyboard when selecting the field
        m_InputField.onSelect.AddListener(x => OpenKeyboard());
    }

    public void OpenKeyboard() {
        NonNativeKeyboard.Instance.InputField = m_InputField;
        NonNativeKeyboard.Instance.PresentKeyboard(m_InputField.text);

        Vector3 direction = positionSource.forward;
        direction.y = 0;
        direction.Normalize();

        Vector3 targetPosition = positionSource.position
                                 + direction * distance
                                 + Vector3.up * verticalOffset;

        NonNativeKeyboard.Instance.RepositionKeyboard(targetPosition);
    }

}
