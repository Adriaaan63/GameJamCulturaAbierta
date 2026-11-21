// Based on SebLague/Boids (MIT License, Copyright (c) 2019 Sebastian Lague).
// Original: https://github.com/SebLague/Boids
// Modified:
//  - movement is locked to a horizontal plane ("PLANE LOCK")
//  - hard obstacle collision: the real displacement is swept against obstacles and
//    the boid slides along surfaces instead of going through them ("HARD COLLISION")

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour {

    BoidSettings settings;

    // State
    [HideInInspector]
    public Vector3 position;
    [HideInInspector]
    public Vector3 forward;
    Vector3 velocity;

    // To update:
    Vector3 acceleration;
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

    // FLEE: set by BoidManager. Boids run away from this transform (e.g. the player).
    [HideInInspector]
    public Transform threat;
    float panic;                         // 0 = calm, 1 = maximum fear (decays slowly)
    float currentMaxSpeed;               // maxSpeed, boosted while panicking
    const float calmDownRate = 0.5f;     // panic lost per second once the threat is gone

    // PLANE LOCK: the world Y at which this boid lives. Taken from its spawn position.
    float planeY;

    // HARD COLLISION
    const float skin = 0.02f;            // gap kept between the boid and a surface
    const int maxSlideIterations = 3;    // how many times a single move can bounce/slide
    static readonly Collider[] overlapBuffer = new Collider[16];

    void Awake () {
        material = transform.GetComponentInChildren<MeshRenderer> ().material;
        cachedTransform = transform;
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

        currentMaxSpeed = settings.maxSpeed;

        float startSpeed = (settings.minSpeed + settings.maxSpeed) / 2;
        velocity = flatForward * startSpeed;
    }

    public void SetColour (Color col) {
        if (material != null) {
            material.color = col;
        }
    }

    public void UpdateBoid () {
        Vector3 acceleration = Vector3.zero;

        // FLEE: how scared am I, and which way is "away"? (horizontal distance only)
        Vector3 fleeDir = Vector3.zero;
        float urgency = 0f;
        if (threat != null && settings.fleeRadius > 0f) {
            Vector3 away = position - threat.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < settings.fleeRadius) {
                urgency = 1f - dist / settings.fleeRadius;   // 0 at the edge, 1 on top of it
                fleeDir = dist > 0.0001f ? away / dist : forward;
            }
        }
        // Panic rises instantly but fades slowly, so boids don't snap back to normal speed.
        panic = Mathf.Max (urgency, panic - calmDownRate * Time.deltaTime);
        currentMaxSpeed = settings.maxSpeed * (1f + settings.fleeSpeedBoost * panic);

        if (target != null) {
            Vector3 offsetToTarget = (target.position - position);
            acceleration = SteerTowards (offsetToTarget) * settings.targetWeight;
        }

        if (numPerceivedFlockmates != 0) {
            centreOfFlockmates /= numPerceivedFlockmates;

            Vector3 offsetToFlockmatesCentre = (centreOfFlockmates - position);

            var alignmentForce = SteerTowards (avgFlockHeading) * settings.alignWeight;
            var cohesionForce = SteerTowards (offsetToFlockmatesCentre) * settings.cohesionWeight;
            var seperationForce = SteerTowards (avgAvoidanceHeading) * settings.seperateWeight;

            acceleration += alignmentForce;
            acceleration += cohesionForce;
            acceleration += seperationForce;
        }

        // FLEE: push away from the threat, stronger the closer it is.
        if (urgency > 0f) {
            acceleration += SteerTowards (fleeDir) * settings.fleeWeight * urgency;
        }

        if (IsHeadingForCollision ()) {
            Vector3 collisionAvoidDir = ObstacleRays ();
            Vector3 collisionAvoidForce = SteerTowards (collisionAvoidDir) * settings.avoidCollisionWeight;
            acceleration += collisionAvoidForce;
        }

        // PLANE LOCK (1/3): no force may push the boid up or down.
        acceleration.y = 0f;

        velocity += acceleration * Time.deltaTime;

        // PLANE LOCK (2/3): velocity stays horizontal.
        velocity.y = 0f;

        float speed = velocity.magnitude;
        Vector3 dir = speed > 0.0001f ? velocity / speed : forward;
        speed = Mathf.Clamp (speed, settings.minSpeed, currentMaxSpeed);
        velocity = dir * speed;

        // HARD COLLISION: move through the obstacle-aware routine instead of
        // simply adding velocity * dt to the position.
        Vector3 startPos = PushOutOfObstacles (cachedTransform.position);
        bool collided;
        Vector3 newPos = MoveWithCollisions (startPos, velocity * Time.deltaTime, out collided);

        if (collided) {
            // velocity was modified to slide along the surface; refresh the heading.
            float s = velocity.magnitude;
            if (s > 0.0001f) {
                dir = velocity / s;
            }
        }

        // PLANE LOCK (3/3): pin the height, in case anything else nudged the transform.
        newPos.y = planeY;
        cachedTransform.position = newPos;

        cachedTransform.forward = dir;
        position = cachedTransform.position;
        forward = dir;
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