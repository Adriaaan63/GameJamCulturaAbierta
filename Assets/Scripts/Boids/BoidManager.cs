using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour {

    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader compute;
    public Transform player;
    public Flashlight flashlight;

    //Boid[] boids;
    private List<Boid> boids = new List<Boid>();
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
        if (numBoids == 0) return;

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
    }

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