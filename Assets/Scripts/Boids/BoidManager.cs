using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour {

    const int threadGroupSize = 1024;

    [Header("Boid Settings")]
    public BoidSettings settings;
    public ComputeShader compute;
    public Boid boidPrefab;
    public int poolSize = 1000;

    [Header("Scene References")]

    public Transform player;
    public Flashlight flashlight;

    private List<Boid> boids = new List<Boid>();
    private Stack<Boid> inactivePool= new Stack<Boid>();
    private List<BoidSuctionZone> suctionZones = new List<BoidSuctionZone>();

    //Buffer reutilizables para evitar GC Alloc cada frame
    private BoidData[] boidDataBuffer;
    private ComputeBuffer computeBuffer;

    // Lista reutilizable para decidir el panico sin crear basura cada frame.
    private readonly List<Boid> beamBoids = new List<Boid>();

    // Informacion de solo lectura, util para calibrar. Se actualiza cada frame.
    [Header ("Debug (read only)")]
    [SerializeField] int boidsIdle;
    [SerializeField] int boidsFleeing;
    [SerializeField] int boidsPanicking;
    [Tooltip ("Boids currently inside the beam (any state).")]
    [SerializeField] int boidsInBeamCount;
    [Tooltip ("Average fear of the boids inside the beam that can still panic. This is the number compared with the threshold.")]
    [SerializeField] float averageBeamFear;

    public int BoidsInBeam => boidsInBeamCount;
    public float AverageBeamFear => averageBeamFear;
    // ------------------------------------------------------------------------------

    void Awake()
    {
        //Pre-alojamos buffers y la pool de golpe al arrancar
        boidDataBuffer = new BoidData[poolSize];
        computeBuffer = new ComputeBuffer(poolSize, BoidData.Size);

        for(int i=0; i < poolSize; i++)
        {
            Boid b = Instantiate(boidPrefab, transform);
            b.Initialize(settings, null);
            b.threat = player;
            b.flashlight = flashlight;
            b.gameObject.SetActive(false);
            inactivePool.Push(b);
        }
    }

    public void RegisterSuctionZone(BoidSuctionZone zone)
    {
        if(zone != null && !suctionZones.Contains(zone))
        {
            suctionZones.Add(zone);
        }
    }

    public void UnregisterSuctionZone(BoidSuctionZone zone)
    {
        suctionZones.Remove(zone);
    }

    public Boid SpawnFromPool(Vector3 position, Vector3 forward, Color colour)
    {
        if (inactivePool.Count == 0) return null; //Pool llena

        Boid b = inactivePool.Pop();
        b.transform.position = position;
        b.transform.forward = forward;
        b.SetColour(colour);
        b.gameObject.SetActive(true);
        b.Initialize(settings, null);

        boids.Add(b);
        return b;
    }

    private void DespawnAtIndex(int index)
    {
        Boid b = boids[index];
        b.gameObject.SetActive(false);
        inactivePool.Push(b);

        //Swap&Pop en List<Boid>: O(1) sin desplazar elementos
        int lastIndex = boids.Count - 1;
        boids[index] = boids[lastIndex];
        boids.RemoveAt(lastIndex);
    }

    //void Start () {
    //    //Por si existe algun boid en la escena puesto a mano
    //    Boid[] initialBoids = FindObjectsByType<Boid>(FindObjectsSortMode.None);
    //    foreach (Boid b in initialBoids)
    //    {
    //        RegisterBoid(b);
    //    }
    //}

    //Metodo para que Spwnaer pueda registrar cada boid al instanciarlo
    //public void RegisterBoid(Boid b)
    //{
    //    if (b == null || boids.Contains(b)) return;

    //    b.Initialize(settings, null);
    //    b.threat = player;
    //    b.flashlight = flashlight;
    //    boids.Add(b);
    //    //Debug.Log("Boid registrado. Total en lista: " + boids.Count);
    //}

    void Update()
    {
        if (flashlight != null) flashlight.Refresh();

        int numBoids = boids.Count;
        if(numBoids == 0)
        {
            ResetStats();
            return;
        }

        //Empaquetamos los datos activos
        for(int i = 0; i < numBoids; i++)
        {
            boidDataBuffer[i].position = boids[i].position;
            boidDataBuffer[i].direction = boids[i].forward;
        }

        computeBuffer.SetData(boidDataBuffer, 0, 0, numBoids);

        compute.SetBuffer(0, "boids", computeBuffer);
        compute.SetInt("numBoids", numBoids);
        compute.SetFloat("viewRadius", settings.perceptionRadius);
        compute.SetFloat("avoidRadius", settings.avoidanceRadius);

        int threadGroups = Mathf.CeilToInt(numBoids / (float)threadGroupSize);
        compute.Dispatch(0, threadGroups, 1, 1);

        computeBuffer.GetData(boidDataBuffer, 0, 0, numBoids);

        //Actualizamos movimiento y comporbamos succionadores
        // En BoidManager.Update():
        for (int i = numBoids - 1; i >= 0; i--)
        {
            Boid b = boids[i];

            // 1. Evaluamos captura y atracción ANTES de UpdateBoid()
            if (ProcessSuctionZones(b))
            {
                DespawnAtIndex(i);
                continue; // Ya fue capturado, no hace falta moverlo este frame
            }

            // 2. Asignamos datos del Compute Shader (corregido)
            b.avgFlockHeading = boidDataBuffer[i].flockHeading;
            b.centreOfFlockmates = boidDataBuffer[i].flockCentre;
            b.avgAvoidanceHeading = boidDataBuffer[i].avoidanceHeading;
            b.numPerceivedFlockmates = boidDataBuffer[i].numFlockmates;

            // 3. Ejecutamos el movimiento (ya incluye la aceleración del aspirador)
            b.UpdateBoid();
        }
        UpdateStatsAndPanic();
    }

    private bool ProcessSuctionZones(Boid b)
    {
        Vector3 bPos = b.position;

        for (int z = 0; z < suctionZones.Count; z++)
        {
            BoidSuctionZone zone = suctionZones[z];
            Vector3 zPos = zone.transform.position;

            // Vector desde el boid HACIA el centro del aspirador
            float dx = zPos.x - bPos.x;
            float dz = zPos.z - bPos.z;
            float distSqr = dx * dx + dz * dz;

            float captureR = zone.Radius;
            if (distSqr <= captureR * captureR)
            {
                zone.OnBoidCaptured(b);
                return true; // Capturado
            }

            float suctionR = zone.SuctionRadius;
            if (distSqr <= suctionR * suctionR && distSqr > 0.0001f)
            {
                float dist = Mathf.Sqrt(distSqr);
                Vector3 dirToCenter = new Vector3(dx / dist, 0f, dz / dist);

                // Opcional: que tire más fuerte cuanto más cerca esté del centro (falloff inverso)
                float pullFactor = 1f - (dist / suctionR);
                b.AddAcceleration(dirToCenter * (zone.SuctionStrength * pullFactor));
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        if(computeBuffer != null)
        {
            computeBuffer.Release();
            computeBuffer = null;
        }
    }


    // NUEVO ------------------------------------------------------------------------

    void ResetStats ()
    {
        boidsIdle = 0;
        boidsFleeing = 0;
        boidsPanicking = 0;
        boidsInBeamCount = 0;
        averageBeamFear = 0f;
    }

    void UpdateStatsAndPanic ()
    {
        ResetStats();

        beamBoids.Clear ();
        float fearSum = 0f;

        for (int i = 0; i < boids.Count; i++) {
            Boid b = boids[i];

            switch (b.State) {
                case BoidState.Idle: boidsIdle++; break;
                case BoidState.Fleeing: boidsFleeing++; break;
                case BoidState.Panic: boidsPanicking++; break;
            }

            if (b.InBeam) {
                boidsInBeamCount++;
                if (b.CanPanic) {
                    beamBoids.Add (b);
                    fearSum += b.Fear;
                }
            }
        }

        if (flashlight == null || !flashlight.active || beamBoids.Count == 0) {
            return;
        }

        averageBeamFear = fearSum / beamBoids.Count;

        if (beamBoids.Count < settings.panicMinBoidsInBeam) {
            return;
        }
        if (averageBeamFear < settings.panicFearThreshold) {
            return;
        }

        // Demasiada presion: todos los boids del haz salen corriendo.
        Vector3 sharedDirection = RandomFlatDirection ();
        for (int i = 0; i < beamBoids.Count; i++) {
            Vector3 direction = settings.panicSharedDirection ? sharedDirection : RandomFlatDirection ();
            float variation = Random.Range (-settings.panicDurationVariation, settings.panicDurationVariation);
            beamBoids[i].EnterPanic (direction, settings.panicDuration * (1f + variation));
        }
    }

    static Vector3 RandomFlatDirection ()
    {
        float angle = Random.Range (0f, Mathf.PI * 2f);
        return new Vector3 (Mathf.Sin (angle), 0f, Mathf.Cos (angle));
    }
    // ------------------------------------------------------------------------------

    public struct BoidData {
        public Vector3 position;
        public Vector3 direction;

        public Vector3 flockHeading;
        public Vector3 flockCentre;
        public Vector3 avoidanceHeading;
        public int numFlockmates;

        public static int Size {
            get {
                return sizeof (float) * 3 * 5 + sizeof (int);
            }
        }
    }
}