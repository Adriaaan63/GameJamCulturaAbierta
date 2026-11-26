using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("Socre Reference")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private string prefix = "Puntos: ";
    
    [Header("UI Reference")]
    [SerializeField] private Image arcFilledImage;
    [SerializeField] private RectTransform moonHandle;
    [SerializeField] private RectTransform arcCenter;
    
    [Header("Arc Geometry")]
    [Tooltip("Radio en píxeles desde el centro hasta la trayectoria de la luna.")]
    [SerializeField] private float radius = 130f;

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
            arcFilledImage.fillAmount = progress;
        }

        if (moonHandle != null)
        {
            //En radial 180 de izquierda a derecha:
            //progress = 0 -> 180 grados
            //progress = 0.5 -> 90 grados
            //progress = 1 -> 0 grados

            float angleRad = Mathf.Lerp(Mathf.PI, 0f, progress);

            Vector2 centerPos = arcCenter != null ? arcCenter.anchoredPosition : Vector2.zero;
            float x = centerPos.x + Mathf.Cos(angleRad) * radius;
            float y = centerPos.y + Mathf.Sin(angleRad) * radius;

            moonHandle.anchoredPosition = new Vector2(x, y);
        }
    }

    private void OnValidate()
    {
        SetProgress(progress);
    }
}