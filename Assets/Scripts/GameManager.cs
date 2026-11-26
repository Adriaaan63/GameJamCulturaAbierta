using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum EndAction { NextLevel, RestartLevel, FirstLevel, LoadScene, Nothing }

    [System.Serializable]
    public class LevelData
    {
        [Tooltip("mínimo de boids que hay que capturar para pasar el nivel.")]
        [SerializeField, Min(1)] private int minBoidsToCapture = 20;
        
        [Tooltip("Duración de la noche en segundos. Al acabar se comprueba si se capturó el mínimo.")]
        [SerializeField, Min(1)] private float timeLimit = 120f;
        public int MinBoidsToCapture => minBoidsToCapture;
        public float TimeLimit => timeLimit;
    }

    [System.Serializable]
    public class EndSettings
    {
        [Tooltip("Qué acción se realiza al finalizar el nivel.")]
        public EndAction action = EndAction.NextLevel;
        [Tooltip("Solo si Action = LoadScene.")]
        public string sceneName = "";
        [Tooltip("Retraso antes de ejecutar la acción, por si quieren mostrar animaciones o efectos.")]
        [Min(0)] public float delay = 2f;
        public UnityEvent onTriggered;
    }

    [Header("Niveles")]
    [Tooltip("Número de noches que contiene el juego. Por ejemplo, si es 5, habrá 5 niveles.")]
    [SerializeField, Min(1)] private int numberOfNights = 5;
    [Tooltip("Escena que se carga para jugar cada noche.")]
    [SerializeField] private string gameSceneName = "Game";
    [Tooltip("Configuración de cada noche. Se ajusta sola a 'Number Of Nights'.")]
    [SerializeField] private List<LevelData> levels = new List<LevelData>();
    [Tooltip("Después de la última noche vuelve a la primera. Si no, carga el menú.")]
    [SerializeField] private bool loopLevels = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Fin de nivel")]
    [SerializeField] private EndSettings onWin = new EndSettings { action = EndAction.NextLevel };
    [SerializeField] private EndSettings onLose = new EndSettings { action = EndAction.RestartLevel };

    // Necesitamos el HUD para mostrar progreso y tiempo restante, pero lo dejo igual
    [Header("UI")]
    public UnityEvent<int, int> onCaptureProgress;
    public UnityEvent<float> onTimeChanged;

    // Como no hay HUD aún, dejé esto para poder ver el progreso y el tiempo restante en el Inspector.
    [Header("Debug (solo lectura)")]
    [SerializeField] private int currentLevel;
    [SerializeField] private int captured;
    [SerializeField] private float timeRemaining;
    [SerializeField] private bool playing;

    private LevelData Level => levels[currentLevel];

    // Se ejecuta en el editor al cambiar un valor del Inspector:
    // deja exactamente una entrada en 'levels' por cada noche.
    private void OnValidate()
    {
        while (levels.Count < numberOfNights) levels.Add(new LevelData());
        if (levels.Count > numberOfNights) levels.RemoveRange(numberOfNights, levels.Count - numberOfNights);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BoidSuctionZone.OnAnyBoidCaptured += OnBoidCaptured;
    }

    // si cambiamos la forma del onEnable, también habría que ajustar esto para que se desuscriba correctamente.
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        BoidSuctionZone.OnAnyBoidCaptured -= OnBoidCaptured;
    }

    private void Update()
    {
        if (!playing) return;

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        onTimeChanged?.Invoke(timeRemaining);

        if (timeRemaining <= 0f)
        {
            EndLevel(captured >= Level.MinBoidsToCapture ? onWin : onLose);
        }
    }

    public void StartGame() => LoadLevel(0);
    public void GoToMainMenu()
    {
        StopAllCoroutines();
        playing = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void LoadLevel(int index)
    {
        StopAllCoroutines();
        playing = false;
        currentLevel = index;
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (levels.Count > 0 && scene.name == gameSceneName) {
            BeginLevel();
        } else
        {
            playing = false;
        }
    }

    private void BeginLevel()
    {
        captured = 0;
        timeRemaining = Level.TimeLimit;
        playing = true;
        onCaptureProgress?.Invoke(captured, Level.MinBoidsToCapture);
        onTimeChanged?.Invoke(timeRemaining);
    }

    private void OnBoidCaptured(BoidSuctionZone zone)
    {
        if (!playing) return;

        captured++;
        onCaptureProgress?.Invoke(captured, Level.MinBoidsToCapture);
    }

    private void EndLevel(EndSettings end)
    {
        playing = false;
        end.onTriggered?.Invoke();
        StartCoroutine(RunAfter(end));
    }

    private IEnumerator RunAfter(EndSettings end)
    {
        yield return new WaitForSecondsRealtime(end.delay);

        switch (end.action)
        {
            case EndAction.NextLevel:
                int next = currentLevel + 1;
                if (next < numberOfNights) LoadLevel(next);
                else if (loopLevels) LoadLevel(0);
                else GoToMainMenu();
                break;
            case EndAction.RestartLevel: LoadLevel(currentLevel); break;
            case EndAction.FirstLevel:   LoadLevel(0); break;
            case EndAction.LoadScene:    SceneManager.LoadScene(end.sceneName); break;
            case EndAction.Nothing:      break;
        }
    }
}
