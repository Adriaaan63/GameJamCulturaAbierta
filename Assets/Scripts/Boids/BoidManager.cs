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
    private Stack<Boid> inactivedPool= new Stack<Boid>();
    private List<Furgoneta> suctionZones = new List<Furgoneta>();

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
            inactivedPool.Push(b);
        }
    }

    public void RegisterSuctionZone(Furgoneta zone)
    {

    }

    void Start () {
        //Por si existe algun boid en la escena puesto a mano
        Boid[] initialBoids = FindObjectsByType<Boid>(FindObjectsSortMode.None);
        foreach (Boid b in initialBoids)
        {
            RegisterBoid(b);
        }
    }

    //Metodo para que Spwnaer pueda registrar cada boid al instanciarlo
    public void RegisterBoid(Boid b)
    {
        if (b == null || boids.Contains(b)) return;

        b.Initialize(settings, null);
        b.threat = player;
        b.flashlight = flashlight;
        boids.Add(b);
        //Debug.Log("Boid registrado. Total en lista: " + boids.Count);
    }

    void Update()
    {
        if (flashlight != null) flashlight.Refresh();

        //Limpiza de boids destruidos
        boids.RemoveAll(b => b == null);

        int numBoids = boids.Count;

        // OJO: Un ComputeBuffer de tamaño 0 lanza una excepción en Unity. Si no hay enemigos, salimos.
        if (numBoids == 0)
        {
            ResetStats(); // NUEVO: que los contadores no se queden congelados
            return;
        }

        var boidData = new BoidData[numBoids];

        for (int i = 0; i < numBoids; i++)
        {
            boidData[i].position = boids[i].position;
            boidData[i].direction = boids[i].forward;
        }

        var boidBuffer = new ComputeBuffer(numBoids, BoidData.Size);
        boidBuffer.SetData(boidData);


        compute.SetBuffer(0, "boids", boidBuffer);
        compute.SetInt("numBoids", numBoids);
        compute.SetFloat("viewRadius", settings.perceptionRadius);
        compute.SetFloat("avoidRadius", settings.avoidanceRadius);

        int threadGroups = Mathf.CeilToInt(numBoids / (float)threadGroupSize);
        compute.Dispatch(0, threadGroups, 1, 1);

        boidBuffer.GetData(boidData);

        for (int i = 0; i < numBoids; i++)
        {
            boids[i].avgFlockHeading = boidData[i].flockHeading;
            boids[i].centreOfFlockmates = boidData[i].flockCentre;
            boids[i].avgAvoidanceHeading = boidData[i].avoidanceHeading;
            boids[i].numPerceivedFlockmates = boidData[i].numFlockmates;

            boids[i].UpdateBoid();
        }
        boidBuffer.Release();

        // NUEVO: el panico es una decision colectiva (depende de la media de varios boids),
        // asi que se toma aqui y no dentro de cada Boid.
        UpdateStatsAndPanic();
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
        ResetStats ();

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