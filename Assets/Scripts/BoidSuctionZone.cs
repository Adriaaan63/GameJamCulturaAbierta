using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BoidSuctionZone : MonoBehaviour
{
    [SerializeField] private BoidManager boidManager;
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private int capturedCount = 0;

    public float Radius => radius;
    public float CapturedCount => capturedCount;

    private void Awake()
    {
        if(boidManager == null)
        {
            boidManager = FindFirstObjectByType<BoidManager>();
        }
    }

    private void OnEnable()
    {
        if(boidManager != null)
        {
            boidManager.RegisterSuctionZone(this);
        }
    }

    private void OnDisable()
    {
        if(boidManager != null)
        {
            boidManager.UnregisterSuctionZone(this);
        }
    }

    public void OnBoidCaptured(Boid boid)
    {
        capturedCount++;

        //Aqui metemos particulas ,soniddso,etc
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
