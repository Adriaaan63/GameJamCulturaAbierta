using UnityEngine;

/// <summary>
/// A flat (XZ plane) cone with its apex on this object, aimed along this object's forward.
///  - Boids inside the cone are herded along the aim direction (see Boid.TryGetBeam).
///  - The two side edges act as moving, semi-permeable walls: they resist being crossed
///    and carry boids along when the cone sweeps (the croupier's rake).
///
/// Put this on the object whose forward is the aim direction (the player, or a child that
/// rotates with the flashlight). Its Update only keeps the optional spot light in sync; BoidManager calls Refresh()
/// once per frame so that "previous" and "current" cone positions are always consistent.
/// </summary>
public class Flashlight : MonoBehaviour {

    [Header("Dependencies")]
    [SerializeField] private BoidManager boidManager;
    
    [Header ("Beam")]
    public bool active = true;
    public float range = 12f;
    [Range (5f, 85f)]
    public float halfAngle = 30f;

    [Header ("Herding")]
    [Tooltip ("How strongly boids inside the beam are pushed along the aim direction.")]
    public float herdWeight = 6f;

    [Header ("Edges")]
    [Range (0f, 1f)]
    [Tooltip ("0 = solid walls. 1 = edges don't exist. In between: the chance that a boid touching an edge slips through.")]
    public float permeability = 0.1f;
    [Tooltip ("Seconds a boid that decided to slip through ignores the edges.")]
    public float slipTime = 0.6f;

    [Header ("Crowd pressure")]
    [Tooltip ("Neighbours (within the perception radius) from which a boid pressed against an edge starts to leak through.")]
    public int crowdStart = 6;
    [Tooltip ("Neighbours at which the leak is at its maximum.")]
    public int crowdFull = 15;
    [Tooltip ("Maximum leak: chance per second, for a pressed boid in a full crowd, of being squeezed through the edge.")]
    public float leakRate = 2f;

    [Header ("Light (optional)")]
    [Tooltip ("A Spot Light, as a CHILD of this object (so it keeps its own downward tilt). Its angle, range and on/off are driven from here.")]
    public Light spotLight;
    [Tooltip ("Multiplier on the light's cone angle. A tilted spot light lights an ellipse, not a triangle: tune by eye against the yellow gizmo.")]
    public float lightAngleScale = 1f;
    [Range (0.1f, 1f)]
    [Tooltip ("Inner/outer cone angle ratio. Closer to 1 = sharper edge, which matches the invisible wall better.")]
    public float innerAngleRatio = 0.85f;
    [Tooltip ("World Y of the floor, used so the light reaches 'range' along the ground and not along its own axis.")]
    public float lightGroundY = 0f;

    [Header ("Debug")]
    [Tooltip ("How far above this object the yellow cone gizmo is drawn, so the floor doesn't hide it.")]
    public float gizmoLift = 0.3f;

    // Per-boid memory of its relationship with the edges. Owned by the Boid, updated here.
    public struct EdgeState {
        public bool inContact;
        public float contactAge;
        public float slipTimer;
    }

    const float contactMemory = 0.25f;   // no contact for this long = a new contact is a new roll

    Vector3 apexNow, apexPrev;
    Vector3 dirNow, dirPrev;
    float dt = 0.016f;
    bool hasState;
    bool wasActive;
    
    public int BoidsInBeam { get; set; }

    // Call once per frame, before the boids are updated.
    public void Refresh () {
        Vector3 a = transform.position;
        a.y = 0f;

        Vector3 d = transform.forward;
        d.y = 0f;
        if (d.sqrMagnitude < 0.000001f) {
            d = hasState ? dirNow : Vector3.forward;
        }
        d.Normalize ();

        if (!hasState || !wasActive) {
            // Just (re)started: no sweep this frame, otherwise boids would be hit by a phantom move.
            apexPrev = a;
            dirPrev = d;
        } else {
            apexPrev = apexNow;
            dirPrev = dirNow;
        }
        apexNow = a;
        dirNow = d;

        dt = Mathf.Max (Time.deltaTime, 0.0001f);
        wasActive = active;
        hasState = true;
        
        Debug.Log(BoidsInBeam);
    }

    // Is this position inside the beam? If so, returns the aim direction and an
    // intensity in [0.5, 1] (1 next to the player, 0.5 at the far end).
    public bool TryGetBeam (Vector3 p, out Vector3 aimDir, out float intensity) {
        aimDir = dirNow;
        intensity = 0f;
        if (!active || !hasState) {
            return false;
        }

        Vector3 v = p - apexNow;
        v.y = 0f;
        float dist = v.magnitude;
        if (dist > range || dist < 0.0001f) {
            return false;
        }

        float cos = Vector3.Dot (v / dist, dirNow);
        if (cos < Mathf.Cos (halfAngle * Mathf.Deg2Rad)) {
            return false;
        }

        intensity = 1f - 0.5f * (dist / range);
        return true;
    }

    // Moves a boid from p0 to p1 (its intended end position) against the two edges.
    // Each edge is a segment from the apex of length "range", moving between its previous
    // and current placement. If the boid would end up on the other side of an edge (or
    // closer than "radius" to it), it is kept on its own side, and its velocity is adjusted
    // so it follows the edge when the edge advances. Returns the corrected end position.
    public Vector3 ResolveEdges (Vector3 p0, Vector3 p1, float radius, int neighbours, ref EdgeState st, ref Vector3 velocity) {
        st.contactAge += Time.deltaTime;
        if (st.contactAge > contactMemory) {
            st.inContact = false;
        }
        if (st.slipTimer > 0f) {
            st.slipTimer -= Time.deltaTime;
        }

        if (!active || !hasState || st.slipTimer > 0f) {
            return p1;
        }

        for (int side = -1; side <= 1; side += 2) {
            Vector3 ePrev = EdgeDir (dirPrev, side);
            Vector3 eNow = EdgeDir (dirNow, side);

            float d0 = Side (p0, apexPrev, ePrev);
            if (Mathf.Abs (d0) < 0.00001f) {
                continue;   // started exactly on the line: no way to know its side
            }
            float sign = Mathf.Sign (d0);

            // Distance to the edge's current line, measured on the boid's ORIGINAL side.
            float d1 = Side (p1, apexNow, eNow) * sign;
            if (d1 >= radius) {
                continue;   // still comfortably on its own side
            }

            Vector3 v = p1 - apexNow;
            float u = eNow.x * v.x + eNow.z * v.z;   // position along the edge
            if (u < 0f || u > range) {
                continue;   // beside the apex or past the open far end: no wall there
            }

            // Contact. On the first frame of a contact, roll the dice for slipping through.
            if (!st.inContact) {
                st.inContact = true;
                if (Random.value < permeability) {
                    st.slipTimer = slipTime;
                    return p1;
                }
            }
            st.contactAge = 0f;

            // Crowd pressure: a boid pressed against the edge in a dense crowd may be squeezed
            // through. This is a rate (per second), so the pile drains gradually and stops
            // leaking once the crowd thins out below crowdStart.
            float crowd = Mathf.InverseLerp (crowdStart, crowdFull, neighbours);
            if (crowd > 0f && Random.value < leakRate * crowd * Time.deltaTime) {
                st.slipTimer = slipTime;
                return p1;
            }

            // Unit vector pointing from the edge towards the boid's side.
            Vector3 n = Normal (eNow) * sign;

            // Put the boid back on its side, "radius" away from the line.
            p1 += n * (radius - d1);

            // Follow the edge: velocity along n must be at least the edge's own speed along n
            // (and never negative, so the boid doesn't keep pushing into it).
            Vector3 edgeVel = ((apexNow + eNow * u) - (apexPrev + ePrev * u)) / dt;
            float wanted = Mathf.Max (Vector3.Dot (edgeVel, n), 0f);
            float vn = Vector3.Dot (velocity, n);
            if (vn < wanted) {
                velocity += n * (wanted - vn);
            }
        }

        return p1;
    }

    // --- geometry helpers (everything on the XZ plane) ---

    Vector3 EdgeDir (Vector3 dir, int side) {
        return Quaternion.AngleAxis (side * halfAngle, Vector3.up) * dir;
    }

    // Signed distance from p to the line through "apex" with unit direction e.
    static float Side (Vector3 p, Vector3 apex, Vector3 e) {
        return e.x * (p.z - apex.z) - e.z * (p.x - apex.x);
    }

    // Unit normal such that dot(Normal(e), p - apex) == Side(p, apex, e).
    static Vector3 Normal (Vector3 e) {
        return new Vector3 (-e.z, 0f, e.x);
    }

    // --- light sync (visual only, no effect on the boids) ---

    void Update () {
        SyncLight ();
        SyncLightColour ();
    }

    void OnValidate () {
        SyncLight ();   // so you can tune the light in the editor without pressing Play
    }

    void SyncLight () {
        if (spotLight == null) {
            return;
        }

        spotLight.type = LightType.Spot;
        spotLight.enabled = active;

        float outer = Mathf.Clamp (halfAngle * 2f * lightAngleScale, 1f, 179f);
        spotLight.spotAngle = outer;
        spotLight.innerSpotAngle = outer * innerAngleRatio;

        // A light's range is measured along its rays, not along the ground. To reach "range"
        // horizontally from a light that sits h above the floor, we need sqrt(range^2 + h^2).
        float h = Mathf.Max (0f, spotLight.transform.position.y - lightGroundY);
        spotLight.range = Mathf.Sqrt (range * range + h * h);
    }
    
    void SyncLightColour () {
        if (spotLight == null || boidManager == null || boidManager.settings == null) {
            return;
        }
        spotLight.color = Color.Lerp (
            boidManager.settings.calmColour,
            boidManager.settings.terrifiedColour,
            boidManager.AverageBeamFear);
    }

    void OnDrawGizmos () {
        Vector3 a = transform.position + Vector3.up * gizmoLift;
        Vector3 d = transform.forward;
        d.y = 0f;
        if (d.sqrMagnitude < 0.000001f) {
            return;
        }
        d.Normalize ();

        Gizmos.color = active ? new Color (1f, 0.9f, 0.2f) : Color.gray;
        Vector3 left = a + EdgeDir (d, -1) * range;
        Vector3 right = a + EdgeDir (d, 1) * range;
        Gizmos.DrawLine (a, left);
        Gizmos.DrawLine (a, right);

        const int segments = 16;
        Vector3 prev = left;
        for (int i = 1; i <= segments; i++) {
            float ang = Mathf.Lerp (-halfAngle, halfAngle, i / (float) segments);
            Vector3 p = a + Quaternion.AngleAxis (ang, Vector3.up) * d * range;
            Gizmos.DrawLine (prev, p);
            prev = p;
        }
    }
}