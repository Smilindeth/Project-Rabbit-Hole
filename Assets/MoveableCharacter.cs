using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class MoveableCharacter : MonoBehaviour
{
    // Per tick value of summed forces to handle counter-acting forces to determine proper direction
    protected Vector3 Force = new Vector3();

    // Carried over value between frames
    public Vector3 Momentum = new Vector3();

    // Value used for determining how fast to fall, how much force is needed for being pushed, and how hard to push
    protected float Mass = 1f;

    protected float AccelerationMultiplier = 4f;

    protected float Acceleration => AccelerationMultiplier * 1f;
    protected float Decceleration => AccelerationMultiplier * 2f; // * DeccelerationModifier
    protected float DeccelerationModifier = 1f;

    // Max speed character can push themselves to move horizontally without outside assistance
    protected float MaxControlledHorizontalSpeed => AccelerationMultiplier * 4f;

    // Angles between -ClimbingDegrees and ClimbingDegrees can be traversed normally
    protected float ClimbingDegrees = 45; //31;
    // Angles Less than -FallingDegrees or greater than FallingDegrees cause object to slide down slope
    protected float FallingDegrees = 45; //59; Angles set to 45 to avoid the weird zones of "do nothing"

    // TODO: Optional air drag or constant air movement until landed

    // Flag for if physics and other events should be ran
    // Good for things such as a stack of boxes. Don't bother with gravity until external force is applied beyond gravity.
    protected bool IsActive = true;

    protected bool CanMoveZ = true;

    protected bool PlayerControlled = false;

    protected float ReducedInputFactor = 0.0f;
    public float ReducedInputDuration = 0f;

    // Use this for initialization
    protected virtual void Start()
    {
        if (tag.Contains("Player"))
            PlayerControlled = true;
    }

    protected virtual void Update()
    {
        DoUpdate(true);
    }

    // Update is called once per frame
    protected virtual void DoUpdate(bool doMove)
    {
#if DEBUG
        if (Input.GetKey(KeyCode.End))
        {
            print("Debug breakpoint for anything!");
        }
#endif
        if (PlayerControlled)
        {
#if DEBUG
            // Debugger, put breakpoint on the print line.
            // Handy for cases like "Stuck while holding left, but problem fixes itself the moment I let go"
            if (Input.GetKey(KeyCode.Pause))
            {
                print("Debug breakpoint for character only!");
            }
#endif
            // TODO: Force deceleration if going too fast

            var playerForce = new Vector3();
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                if(-Momentum.x < MaxControlledHorizontalSpeed)
                    playerForce.x -= Acceleration;
            }
            else if (Momentum.x < 0)
            {
                if (-Momentum.x < Decceleration)
                    playerForce.x = -Momentum.x;
                else
                    playerForce.x += Decceleration;
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                if(Momentum.x < MaxControlledHorizontalSpeed)
                    playerForce.x += Acceleration;
            }
            else if (Momentum.x > 0)
            {
                if (Momentum.x < Decceleration)
                    playerForce.x = -Momentum.x;
                else
                    playerForce.x -= Decceleration;
            }
            if (CanMoveZ)
            {
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    if (-Momentum.z < MaxControlledHorizontalSpeed)
                        playerForce.z -= Acceleration;
                }
                else if (Momentum.z < 0)
                {
                    if (-Momentum.z < Decceleration)
                        playerForce.z = -Momentum.z;
                    else
                        playerForce.z += Decceleration;
                }
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    if (Momentum.z < MaxControlledHorizontalSpeed)
                        playerForce.z += Acceleration;
                }
                else if (Momentum.z > 0)
                {
                    if (Momentum.z < Decceleration)
                        playerForce.z = -Momentum.z;
                    else
                        playerForce.z -= Decceleration;
                }
            }

            if(ReducedInputDuration > 0)
            {
                Force += playerForce * ReducedInputFactor;
                ReducedInputDuration -= Time.deltaTime;
            }
            else
            {
                Force += playerForce;
            }
        }

        if (doMove)
        {
            DoMove(null);
        }
    }

    protected Tuple<Vector3, float?> DoMove(HashSet<RaycastHit> collisions)
    {
        // Move character and prepare for next frame
        if (Force.magnitude != 0)
        {
            Momentum += Force;
            Force = new Vector3();
        }

        if(Momentum.magnitude != 0)
        {
            var dist = TryMoveDirection(Momentum, transform.position, Time.deltaTime, collisions);
            transform.localPosition += dist.Item1;

            return dist;
        }

        return new Tuple<Vector3, float?>(Force, null);
    }

    const float rayBuffer = 0.0001f;
    protected Tuple<Vector3, float?> TryMoveDirection(Vector3 originalDir, Vector3 startPoint, float deltaTime, HashSet<RaycastHit> collisions, bool dontRecurse = false)
    {
        // No movement, don't bother trying.
        if (originalDir.magnitude == 0)
            return new Tuple<Vector3, float?>(new Vector3(), null);

        var direction = originalDir;
        var collider = GetComponent<Collider>();
        var extents = collider.bounds.extents;
        var rayDist = direction.magnitude * deltaTime;
        var traveled = 0.0f;
        var canMove = true;
        float? lastAngle = null;

        var destination = new Vector3();
        var movementAttempts = 10; // Loop breaker
        bool canSlopeAgain = true;
        while (traveled < rayDist && canMove && movementAttempts > 0)
        {
            movementAttempts--;

#if DEBUG
            if (movementAttempts <= 0)
                print("Movement problem! Broke infinite loop.");
#endif

            var hits = new List<RaycastHit>();
            for (var i = -1; i < 2; i++)
            {
                // Don't need middle cast, have box for that.
                //if (i == 0)
                //    continue;

                var xDist = i * (extents.x - rayBuffer);
                var yDist = i * (extents.y - rayBuffer);
                var zDist = i * (extents.z - rayBuffer);

                // Fire rays from left or right is changing x position
                if (direction.y < 0)
                {
                    LaunchRay(new Ray(startPoint + destination + new Vector3(xDist, rayBuffer - extents.y, zDist), direction), rayDist, hits);
                }
                else if (direction.y > 0)
                {
                    LaunchRay(new Ray(startPoint + destination + new Vector3(xDist, extents.y - rayBuffer, zDist), direction), rayDist, hits);
                }

                // Fire rays from top of bottom if changing y position
                if (direction.x < 0)
                {
                    LaunchRay(new Ray(startPoint + destination + new Vector3(rayBuffer - extents.x, yDist, zDist), direction), rayDist, hits);
                }
                else if (direction.x > 0)
                {
                    LaunchRay(new Ray(startPoint + destination + new Vector3(extents.x - rayBuffer, yDist, zDist), direction), rayDist, hits);
                }

                // Z, added after, not thinking currently, following pattern of x and y, assuming the best until proven otherwise
                if (direction.z < 0)
                {
                    LaunchRay(new Ray(startPoint + destination + new Vector3(xDist, yDist, rayBuffer - extents.z), direction), rayDist, hits);
                }
                else if (direction.z > 0)
                {
                    LaunchRay(new Ray(startPoint + destination + new Vector3(xDist, yDist, extents.z - rayBuffer), direction), rayDist, hits);
                }

                // TODO: GetComponent<ScriptType> and check if moveable
            }

            // Anti-spike/cliff protection
            var boxHits = new List<RaycastHit>();
            LaunchBox(startPoint + destination, direction, rayDist, boxHits, collider);
            hits.AddRange(boxHits);

            hits.ForEach(h => collisions.Add(h));

            var orderedHits = hits.Where(h => h.collider.gameObject.tag.Contains("Solid")).OrderBy(h => h.distance).ToArray(); //Where(h => boxColliders.Contains(h.collider)).

            // Check for obstacles
            if (orderedHits.Any()) // Or use other check if better one found
            {
                var closest = orderedHits.First();
                // Travel partial distance
                var cDist = closest.distance > rayBuffer ? closest.distance - rayBuffer : closest.distance;
                var trav = cDist * direction.normalized;
                if (trav.magnitude > rayBuffer)
                {
                    destination += trav;// new Vector3(RemoveBuffer(trav.x), RemoveBuffer(trav.y), RemoveBuffer(trav.z));
                    traveled = destination.magnitude;
                    canSlopeAgain = true;
                    continue;
                }
                // Stop
                else
                {
                    if(!dontRecurse && ((direction.x != 0 && direction.y != 0) || (direction.x != 0 && direction.z != 0) || (direction.y != 0 && direction.z != 0)))
                    {
                        // Move single direction at a time, only one should return any results
                        // Breaking the directions apart resolves conflicts such as "object tries to move right, but gravity pulls it down and left on a slope"
                        // Also resolves issues such as tracking if ray hits came from which direction and calculating slopes wrong
                        var res = TryMoveDirection(new Vector3(direction.x - destination.x, 0, 0), startPoint + destination, deltaTime, collisions, true);
                        destination += res.Item1;
                        lastAngle = res.Item2;
                        res = TryMoveDirection(new Vector3(0, direction.y - destination.y, 0), startPoint + destination, deltaTime, collisions, true);
                        destination += res.Item1;
                        lastAngle += res.Item2 ?? 0;
                        res = TryMoveDirection(new Vector3(0, 0, direction.z - destination.z), startPoint + destination, deltaTime, collisions, true);
                        destination += res.Item1;
                        lastAngle += res.Item2 ?? 0;
                    }
                    else if(direction.magnitude != 0 && canSlopeAgain)
                    {
                        canSlopeAgain = false;

                        // Get nearest collision to smoothly traverse a slope instead of speeding up it via teleportation.
                        // For performance, maybe use .Last instead
                        var nextCollision = orderedHits.FirstOrDefault(h => h.distance > closest.distance);

                        // Corner stick fix
                        if (nextCollision.collider == null && orderedHits.Length == 1)
                            nextCollision = orderedHits.First();

                        // Try slope movement if not completely blocked
                        if (nextCollision.collider != null)
                        {
                            var slope = Vector3.Angle(nextCollision.normal, Vector3.up);
                            lastAngle = slope;
                            var myDirection = Vector3.Cross(Vector3.Cross(nextCollision.normal, direction), nextCollision.normal);
                            // Climb
                            if (direction.x != 0 && (slope >= (180-ClimbingDegrees % 180) || slope <= (ClimbingDegrees % 180)))
                            {
                                //slope *= Mathf.Deg2Rad;
                                direction = myDirection;// new Vector3(Mathf.Cos(slope) * (direction.magnitude - traveled), Mathf.Sin(slope) * (direction.magnitude - traveled), 0);
                                continue;
                            }
                            // Fall
                            else if (direction.y != 0 && slope <= (180-FallingDegrees % 180) && slope >= (FallingDegrees % 180))
                            {
                                //slope *= Mathf.Deg2Rad;
                                direction = myDirection;// new Vector3(Mathf.Cos(slope) * (direction.magnitude - traveled), Mathf.Sin(slope) * (direction.magnitude - traveled), 0);
                                continue;
                            }
                            // Else, "in between range" of do nothing
                        }
                    }
                    break;
                }
            }
            // Travel full distance
            else
            {
                destination += direction * deltaTime;
                traveled = destination.magnitude;
            }
        }

        return new Tuple<Vector3, float?>(destination, lastAngle);
    }

    protected void LaunchRay(Ray ray, float dist, List<RaycastHit> hits)
    {
        //Draw ray on screen to see visually. Remember visual length is not actual length.
#if DEBUG
        Debug.DrawRay(ray.origin, ray.direction, Color.green);
#endif
        var hit = Physics.RaycastAll(ray, dist + rayBuffer);
        if (hit.Any())
            hits.AddRange(hit);
    }

    protected void LaunchBox(Vector3 position, Vector3 direction, float dist, List<RaycastHit> hits, Collider collider)
    {
        //Draw ray on screen to see visually. Remember visual length is not actual length.
#if DEBUG
        Debug.DrawRay(position, direction * 0.1f, Color.blue);
#endif
        var extents = collider.bounds.extents;
        // Dumb floating point errors causing collisions where there shouldn't be any, so had to reduce box size further.
        // Character is 1x1x1, at (0,0,0). 1 tall platform is 1 below character. Character tries to move left without gravity.
        // With box shrunk by normal buffer, it was still finding collision from beneath.
        var hit = Physics.BoxCastAll(position, new Vector3(extents.x - (100.0f * rayBuffer), extents.y - (100.0f * rayBuffer), extents.z - (100.0f * rayBuffer)), direction, transform.rotation, dist).Where(h => h.collider != collider).ToArray();
        if (hit.Any())
            hits.AddRange(hit);
    }

    public static Vector3 AverageVector3(params Vector3[] positions)
    {
        if (positions.Length == 0)
            return Vector3.zero;
        float x = 0f;
        float y = 0f;
        float z = 0f;
        foreach (var pos in positions)
        {
            x += pos.x;
            y += pos.y;
            z += pos.z;
        }
        return new Vector3(x / positions.Length, y / positions.Length, z / positions.Length);
    }
}
