using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public class JumpableCharacter : MoveableCharacter
{
    #region Properties
    // Number of "jumps" the character is permitted.
    protected byte JumpCount = 1;
    protected byte CurrentJump = 0;

    // Max fall speed
    protected float FreeFallSpeed => AccelerationMultiplier * 4f;
    protected float JumpSpeed => AccelerationMultiplier * 4f;

    // Modifier for things like being shot out of a cannon, generally rise faster
    //protected float JumpSpeedModifier = 1f;
    // Flag needed to reset modifiers. 
    //protected JumpState ResetModifiersCondition = JumpState.Grounded;

    // Tracker for current jump so player doesn't just fly up forever
    // Durations measured in seconds
    protected float CurrentJumpDuration = 0;

    // Max length of first jump. Done "From the ground" so it has more power generally and therefore more distance
    protected float MaxJumpDuration = 1f;
    // "Air jumps" are generally a small boost without providing a full x2 effect, could also go for a decay system (Generally used in games that include gliding)
    // Avoiding decay here since aiming for jumping and if more jumps are provided, don't make them feel worthless
    protected float SecondJumpDuration = 0.5f;

    // So a jump isn't just a couple pixels, enforce a minimum jump length
    protected float MinimumJumpDuration = 0.25f;

    // Running on ramp before jump could increase jump length since angle is adding to "updward force", alternate example is being shot out of a cannon, probably want it stronger than regular jump
    //protected float JumpLengthModifier = 1.0;

    // Adjustable value for if air jumps partially dampen a fall, or fully reset it to upward movement
    // "Falling at 30f/s, jump gives 10f, 1.0f factor makes upward speed go to 10f/s, 0.0f factor would slow the fall to 20f/s momentarily"
    // Basically a "How much of the original falling speed to ignore when making a new jump"
    protected float SecondJumpMomentumCancelFactor = 0.9f;

    // Factor to slow jumps per current duration?
    protected float JumpDecay => JumpSpeed / MaxJumpDuration * 1.0f;

    protected float AirFriction = 0.0f;

    public JumpState CurrentJumpState = JumpState.Grounded;

    public Vector3 JumpDirection = Vector3.right; // TODO: Replace with character default direction

    protected bool AllowSpinJump = true;
    // Troid "Space jump" option
    protected bool AirJumpsRequireSpinJump = false;

    protected bool AllowWallJumps = true;
    // If MaxWallJumps is set, restricts count to set number. 
    protected byte? MaxWallJumps = null;
    protected byte CurrentWallJumps = 0;
    // Sets JumpCount to this value upon wall jump. byte.MaxValue would push it to cap
    protected byte WallJumpRestoreJumpCount = 0; // Only giving back air jumps, gets incremented shortly after

    // Available time since last "grabbable" wall to do wall jump
    protected float WallJumpBufferDuration = 0.2f;
    public float CurrentWallJumpBuffer = 0.0f;

    // If true, player needs to push away from wall for jump (ex. troid), false for holding to single wall (ex. m-man)
    protected bool WallJumpsNeedOppositeDirection = false; // Need input fix and max momentum fix for this to be viable

    // Direction set on the fly based on walls being touched
    protected Vector3 WallJumpDirection = new Vector3();
    
    // How fast to push away from the wall
    protected float WallJumpPushBackDistance => Acceleration * 2f;
    
    // How long to reduce player input after doing wall jump
    protected float JumpBackDuration = 0.2f;

    // TODO: Default wall sliding modifier and walls that dis-allow climbing
    // TODO: Walljump angle modifier

    // TODO: Climbable (Wall and ceiling
    // TODO: Flyable, with max distance, options between min-max, dist from ground, and gliding with gliding being before air jumps, mixing airjumps into flyable distance

    // Outside modifyable flag for if we should even bother with trying to jump.
    protected bool CanJump = true;
    // When to apply gravity. Could have "flying" portions that turn this off or ladders
    protected bool ShouldFall = true;
    #endregion

    #region SubTypes
    [Flags]
    public enum JumpState
    {
        Grounded = 0, // AKA doing nothing related to being in the air
        Jumping = 1, // Rising
        Falling = 2, // Dropping
        Spinning = 4, // Spinning in jump or fall, use combination.
        // Example is Sonic or Metroid, spinning during jump gives more horizontal mobility and potential damage
        // Using doubling so flagging can occur (Jumping AND spinning)
    }
    #endregion

    // Use this for initialization
    protected override void Start()
    {
        base.Start();
    }

    // Update is called once per frame
    protected override void DoUpdate(bool doMove)
    {
        base.DoUpdate(false);

        if (PlayerControlled)
        {
            // Jump if space is pressed and we are moving down or are near the peak of the jump
            if (Input.GetKeyDown(KeyCode.Space) && (CurrentJumpState & JumpState.Jumping) <= 0
                && ((CurrentJump < JumpCount && (CurrentJump == 0 || !AirJumpsRequireSpinJump || (CurrentJumpState & JumpState.Spinning) > 0))
                // Walljump checks
                || (AllowWallJumps && (MaxWallJumps == null || CurrentWallJumps < MaxWallJumps) && CurrentWallJumpBuffer > 0
                // The * checking for negative ensures opposite direction without anything fancy to deal with magnitudes
                // TODO: User inputs instead of momentum checks
                && (!WallJumpsNeedOppositeDirection || Momentum.x * WallJumpDirection.x < 0 || Momentum.z * WallJumpDirection.z < 0))))
            {
                // If already falling, take a jump out since jump count is "Ground jump" + "Magic or other special bonus jumps"
                if (CurrentJump == 0 && (CurrentJumpState & JumpState.Falling) > 0)
                    CurrentJump++;

                if (CurrentWallJumpBuffer > 0 && ReducedInputDuration <= 0)
                {
                    CurrentJump = WallJumpRestoreJumpCount;
                    CurrentWallJumpBuffer = 0;

                    // Reset momentum based on direction/angle
                    Momentum.x -= Mathf.Abs(Momentum.x) * WallJumpDirection.x;
                    Momentum.y -= Mathf.Abs(Momentum.y) * WallJumpDirection.y;
                    Momentum.z -= Mathf.Abs(Momentum.z) * WallJumpDirection.z;

                    Force.x -= WallJumpPushBackDistance * WallJumpDirection.x;
                    Force.z -= WallJumpPushBackDistance * WallJumpDirection.z;

                    ReducedInputDuration += JumpBackDuration;

                    if (MaxWallJumps != null)
                        CurrentWallJumps++;

                    // TODO: Replace with input adjustments and get z support
                    if (AllowSpinJump && Momentum.x != 0)
                    {
                        CurrentJumpState |= JumpState.Spinning;
                        JumpDirection = Momentum.x < 0 ? Vector3.left : Vector3.right;
                    }
                }

                // Negate fall force if second jump
                if (CurrentJump > 0)
                {
                    if (SecondJumpMomentumCancelFactor != 0)
                    {
                        Momentum.y -= Momentum.y * SecondJumpMomentumCancelFactor;
                        Force.y -= Force.y * SecondJumpMomentumCancelFactor;
                    }
                }
                else
                {
                    if (Momentum.y < 0)
                        Momentum.y = 0;

                    // TODO: Replace with input adjustments
                    if (AllowSpinJump && Momentum.x != 0)
                    {
                        CurrentJumpState |= JumpState.Spinning;
                        JumpDirection = Momentum.x < 0 ? Vector3.left : Vector3.right;
                    }
                }

                // Reset state to jumping, include spinning if previously spinning
                CurrentJumpState |= JumpState.Jumping;
                DeccelerationModifier = AirFriction;
                if ((CurrentJumpState & JumpState.Falling) > 0)
                    CurrentJumpState -= JumpState.Falling;
                CurrentJumpDuration = 0;
                CurrentJump++;
                Force.y += JumpSpeed;
            }
        }

        if ((CurrentJumpState & JumpState.Jumping) > 0)
        {
            // Rise
            if (CurrentJumpDuration < (CurrentJump <= 1 ? MaxJumpDuration : SecondJumpDuration) && (CurrentJumpDuration < MinimumJumpDuration || (PlayerControlled && Input.GetKey(KeyCode.Space))))
            {
                CurrentJumpDuration += Time.deltaTime;
                Force.y -= Time.deltaTime * JumpDecay;
                DeccelerationModifier = AirFriction;
            }
            // Start falling
            else
            {
                CurrentJumpState -= JumpState.Jumping;
                CurrentJumpState |= JumpState.Falling;
                DeccelerationModifier = AirFriction;
            }
        }

        // Fall
        if ((CurrentJumpState & JumpState.Jumping) == 0 && -Momentum.y < FreeFallSpeed)
        {
            Force.y -= FreeFallSpeed + Momentum.y;
            DeccelerationModifier = AirFriction;
        }

        if (CurrentWallJumpBuffer > 0)
            CurrentWallJumpBuffer -= Time.deltaTime;

        if (doMove)
        {
            var hits = new HashSet<RaycastHit>();
            var resultingDist = DoMove(hits);

            var slope = resultingDist.Item2;
            // Hit "walkable surface" or stopped moving
            if ((slope != null && (slope >= (180 - ClimbingDegrees % 180) || slope <= (ClimbingDegrees % 180))) || resultingDist.Item1.y == 0)
            {
                //If moving down
                if (Momentum.y <= 0)
                {
                    CurrentJumpState = JumpState.Grounded;
                    DeccelerationModifier = 1f;
                    CurrentJump = 0;
                    Momentum.y = 0;
                    CurrentWallJumps = 0;
                }
                else
                {
                    CurrentJumpState |= JumpState.Falling;
                    if ((CurrentJumpState & JumpState.Jumping) > 0)
                        CurrentJumpState -= JumpState.Jumping;
                }
            }

            // Only wall jump in air, no "wall jumping" from the ground
            if (AllowWallJumps && ((CurrentJumpState & JumpState.Falling) > 0 || (CurrentJumpState & JumpState.Jumping) > 0))
            {
                var collisions = new HashSet<RaycastHit>();
                // TODO: Replace 1f with walljump distance buffer
                if (Momentum.x <= 0)
                    TryMoveDirection(Vector3.left * 1f, transform.position, Time.deltaTime, collisions, true);
                if (Momentum.x >= 0)
                    TryMoveDirection(Vector3.right * 1f, transform.position, Time.deltaTime, collisions, true);
                if (Momentum.z <= 0)
                    TryMoveDirection(Vector3.back * 1f, transform.position, Time.deltaTime, collisions, true);
                if (Momentum.z >= 0)
                    TryMoveDirection(Vector3.forward * 1f, transform.position, Time.deltaTime, collisions, true);

                var angles = collisions.ToDictionary(c => c, c => Vector3.Angle(c.normal, Vector3.up)).Where(c => c.Value <= (180 - FallingDegrees % 180) && c.Value >= (FallingDegrees % 180)).ToList();
                if (angles.Any())
                {
                    // Turn on walljumps
                    CurrentWallJumpBuffer = WallJumpBufferDuration;

                    WallJumpDirection = new Vector3();
                    WallJumpDirection -= AverageVector3(angles.Select(a => a.Key.normal).ToArray()).normalized;
                }
            }
        }
    }
}
