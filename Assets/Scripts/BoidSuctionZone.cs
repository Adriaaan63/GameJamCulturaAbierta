using UnityEngine;

public class BoidSuctionZone : MonoBehaviour
{
    [SerializeField] private BoidManager boidManager;

    [Header("Radii & Pull")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float suctionRadius = 4.5f;
    [SerializeField] private float suctionStrength = 15f;

    [Header("Capacity & Processing Rate")]
    [Tooltip("Cuántos boids caben a la vez procesándose en la aspiradora (huecos).")]
    [SerializeField] private float maxCapacity = 3f;
    [Tooltip("Cuántos boids elimina por segundo para liberar huecos.")]
    [SerializeField] private float processRate = 1f;

    [Header("Debug")]
    [SerializeField] private int capturedCount = 0;
    [SerializeField] private int processedCount = 0;
    [SerializeField] private float currentLoad = 0f; // Cuánto está llena ahora mismo (0 .. maxCapacity)
    [SerializeField] private int TotalScore = 0;
    
    public float Radius => radius;
    public float SuctionRadius => suctionRadius;
    public float SuctionStrength => suctionStrength;
    public int CapturedCount => capturedCount;
    public int ProcessedCount => processedCount;

    // Hay hueco si al meter 1 boid nuevo no superamos la capacidad máxima
    public bool HasSlot => (currentLoad + 1f) <= maxCapacity;


    private int checkIntScore = 0;
    
    private void Awake()
    {
        if (boidManager == null)
            boidManager = FindAnyObjectByType<BoidManager>();
    }

    private void OnEnable()
    {
        if (boidManager != null)
            boidManager.RegisterSuctionZone(this);
    }

    private void OnDisable()
    {
        if (boidManager != null)
            boidManager.UnregisterSuctionZone(this);
    }

    private void Update()
    {
        // Se ejecuta UNA sola vez por frame: va vaciando la carga poco a poco
        if (currentLoad > 0f)
        {
            // int currentInteger = Mathf.CeilToInt(currentLoad);
            currentLoad = Mathf.Max(0f, currentLoad - processRate * Time.deltaTime);
            
            if (checkIntScore-1 >= Mathf.CeilToInt(currentLoad))
            {
                checkIntScore--;
                OnBoidProcessed();
            }
        }
    }

    public static event System.Action<BoidSuctionZone> OnAnyBoidProcessed;
    public void OnBoidProcessed()
    {
        TotalScore++;
        OnAnyBoidProcessed?.Invoke(this);
    }

    public static event System.Action<BoidSuctionZone> OnAnyBoidCaptured;

    public void OnBoidCaptured(Boid boid)
    {
        capturedCount++;
        checkIntScore++;
        currentLoad += 1f;
        OnAnyBoidCaptured?.Invoke(this);
        // Aquí metemos partículas, sonidos, etc.
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = new Color(1f, 0.5f, 0.31f);
        Gizmos.DrawWireSphere(transform.position, suctionRadius);
    }
}