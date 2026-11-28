using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class BoidSettings : ScriptableObject {
    // Settings
    public float minSpeed = 2;
    public float maxSpeed = 5;
    public float perceptionRadius = 2.5f;
    public float avoidanceRadius = 1;
    public float maxSteerForce = 3;

    public float alignWeight = 1;
    public float cohesionWeight = 1;
    public float seperateWeight = 1;

    public float targetWeight = 1;

    [Header ("Collisions")]
    public LayerMask obstacleMask;
    public float boundsRadius = .27f;
    public float avoidCollisionWeight = 10;
    public float collisionAvoidDst = 5;

    [Header ("Flee (circle around the player)")]
    public float fleeRadius = 8f;
    public float fleeWeight = 5f;
    public float fleeSpeedBoost = 0.5f;

    [Header ("State: Idle")]
    [Tooltip ("Walking speed while idle.")]
    public float idleSpeed = 1.2f;
    [Tooltip ("Min / max seconds of each walk.")]
    public Vector2 idleWalkTime = new Vector2 (2f, 5f);
    [Tooltip ("Min / max seconds of each stop.")]
    public Vector2 idleStopTime = new Vector2 (1.5f, 4f);
    [Tooltip ("How much idle boids drift towards their neighbours while walking. Keeps the herd from dissolving.")]
    public float idleCohesionWeight = 0.3f;

    [Header ("State: Fleeing / fear")]
    [Tooltip ("Fear gained per second at maximum threat (inside the beam next to the player). 0.35 = about 3 s to reach 1.")]
    public float baseFearRiseRate = 0.35f;
    [System.NonSerialized]
    public float fearRiseRate = 0.35f;
    [Tooltip ("Fear lost per second when nothing threatens the boid.")]
    public float fearFallRate = 0.25f;
    [Tooltip ("Seconds without any threat before a fleeing boid goes back to idle.")]
    public float calmDownTime = 3f;

    [Header ("State: Panic")]
    [Range (0f, 1f)]
    [Tooltip ("Panic starts when the AVERAGE fear of the boids inside the beam reaches this value.")]
    public float panicFearThreshold = 0.7f;
    [Tooltip ("Minimum number of boids in the beam for the average to count (so one scared boid can't trigger it).")]
    public int panicMinBoidsInBeam = 5;
    [Tooltip ("Seconds a boid runs in panic.")]
    public float panicDuration = 4f;
    [Range (0f, 1f)]
    [Tooltip ("Random +/- fraction applied to each boid's panic duration, so they don't all stop together.")]
    public float panicDurationVariation = 0.25f;
    public float panicSpeed = 9f;
    [Tooltip ("How hard a panicking boid steers towards its escape direction.")]
    public float panicSteerWeight = 5f;
    [Tooltip ("Seconds after panic during which a boid can't panic again.")]
    public float panicImmunityTime = 3f;
    [Tooltip ("Off: every boid runs in its own random direction (scatter). On: all boids of one outburst run the same way (stampede).")]
    public bool panicSharedDirection = false;

    [Header ("Debug colour")]
    [Tooltip ("Tints each boid from calm to terrified colour according to its fear.")]
    public bool debugFearColour = true;
    public Color calmColour = Color.white;
    public Color terrifiedColour = new Color (1f, 0f, 0f);   // #ff0000

}
