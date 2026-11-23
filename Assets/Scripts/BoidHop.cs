using UnityEngine;

/// <summary>
/// Purely cosmetic: makes the boid's VISUAL hop and squash/stretch while it moves.
/// It never touches the boid's root transform (Boid.cs owns that: plane lock, collisions...).
///
/// Setup: put this on the boid prefab root (next to Boid) and assign "Visual".
/// "Visual" must be a child of the root, NOT rotated (its local Y must be world up) and
/// with the mesh below it. If your capsule is rotated/lying down, make an empty child
/// "Visual" with identity rotation and put the capsule inside it.
///
/// Hop rhythm follows the real speed: one hop every "hopDistance" metres travelled, so
/// idle strolls are slow little hops, fleeing is brisk, panic is frantic, and a boid that
/// stops finishes its current hop and rests on the ground.
/// </summary>
public class BoidHop : MonoBehaviour {

    [Header ("References")]
    [Tooltip ("Child that moves and deforms. Must be unrotated relative to the boid root.")]
    [SerializeField] private Transform visual;

    [SerializeField] private Boid boid;

    [Header ("Hop")]
    [Tooltip ("Height of a hop at 'referenceSpeed'.")]
    [SerializeField] private float hopHeight = 0.3f;
    [Tooltip ("Metres travelled per hop. Bigger = lazier hopping.")]
    [SerializeField] private float hopDistance = 2f;
    [Tooltip ("Speed at which the hop height equals 'hopHeight'. Slower boids hop lower, faster ones higher (limited to 0.4x - 1.5x).")]
    [SerializeField] private float referenceSpeed = 5f;
    [Tooltip ("Below this speed the boid is considered stopped.")]
    [SerializeField] private float minSpeedToHop = 0.15f;
    [Tooltip ("When it stops mid-air, how fast it finishes the hop and lands (hops per second).")]
    [SerializeField] private float settleHopsPerSecond = 4f;
    [Range (0f, 0.5f)]
    [Tooltip ("Random +/- variation of hop distance and height per boid, so the herd doesn't hop in unison.")]
    [SerializeField] private float randomVariation = 0.15f;
    [Tooltip ("Extra hop frequency at maximum fear. 1.5 = hops 2.5x as often when terrified.")]
    [SerializeField] private float fearHopBoost = 1.5f;

    [Header ("Squash & stretch")]
    [SerializeField] private bool squashAndStretch = true;
    [Tooltip ("How much it stretches vertically while flying (0.2 = +20%).")]
    [SerializeField] private float stretchAmount = 0.2f;
    [Tooltip ("How much it squashes when touching the ground (0.25 = -25%).")]
    [SerializeField] private float squashAmount = 0.25f;
    [Range (0.02f, 0.5f)]
    [Tooltip ("Fraction of a hop, around each ground contact, during which it is squashed.")]
    [SerializeField] private float squashWindow = 0.15f;
    [SerializeField] private float scaleSmoothSpeed = 20f;

    private Vector3 baseLocalPos;
    private Vector3 baseLocalScale;
    private bool baseStored;
    
    private float pivotToBottom;     // distance from the visual's pivot to its lowest point
    private float distanceFactor = 1f;
    private float heightFactor = 1f;

    private float phase;             // 0..1: 0 = on the ground, 0.5 = top of the hop
    private float heightScale = 1f;  // smoothed speed-based height multiplier
    private float currentScaleY = 1f;

    private Vector3 lastPos;
    private bool initialized;

    private void Awake ()
    {
        if (visual == null) {
            Renderer r = GetComponentInChildren<Renderer> ();
            if (r != null) {
                visual = r.transform;
            }
        }

        if (boid == null)
        {
            boid = this.transform.parent.GetComponent<Boid>();
        }
        
        if (visual == null || visual == transform) {
            Debug.LogWarning ("BoidHop: assign a child 'Visual' (not the boid root: Boid.cs pins the root's height).", this);
            enabled = false;
            return;
        }

        baseLocalPos = visual.localPosition;
        baseLocalScale = visual.localScale;
        baseStored = true;

        // Distance from the pivot to the lowest point of the mesh, so squashing keeps the feet on the ground.
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer> ();
        if (renderers.Length > 0) {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                b.Encapsulate (renderers[i].bounds);
            }
            pivotToBottom = Mathf.Max (0f, visual.position.y - b.min.y);
        }

        distanceFactor = 1f + Random.Range (-randomVariation, randomVariation);
        heightFactor = 1f + Random.Range (-randomVariation, randomVariation);
    }

    private void OnDisable ()
    {
        if (baseStored && visual != null) {
            visual.localPosition = baseLocalPos;
            visual.localScale = baseLocalScale;
        }
    }

    // LateUpdate: by now BoidManager has already moved the boid this frame.
    private void LateUpdate ()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos = transform.position;
        if (!initialized) {
            lastPos = pos;
            initialized = true;
            return;
        }

        // Real horizontal speed, measured from how far the boid actually moved.
        Vector3 delta = pos - lastPos;
        delta.y = 0f;
        lastPos = pos;
        float speed = delta.magnitude / dt;

        // --- phase of the hop ---
        bool moving = speed > minSpeedToHop;
        if (moving) {
            float fearFactor = 1f + fearHopBoost * (boid != null ? boid.Fear : 0f);
            phase += speed * dt * fearFactor / Mathf.Max (0.05f, hopDistance * distanceFactor); //hop speed doubles for maximum fear
            phase %= 1f;

            float targetHeightScale = Mathf.Clamp (speed / Mathf.Max (0.01f, referenceSpeed), 0.4f, 1.5f);
            heightScale = Mathf.Lerp (heightScale, targetHeightScale, 1f - Mathf.Exp (-6f * dt));
        } else if (phase > 0f) {
            // Stopped mid-hop: keep going until it lands, then rest.
            phase += settleHopsPerSecond * dt;
            if (phase >= 1f) {
                phase = 0f;
            }
        }

        bool hopping = moving || phase > 0f;

        // --- height: parabola, 0 on the ground, hopHeight at the top ---
        float hop = hopping ? 4f * phase * (1f - phase) * hopHeight * heightFactor * heightScale : 0f;

        // --- squash & stretch ---
        float targetScaleY = 1f;
        if (squashAndStretch && hopping) {
            // Squashed around each ground contact (landing and take-off).
            float distToGround = Mathf.Min (phase, 1f - phase);
            float contact = Mathf.Clamp01 (1f - distToGround / squashWindow);
            contact = contact * contact * (3f - 2f * contact);

            // Stretched while flying, most when moving fastest vertically (near the ground), none at the top.
            float verticalSpeed = Mathf.Abs (1f - 2f * phase);

            targetScaleY = 1f + stretchAmount * verticalSpeed * (1f - contact) - squashAmount * contact;
        }
        currentScaleY = Mathf.Lerp (currentScaleY, targetScaleY, 1f - Mathf.Exp (-scaleSmoothSpeed * dt));

        // Keep the volume roughly constant: taller = thinner.
        float scaleXZ = 1f / Mathf.Sqrt (Mathf.Max (0.2f, currentScaleY));

        visual.localScale = new Vector3 (
            baseLocalScale.x * scaleXZ,
            baseLocalScale.y * currentScaleY,
            baseLocalScale.z * scaleXZ);

        // Scaling happens around the pivot: compensate so the bottom stays on the ground.
        visual.localPosition = baseLocalPos + Vector3.up * (hop + pivotToBottom * (currentScaleY - 1f));
    }
}