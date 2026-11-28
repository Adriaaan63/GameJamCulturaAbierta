using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{

    public enum GizmoType { Never, SelectedOnly, Always }

    [Header("Dependencies")]
    [SerializeField] private BoidManager boidManager;

    public float spawnRadius = 10;
    public int spawnCount = 10;
    public float baseSpawnRate = 5f;
    private float spawnRate = 5f;
    [Tooltip("Only tints the spawn-region gizmo. The boids' colour comes from their fear (see BoidSettings, Debug colour).")]
    public Color colour;
    public GizmoType showSpawnRegion;

    [Header("Placement")]
    [Tooltip("World Y at which boids appear. 0 = as before.")]
    public float spawnHeight = 0f;
    [Tooltip("Off (as before): flattened sphere, denser near the centre. On: uniform over the disc.")]
    public bool uniformDisc = false;
    [Tooltip("How many random positions to try when one lands on an obstacle. If all of them fail, that boid is skipped.")]
    public int maxPlacementAttempts = 10;

    [Header("Options")]
    [Tooltip("Off (as before): boids stay parented to BoidManager. On: they become children of this spawner and are named 'Boid N'.")]
    public bool parentToSpawner = false;
    [Tooltip("Off (as before): the first wave comes after 'spawnRate' seconds. On: a wave also appears right at the start.")]
    public bool spawnOnStart = false;
    [SerializeField] private bool OneSpawn;

    private int spawnedTotal;
    private bool warnedPlacement;

    private float timeRemaining;
    private bool isSpawning;
    
    void Awake()
    {
        if (boidManager == null)
        {
            boidManager = FindFirstObjectByType<BoidManager>();
        }

        timeRemaining = spawnRate;
        isSpawning = true;
        spawnRate = baseSpawnRate;
    }

    void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    private void Update()
    {
        if (!OneSpawn)
        {
            if (!isSpawning) return;

            timeRemaining -= Time.deltaTime;

            if (timeRemaining <= 0f)
            {
                timeRemaining = spawnRate;
                Spawn();
            }
        }
    }

    private void Spawn()
    {
        if (boidManager == null)
        {
            Debug.LogError("Spawner: boidManager es NULL en " + gameObject.name, this);
            return;
        }

        int skipped = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            // 1. Buscar una posición libre de obstáculos
            if (!TryFindFreePosition(out Vector3 pos))
            {
                skipped++;
                continue;
            }

            // 2. Calcular dirección inicial plana
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            Vector3 forward = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;

            // 3. Pedir un boid a la pool en lugar de hacer Instantiate
            Boid boid = boidManager.SpawnFromPool(pos, forward, colour);

            if (boid == null)
            {
                // La pool ha llegado a su límite (poolSize); no seguimos intentando en esta oleada
                break;
            }

            if (parentToSpawner)
            {
                boid.transform.SetParent(transform, true);
                boid.name = "Boid " + spawnedTotal;
            }
            spawnedTotal++;
        }

        if (skipped > 0 && !warnedPlacement)
        {
            warnedPlacement = true;
            Debug.LogWarning("Spawner '" + gameObject.name + "': " + skipped + " boid(s) were skipped because no obstacle-free position was found in " +
                             maxPlacementAttempts + " attempts. (Shown once.)", this);
        }
    }

    private Vector3 RandomSpawnPosition()
    {
        Vector3 pos;

        if (uniformDisc)
        {
            Vector2 p = Random.insideUnitCircle * spawnRadius;
            pos = transform.position + new Vector3(p.x, 0f, p.y);
        }
        else
        {
            pos = transform.position + Random.insideUnitSphere * spawnRadius;
        }

        pos.y = spawnHeight;
        return pos;
    }

    private bool TryFindFreePosition(out Vector3 pos)
    {
        bool canCheck = boidManager != null && boidManager.settings != null;
        float checkRadius = canCheck ? boidManager.settings.boundsRadius + 0.05f : 0f;
        int attempts = Mathf.Max(1, maxPlacementAttempts);

        for (int i = 0; i < attempts; i++)
        {
            pos = RandomSpawnPosition();

            if (!canCheck || !Physics.CheckSphere(pos, checkRadius, boidManager.settings.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
        }

        pos = Vector3.zero;
        return false;
    }

    private void OnDrawGizmos()
    {
        if (showSpawnRegion == GizmoType.Always)
        {
            DrawGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (showSpawnRegion == GizmoType.SelectedOnly)
        {
            DrawGizmos();
        }
    }

    void DrawGizmos()
    {
        Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.3f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
    }
    
    void OnEnable ()  => SkillTreeUI.EffectApplied += OnEffect;
    void OnDisable () => SkillTreeUI.EffectApplied -= OnEffect;

    void OnEffect (StatEffect e) {
            if(e.stat== StatType.SpawnSpeed)
            {
                spawnRate = baseSpawnRate * e.value;
            }
    }
}