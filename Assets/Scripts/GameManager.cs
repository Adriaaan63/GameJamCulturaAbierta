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
        [SerializeField] private string sceneName = "Game";
        [Tooltip("Boids que hay que capturar para ganar el nivel.")]
        [SerializeField, Min(1)] private int boidsToCapture = 20;
        [Tooltip("Segundos. 0 = sin límite.")]
        // seteé un mínimo de 0, es medio innecesario, pero para prevenir valores negativos o errores de tipeo
        // o me puse muy QA?
        [SerializeField, Min(0)] private float timeLimit = 120f;

        public string SceneName => sceneName;
        public int BoidsToCapture => boidsToCapture;
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
    [Tooltip("Lista de niveles en el juego.")]
    [SerializeField] private List<LevelData> levels = new List<LevelData>();
    [Tooltip("Después del último nivel vuelve al primero. Si no, carga el menú.")]
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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // No sé si les parece bien esto, si quieren lo podemos cambiar,
        // pasa que de esta forma el Start() sólo se ejecuta una vez, por lo que al cambiar de nivel
        // el GM sigue siendo el mismo objeto y no vuelve a pasar por Start()
        // a mí me parece bien, no sé uds.
        SceneManager.sceneLoaded += OnSceneLoaded;
        BoidSuctionZone.OnAnyBoidCaptured += OnBoidCaptured;
    }

    // si cambiamos la forma del onEnable, también habría que ajustar esto para que se desuscriba correctamente.
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        BoidSuctionZone.OnAnyBoidCaptured -= OnBoidCaptured;
    }

    private void Start()
    {
        int levelIndex = levels.FindIndex(l => l.SceneName == SceneManager.GetActiveScene().name);
        if (levelIndex >= 0) { currentLevel = levelIndex; BeginLevel(); }
    }

    private void Update()
    {
        if (!playing || Level.TimeLimit <= 0f) return;

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        onTimeChanged?.Invoke(timeRemaining);

        if (timeRemaining <= 0f) EndLevel(onLose);
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
        SceneManager.LoadScene(Level.SceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (levels.Count > 0 && scene.name == Level.SceneName) {
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
        onCaptureProgress?.Invoke(captured, Level.BoidsToCapture);
        onTimeChanged?.Invoke(timeRemaining);
    }

    private void OnBoidCaptured(BoidSuctionZone zone)
    {
        if (!playing) return;

        captured++;
        onCaptureProgress?.Invoke(captured, Level.BoidsToCapture);

        if (captured >= Level.BoidsToCapture) EndLevel(onWin);
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
                if (next < levels.Count) LoadLevel(next);
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
