using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour {

    public enum GizmoType { Never, SelectedOnly, Always }

    [Header("Dependencies")]
    [SerializeField] private BoidManager boidManager;


    public Boid prefab;
    public float spawnRadius = 10;
    public int spawnCount = 10;
    public float spawnRate = 5f;
    public Color colour;
    public GizmoType showSpawnRegion;

    private float timeRemaining;
    private bool isSpawning;

    void Awake () {

        if (boidManager == null)
        {
            boidManager = FindFirstObjectByType<BoidManager>();
        }

        timeRemaining = spawnRate;
        isSpawning = true;
    }



    private void Update()
    {
        if (!isSpawning) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = spawnRate;
            //isSpawning = false;
            Spawn();
        }
    }
    private void Spawn()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 pos = transform.position + Random.insideUnitSphere * spawnRadius;
            pos.y = 0;
            Boid boid = Instantiate(prefab);
            boid.transform.position = pos;

            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0;
            boid.transform.forward = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;

            boid.SetColour(colour);

            // Registramos el boid recién creado en el manager:
            if (boidManager != null)
            {
                boidManager.RegisterBoid(boid);
            }
            else
            {
                Debug.LogError("Spawner: boidManager es NULL en " + gameObject.name, this);
            }
        }
    }
    private void OnDrawGizmos () {
        if (showSpawnRegion == GizmoType.Always) {
            DrawGizmos ();
        }
    }

    void OnDrawGizmosSelected () {
        if (showSpawnRegion == GizmoType.SelectedOnly) {
            DrawGizmos ();
        }
    }

    void DrawGizmos () {

        Gizmos.color = new Color (colour.r, colour.g, colour.b, 0.3f);
        Gizmos.DrawSphere (transform.position, spawnRadius);
    }

}