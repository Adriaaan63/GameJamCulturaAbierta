using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PruebaPausaRapida : MonoBehaviour
{
    [Header("Contenedores UI")]
    public GameObject pauseMenu;
    public RawImage blurBackground;

    [Header("Oscurecido")]
    [Range(0f, 1f)] public float brillo = 0.5f; // 1 = sin oscurecer, 0 = negro

    private bool isPaused = false;
    private Texture2D captura;

    void Start()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseMenu != null) pauseMenu.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool inst = GameManager.Instance != null;
            bool play = inst && GameManager.Instance.IsPlaying;
            Debug.Log($"ESC | instance={inst} | playing={play} | pauseMenu={(pauseMenu != null)}");

            if (isPaused) Reanudar();
            else if (play) Pausar();
        }
    }

    public void Pausar()
    {
        if (isPaused) return;
        isPaused = true;
        StartCoroutine(CapturarYPausar());
    }

    private IEnumerator CapturarYPausar()
    {
        yield return new WaitForEndOfFrame();

        if (captura != null) Destroy(captura);
        captura = ScreenCapture.CaptureScreenshotAsTexture();
        captura.wrapMode = TextureWrapMode.Clamp;
        captura.filterMode = FilterMode.Bilinear;

        blurBackground.texture = captura;
        blurBackground.color = new Color(brillo, brillo, brillo, 1f);

        pauseMenu.SetActive(true);
        blurBackground.gameObject.SetActive(true);

        Time.timeScale = 0f;
    }

    public void Reanudar()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = 1f;
        pauseMenu.SetActive(false);
        if (captura != null) { Destroy(captura); captura = null; }
    }

    void OnDestroy() { if (captura != null) Destroy(captura); }


    public void VolverAlMenu()
    {
        isPaused = false;
        Time.timeScale = 1f;
        pauseMenu.SetActive(false);
        if (captura != null) { Destroy(captura); captura = null; }
        GameManager.Instance.GoToMainMenu();
    }
}   