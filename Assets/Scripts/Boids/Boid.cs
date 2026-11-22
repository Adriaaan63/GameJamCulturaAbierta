// Based on SebLague/Boids (MIT License, Copyright (c) 2019 Sebastian Lague).
// Original: https://github.com/SebLague/Boids
// Modified:
//  - movement is locked to a horizontal plane ("PLANE LOCK")
//  - hard obstacle collision: the real displacement is swept against obstacles and
//    the boid slides along surfaces instead of going through them ("HARD COLLISION")
//  - per-boid state machine: Idle / Fleeing / Panic ("STATE MACHINE")
//  - fear accumulation, flashlight beam + edges, player flee circle

using UnityEngine;

public enum BoidState { Idle, Fleeing, Panic }

public class Boid : MonoBehaviour {

    BoidSettings settings;

    [Tooltip ("Per-boid random variation of how scared it is of the player (breaks the halo around him).")]
    public float dangerRange = 0.5f;

    // State
    [HideInInspector]
    public Vector3 position;
    [HideInInspector]
    public Vector3 forward;
    Vector3 velocity;

    // To update (filled by BoidManager from the compute shader):
    [HideInInspector]
    public Vector3 avgFlockHeading;
    [HideInInspector]
    public Vector3 avgAvoidanceHeading;
    [HideInInspector]
    public Vector3 centreOfFlockmates;
    [HideInInspector]
    public int numPerceivedFlockmates;

    // Cached
    Material material;
    Transform cachedTransform;
    Transform target;
    Color baseColour;
    bool colourApplied;

    // ---- STATE MACHINE ----
    public BoidState State { get; private set; }
    public float Fear { get; private set; }          // 0 = calm .. 1 = terrified
    public bool InBeam { get; private set; }         // inside the flashlight beam this frame
    public bool CanPanic { get { return State != BoidState.Panic && immunityTimer <= 0f; } }

    float calmTimer;            // Fleeing: time without threat
    float panicTimer;           // Panic: time left
    float immunityTimer;        // after panic: can't panic again until 0
    Vector3 panicDir;

    // Idle wandering
    bool idleWalking;
    float idleTimer;
    Vector3 idleHeading;

    // FLEE: set by BoidManager. Boids run away from this transform (e.g. the player).
    [HideInInspector]
    public Transform threat;
    float agitation;                     // 0..1, rises instantly and decays slowly: drives the speed boost while fleeing
    float currentMaxSpeed;               // desired / maximum speed for the current state
    const float calmDownRate = 0.5f;     // agitation lost per second once the threat is gone
    float dangerPerception;              // this boid's fixed fear offset, set in Awake

    // FLASHLIGHT: set by BoidManager.
    [HideInInspector]
    public Flashlight flashlight;
    Flashlight.EdgeState edgeState;

    // PLANE LOCK: the world Y at which this boid lives. Taken from its spawn position.
    float planeY;

    // HARD COLLISION
    const float skin = 0.02f;            // gap kept between the boid and a surface
    const int maxSlideIterations = 3;    // how many times a single move can bounce/slide
    static readonly Collider[] overlapBuffer = new Collider[16];

    void Awake () {
        material = transform.GetComponentInChildren<MeshRenderer> ().material;
        if (material != null) {
            baseColour = material.color;
        }
        cachedTransform = transform;
        dangerPerception = Random.Range (-dangerRange, dangerRange);
    }

    public void Initialize (BoidSettings settings, Transform target) {
        this.target = target;
        this.settings = settings;

        // PLANE LOCK: remember the height and start with a purely horizontal heading.
        planeY = cachedTransform.position.y;

        Vector3 flatForward = cachedTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) {
            flatForward = Vector3.forward;
        }
        flatForward.Normalize ();

        cachedTransform.rotation = Quaternion.LookRotation (flatForward, Vector3.up);

        position = cachedTransform.position;
        forward = flatForward;

        currentMaxSpeed = settings.idleSpeed;

        // Everyone starts idle, at a random point of the walk / stop cycle.
        State = BoidState.Idle;
        idleWalking = Random.value < 0.5f;
        idleHeading = flatForward;
        idleTimer = RandomIn (idleWalking ? settings.idleWalkTime : settings.idleStopTime);
        velocity = idleWalking ? flatForward * settings.idleSpeed : Vector3.zero;
    }

    public void SetColour (Color col) {
        if (material != null) {
            material.color = col;
        }
    }

    // Called by BoidManager when the flashlight has frightened the herd too much.
    public void EnterPanic (Vector3 direction, float duration) {
        if (!CanPanic) {
            return;
        }
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) {
            direction = forward;
        }
        panicDir = direction.normalized;
        panicTimer = duration;
        State = BoidState.Panic;
        Fear = 1f;
    }

    public void UpdateBoid () {
        float dt = Time.deltaTime;

        // ---- 1. SENSE ------------------------------------------------------------------

        // The circle around the player (horizontal distance only).
        Vector3 fleeDir = Vector3.zero;
        float urgency = 0f;
        if (threat != null && settings.fleeRadius > 0f) {
            Vector3 away = position - threat.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < settings.fleeRadius) {
                // Base: 0 at the radius edge, 1 on top of the player. The per-boid offset makes
                // each boid keep a slightly different distance, so no halo forms.
                urgency = 1f - dist / settings.fleeRadius + dangerPerception;
                fleeDir = dist > 0.0001f ? away / dist : forward;
            }
        }

        // The flashlight beam.
        Vector3 beamDir = Vector3.zero;
        float beamIntensity = 0f;
        InBeam = flashlight != null && flashlight.TryGetBeam (position, out beamDir, out beamIntensity);

        // One number for "how threatened am I right now", 0..1.
        float threatLevel = Mathf.Max (Mathf.Clamp01 (urgency), beamIntensity);
        if (edgeState.inContact) {
            threatLevel = Mathf.Max (threatLevel, 0.5f);   // being pushed by a beam edge is scary too
        }

        // ---- 2. FEAR, TIMERS, STATE TRANSITIONS -----------------------------------------

        if (immunityTimer > 0f) {
            immunityTimer -= dt;
        }

        if (State == BoidState.Panic) {
            Fear = 1f;
        } else if (threatLevel > 0f) {
            Fear = Mathf.Min (1f, Fear + threatLevel * settings.fearRiseRate * dt);
        } else {
            Fear = Mathf.Max (0f, Fear - settings.fearFallRate * dt);
        }

        // Short-term agitation: rises instantly, decays slowly. Drives the speed boost while fleeing.
        agitation = Mathf.Max (Mathf.Max (urgency, beamIntensity), agitation - calmDownRate * dt);

        switch (State) {
            case BoidState.Idle:
                if (threatLevel > 0f) {
                    State = BoidState.Fleeing;
                    calmTimer = 0f;
                }
                break;

            case BoidState.Fleeing:
                if (threatLevel > 0f) {
                    calmTimer = 0f;
                } else {
                    calmTimer += dt;
                    if (calmTimer >= settings.calmDownTime) {
                        EnterIdle ();
                    }
                }
                break;

            case BoidState.Panic:
                panicTimer -= dt;
                if (panicTimer <= 0f) {
                    ExitPanic (threatLevel > 0f);
                }
                break;
        }

        // ---- 3. FORCES (depend on the state) --------------------------------------------

        Vector3 acceleration = Vector3.zero;

        Vector3 offsetToCentre = Vector3.zero;
        if (numPerceivedFlockmates != 0) {
            centreOfFlockmates /= numPerceivedFlockmates;
            offsetToCentre = centreOfFlockmates - position;
        }

        float minSpeedNow;
        switch (State) {
            case BoidState.Idle:
                currentMaxSpeed = settings.idleSpeed;
                minSpeedNow = 0f;   // idle boids can stand still
                acceleration = IdleForces (offsetToCentre, dt);
                break;

            case BoidState.Panic:
                // Ignores the player, the beam and the herd. Only runs (obstacles are avoided below).
                currentMaxSpeed = settings.panicSpeed;
                minSpeedNow = settings.minSpeed;
                acceleration = SteerTowards (panicDir) * settings.panicSteerWeight;
                break;

            default: // Fleeing
                currentMaxSpeed = settings.maxSpeed * (Random.Range (-0.2f, 0.2f) + 1f + settings.fleeSpeedBoost * agitation);
                minSpeedNow = settings.minSpeed;
                acceleration = FleeingForces (offsetToCentre, fleeDir, urgency, beamDir, beamIntensity);
                break;
        }

        // Obstacle avoidance applies in every state.
        if (IsHeadingForCollision ()) {
            Vector3 collisionAvoidDir = ObstacleRays ();
            Vector3 collisionAvoidForce = SteerTowards (collisionAvoidDir) * settings.avoidCollisionWeight;
            acceleration += collisionAvoidForce;
        }

        // ---- 4. MOVE --------------------------------------------------------------------

        // PLANE LOCK (1/3): no force may push the boid up or down.
        acceleration.y = 0f;

        velocity += acceleration * dt;

        // PLANE LOCK (2/3): velocity stays horizontal.
        velocity.y = 0f;

        float speed = velocity.magnitude;
        Vector3 dir = speed > 0.0001f ? velocity / speed : forward;
        speed = Mathf.Clamp (speed, minSpeedNow, currentMaxSpeed);
        velocity = dir * speed;

        Vector3 startPos = PushOutOfObstacles (cachedTransform.position);
        Vector3 desiredPos = startPos + velocity * dt;

        // The beam's edges are moving, semi-permeable walls. Panicking boids ignore them.
        if (flashlight != null && State != BoidState.Panic) {
            desiredPos = flashlight.ResolveEdges (startPos, desiredPos, settings.boundsRadius, numPerceivedFlockmates, ref edgeState, ref velocity);
        }

        // HARD COLLISION: obstacles have the last word.
        bool collided;
        Vector3 newPos = MoveWithCollisions (startPos, desiredPos - startPos, out collided);

        // Velocity may have been changed by the flashlight edges or by sliding along a
        // surface; refresh the heading.
        float s = velocity.magnitude;
        if (s > 0.0001f) {
            dir = velocity / s;
        }

        // A panicking boid that hit a wall keeps running along it instead of grinding into it.
        if (collided && State == BoidState.Panic && s > 0.0001f) {
            panicDir = dir;
        }

        // PLANE LOCK (3/3): pin the height, in case anything else nudged the transform.
        newPos.y = planeY;
        cachedTransform.position = newPos;

        cachedTransform.forward = dir;
        position = cachedTransform.position;
        forward = dir;

        UpdateDebugColour ();
    }

    // STATE MACHINE ---------------------------------------------------------------------

    void EnterIdle () {
        State = BoidState.Idle;
        calmTimer = 0f;
        idleWalking = false;   // rest first
        idleTimer = RandomIn (settings.idleStopTime);
    }

    void ExitPanic (bool stillThreatened) {
        immunityTimer = settings.panicImmunityTime;
        Fear = 0f;
        agitation = 0f;
        calmTimer = 0f;
        edgeState = default (Flashlight.EdgeState);

        if (stillThreatened) {
            State = BoidState.Fleeing;
        } else {
            EnterIdle ();
        }
    }

    // Walk a bit, stop, walk again, without any destination.
    Vector3 IdleForces (Vector3 offsetToCentre, float dt) {
        Vector3 force = Vector3.zero;

        idleTimer -= dt;
        if (idleTimer <= 0f) {
            idleWalking = !idleWalking;
            if (idleWalking) {
                // New heading: turn up to 120 degrees from where we are facing.
                idleHeading = Quaternion.AngleAxis (Random.Range (-120f, 120f), Vector3.up) * forward;
                idleTimer = RandomIn (settings.idleWalkTime);
            } else {
                idleTimer = RandomIn (settings.idleStopTime);
            }
        }

        if (idleWalking) {
            force += SteerTowards (idleHeading);
            if (numPerceivedFlockmates != 0) {
                // Weak pull towards the neighbours so the herd doesn't dissolve while idle.
                force += SteerTowards (offsetToCentre) * settings.idleCohesionWeight;
            }
        } else {
            // Come to a stop.
            force += Vector3.ClampMagnitude (-velocity * 5f, settings.maxSteerForce);
        }

        // Personal space, only if someone is actually too close.
        if (avgAvoidanceHeading.sqrMagnitude > 0.0001f) {
            force += SteerTowards (avgAvoidanceHeading) * settings.seperateWeight;
        }

        return force;
    }

    // The herd behaviour: flocking + flee from the player + herded by the beam.
    Vector3 FleeingForces (Vector3 offsetToCentre, Vector3 fleeDir, float urgency, Vector3 beamDir, float beamIntensity) {
        Vector3 force = Vector3.zero;

        if (target != null) {
            Vector3 offsetToTarget = (target.position - position);
            force += SteerTowards (offsetToTarget) * settings.targetWeight;
        }

        if (numPerceivedFlockmates != 0) {
            force += SteerTowards (avgFlockHeading) * settings.alignWeight;
            force += SteerTowards (offsetToCentre) * settings.cohesionWeight;
            force += SteerTowards (avgAvoidanceHeading) * settings.seperateWeight;
        }

        // Push away from the player, stronger the closer he is.
        if (urgency > 0f) {
            force += SteerTowards (fleeDir) * settings.fleeWeight * urgency;
        }

        // Inside the beam, go where the beam points (not sideways out of it).
        if (beamIntensity > 0f) {
            force += SteerTowards (beamDir) * flashlight.herdWeight * beamIntensity;
        }

        return force;
    }

    static float RandomIn (Vector2 range) {
        return Random.Range (Mathf.Min (range.x, range.y), Mathf.Max (range.x, range.y));
    }

    // DEBUG COLOUR ----------------------------------------------------------------------

    void UpdateDebugColour () {
        if (material == null) {
            return;
        }

        if (settings.debugFearColour) {
            material.color = Color.Lerp (settings.calmColour, settings.terrifiedColour, Fear);
            colourApplied = true;
        } else if (colourApplied) {
            material.color = baseColour;   // switched off at runtime: restore the original colour
            colourApplied = false;
        }
    }

    // HARD COLLISION ------------------------------------------------------------------

    // Sweeps the boid's sphere along "move". If something is in the way, stops at the
    // surface (leaving a small skin gap) and keeps only the part of the motion that runs
    // along the surface. Because it sweeps the whole displacement, it cannot tunnel
    // through thin walls, however large the step is.
    Vector3 MoveWithCollisions (Vector3 startPos, Vector3 move, out bool collided) {
        collided = false;
        Vector3 pos = startPos;
        Vector3 remaining = move;
        Vector3 lastNormal = Vector3.zero;

        for (int i = 0; i < maxSlideIterations; i++) {
            float dist = remaining.magnitude;
            if (dist < 0.0001f) {
                break;
            }
            Vector3 dir = remaining / dist;

            RaycastHit hit;
            bool blocked = Physics.SphereCast (pos, settings.boundsRadius, dir, out hit,
                dist + skin, settings.obstacleMask, QueryTriggerInteraction.Ignore);

            if (!blocked) {
                pos += remaining;
                break;
            }

            collided = true;

            // Advance up to the surface, keeping a small gap.
            float travel = Mathf.Max (0f, hit.distance - skin);
            pos += dir * travel;

            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.000001f) {
                // Hit a floor/ceiling-like surface: nothing to slide along, stay put.
                break;
            }
            normal.Normalize ();
            lastNormal = normal;

            // Keep only the leftover motion that runs along the surface.
            float leftover = Mathf.Max (0f, dist - travel);
            remaining = Vector3.ProjectOnPlane (dir * leftover, normal);
        }

        if (collided && lastNormal != Vector3.zero) {
            // Remove the velocity component that points into the surface.
            Vector3 slid = Vector3.ProjectOnPlane (velocity, lastNormal);
            slid.y = 0f;

            if (slid.sqrMagnitude < 0.000001f) {
                // Head-on hit: pick a sideways direction along the surface.
                Vector3 tangent = Vector3.Cross (Vector3.up, lastNormal);
                if (Vector3.Dot (tangent, forward) < 0f) {
                    tangent = -tangent;
                }
                slid = tangent * settings.minSpeed;
            }
            velocity = slid;
        }

        return pos;
    }

    // Safety net: if the boid somehow ends up overlapping an obstacle (spawned inside the
    // skin of a wall, an obstacle moved onto it, floating point error...), push it out.
    // Note: Collider.ClosestPoint works for Box/Sphere/Capsule/convex Mesh colliders,
    // and cannot resolve the case where the boid's centre is inside the collider.
    Vector3 PushOutOfObstacles (Vector3 pos) {
        int count = Physics.OverlapSphereNonAlloc (pos, settings.boundsRadius, overlapBuffer,
            settings.obstacleMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++) {
            Vector3 closest = overlapBuffer[i].ClosestPoint (pos);
            Vector3 delta = pos - closest;
            delta.y = 0f;
            float d = delta.magnitude;
            if (d > 0.00001f && d < settings.boundsRadius) {
                pos += (delta / d) * (settings.boundsRadius - d + skin);
            }
        }
        return pos;
    }

    // ---------------------------------------------------------------------------------

    bool IsHeadingForCollision () {
        RaycastHit hit;
        if (Physics.SphereCast (position, settings.boundsRadius, forward, out hit, settings.collisionAvoidDst, settings.obstacleMask)) {
            return true;
        } else { }
        return false;
    }

    Vector3 ObstacleRays () {
        Vector3[] rayDirections = BoidHelper.directions;

        for (int i = 0; i < rayDirections.Length; i++) {
            Vector3 dir = cachedTransform.TransformDirection (rayDirections[i]);
            Ray ray = new Ray (position, dir);
            if (!Physics.SphereCast (ray, settings.boundsRadius, settings.collisionAvoidDst, settings.obstacleMask)) {
                return dir;
            }
        }

        return forward;
    }

    Vector3 SteerTowards (Vector3 vector) {
        Vector3 v = vector.normalized * currentMaxSpeed - velocity;
        return Vector3.ClampMagnitude (v, settings.maxSteerForce);
    }

}
