using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("Score Reference")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private string prefix = "Puntos: ";

    [Header("UI Reference")]
    [SerializeField] private Image arcFilledImage;
    [SerializeField] private RectTransform ejeLuna;

    [Header("Arc Fill Calibration")]
    [Tooltip("Valor de Fill Amount donde empieza visualmente el arco (izquierda).")]
    [Range(0f, 1f)][SerializeField] private float startFill = 0f;
    [Tooltip("Valor de Fill Amount donde termina visualmente el arco (derecha).")]
    [Range(0f, 1f)][SerializeField] private float endFill = 1f;

    [Header("Moon Pivot Rotation (Z)")]
    [Tooltip("Ángulo Z de EjeLuna cuando la luna está en el extremo izquierdo.")]
    [SerializeField] private float startAngleZ = 0f;
    [Tooltip("Ángulo Z de EjeLuna cuando la luna llega al extremo derecho.")]
    [SerializeField] private float endAngleZ = -90f;

    [Header("Preview / Debug")]
    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f; // 0 = empieza la noche, 1 = amanece

    private string formatTemplate;
    private int pendingScore;
    private bool isDirty;

    private void Awake()
    {
        // Preparamos la plantilla una sola vez al inicio (ej. "Puntos: {0}")
        formatTemplate = prefix + "{0}";
    }

    public void UpdateScoreText(int newScore)
    {
        pendingScore = newScore;
        isDirty = true;
    }

    private void LateUpdate()
    {
        if (!isDirty) return;

        isDirty = false;
        // Ahora el argumento 2 es un int/float, cero basura en memoria
        scoreText.SetText(formatTemplate, pendingScore);
    }

    public void SetProgress(float normalizedTime)
    {
        progress = Mathf.Clamp01(normalizedTime);

        if (arcFilledImage != null)
        {
            arcFilledImage.fillAmount = Mathf.Lerp(startFill, endFill, progress);
        }

        if (ejeLuna != null)
        {
            float currentAngleZ = Mathf.Lerp(startAngleZ, endAngleZ, progress);
            ejeLuna.localEulerAngles = new Vector3(0f, 0f, currentAngleZ);
        }
    }

    private void OnValidate()
    {
        SetProgress(progress);
    }
}