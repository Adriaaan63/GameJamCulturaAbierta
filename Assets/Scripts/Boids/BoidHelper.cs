// Adapted from SebLague/Boids (MIT License, Copyright (c) 2019 Sebastian Lague).
// Original: https://github.com/SebLague/Boids
// Include the full MIT license text somewhere in your project (e.g. THIRD_PARTY_LICENSES.txt).

using UnityEngine;

/// <summary>
/// Pre-computed set of directions, in the boid's LOCAL space, used to look for a free
/// path when an obstacle is ahead. All directions lie on the XZ plane (Y = 0), so
/// avoidance never pushes a boid up or down.
///
/// Order matters: ObstacleRays returns the first free direction, so the list goes from
/// "least turn" to "most turn", alternating right and left:
///   +step, -step, +2*step, -2*step, ... up to maxAngle.
/// The straight-ahead direction is NOT included, because IsHeadingForCollision has
/// already tested it before ObstacleRays is called.
/// </summary>
public static class BoidHelper {

    // Angle between consecutive directions, in degrees. Smaller = finer, but more casts.
    const float angleStepDegrees = 10f;

    // How far to each side we search, in degrees (180 would mean straight behind).
    const float maxAngleDegrees = 170f;

    public static readonly Vector3[] directions;

    static BoidHelper () {
        int stepsPerSide = Mathf.FloorToInt (maxAngleDegrees / angleStepDegrees);
        directions = new Vector3[stepsPerSide * 2];

        int index = 0;
        for (int k = 1; k <= stepsPerSide; k++) {
            float angle = k * angleStepDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin (angle);
            float cos = Mathf.Cos (angle);

            // Local forward is +Z, local right is +X.
            directions[index++] = new Vector3 (sin, 0f, cos);   // turn right
            directions[index++] = new Vector3 (-sin, 0f, cos);  // turn left
        }
    }
}