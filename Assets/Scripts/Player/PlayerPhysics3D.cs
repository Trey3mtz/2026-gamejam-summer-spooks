using UnityEngine;
using SummerSpooks.Player.Data;
using SummerSpooks.Player.Configuration;

namespace SummerSpooks.Player
{
    /// <summary>
    /// The single, shared 3D movement motor. Pure-ish, kinematic capsule simulation:
    /// it never mutates the player's transform itself. Instead it takes the current
    /// <see cref="CharacterStatePayload"/>, integrates intent + forces, resolves the
    /// capsule against the world with a collide-and-slide sweep, and returns the next
    /// state so the controller can commit the predicted position.
    ///
    /// Broken into the same phases as the 2D motor: Intent &amp; timers > Integrate forces > Resolve space.
    /// </summary>
    public static class PlayerPhysics3D
    {
        // Core feel constants – never vary per character/build
        private const float JumpBufferTime = 0.12f;
        private const float CoyoteTime = 0.12f;
        private const float SkinWidth = 0.015f;
        private const float GroundProbeDistance = 0.12f;
        private const float MinGroundNormalY = 0.5f; // ~60 degrees; steeper counts as a wall
        private const int MaxSlideIterations = 5;

        // Reused buffer for depenetration overlap queries (avoids per-frame allocations).
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        public static CharacterStatePayload Simulate(
            CharacterStatePayload state,
            PlayerInputPayload cmd,
            MovementProfile profile,
            CapsuleCollider capsule,
            LayerMask groundMask,
            ExternalImpulse? impulse = null)
        {
            float dt = cmd.DeltaTime > 0f ? cmd.DeltaTime : Time.deltaTime;

            // --- Intent & timers ---
            state.JumpBufferTimer = cmd.JumpPressed ? JumpBufferTime : state.JumpBufferTimer - dt;

            bool grounded = ProbeGround(state.Position, capsule, groundMask);
            state.Grounded = grounded;
            state.CoyoteTimer = grounded ? CoyoteTime : state.CoyoteTimer - dt;
            if (grounded)
                state.AirJumpsUsed = 0;

            // --- Integrate vertical forces ---
            if (grounded && state.Velocity.y <= 0f)
            {
                // Small constant pull keeps us glued to the ground / steps & slopes.
                state.Velocity.y = -profile.GroundStickSpeed;
            }
            else
            {
                state.Velocity.y -= profile.Gravity * dt;
                if (state.Velocity.y < -profile.MaxFallSpeed)
                    state.Velocity.y = -profile.MaxFallSpeed;
            }

            // --- Integrate horizontal forces ---
            float targetSpeed = cmd.Crouch ? profile.CrouchSpeed
                              : cmd.Sprint ? profile.SprintSpeed
                              : profile.WalkSpeed;

            Vector3 targetHorizontal = cmd.WorldMove * targetSpeed;
            Vector3 horizontal = new Vector3(state.Velocity.x, 0f, state.Velocity.z);

            // In the air we keep only a fraction of our steering authority.
            float smoothTime = grounded
                ? profile.MoveSmoothTime
                : profile.MoveSmoothTime / Mathf.Max(profile.AirControl, 0.01f);

            horizontal = Vector3.SmoothDamp(horizontal, targetHorizontal, ref state.HorizontalRef,
                smoothTime, Mathf.Infinity, dt);

            state.Velocity.x = horizontal.x;
            state.Velocity.z = horizontal.z;

            // Kill micro-slides when there is no input.
            if (cmd.WorldMove.sqrMagnitude < 0.0001f &&
                horizontal.sqrMagnitude < 0.0025f) // < 0.05 m/s
            {
                state.Velocity.x = 0f;
                state.Velocity.z = 0f;
            }

            // --- Jump (after gravity so a fresh jump is not cancelled by the ground stick) ---
            bool wantsToJump = state.JumpBufferTimer > 0f;
            if (wantsToJump && (grounded || state.CoyoteTimer > 0f))
                DoJump(ref state, profile);

            // Jump cut for variable height.
            if (cmd.JumpReleased && state.Velocity.y > 0f)
                state.Velocity.y *= 0.5f;

            // --- External impulse on top of player-driven motion ---
            if (impulse.HasValue)
                state.Velocity += impulse.Value.Force;

            // --- Clamp combined velocity ---
            state.Velocity.x = Mathf.Clamp(state.Velocity.x, -profile.MaxHorizontalSpeed, profile.MaxHorizontalSpeed);
            state.Velocity.z = Mathf.Clamp(state.Velocity.z, -profile.MaxHorizontalSpeed, profile.MaxHorizontalSpeed);
            state.Velocity.y = Mathf.Clamp(state.Velocity.y, -profile.MaxFallSpeed, profile.JumpVelocity + 1f);

            // --- Resolve space: predict, slide along surfaces, then push out of any overlap ---
            Vector3 displacement = state.Velocity * dt;
            Vector3 predicted = CollideAndSlide(state.Position, displacement, capsule, groundMask);
            predicted = Depenetrate(predicted, capsule, groundMask);

            state.JustJumped = false;
            state.Position = predicted;
            state.Tick = cmd.Tick;
            return state;
        }

        private static void DoJump(ref CharacterStatePayload state, MovementProfile profile)
        {
            state.Velocity.y = profile.JumpVelocity;
            state.Grounded = false;
            state.JustJumped = true;
            state.JumpBufferTimer = 0f;
            state.CoyoteTimer = 0f;
        }

        /// <summary>Short downward capsule cast to decide if we are standing on walkable ground.</summary>
        private static bool ProbeGround(Vector3 position, CapsuleCollider capsule, LayerMask mask)
        {
            GetCapsulePoints(position, capsule, out Vector3 top, out Vector3 bottom, out float radius);

            // Slightly thinner probe so wall edges next to us are not mistaken for floor.
            if (Physics.CapsuleCast(top, bottom, radius * 0.92f, Vector3.down, out RaycastHit hit,
                    GroundProbeDistance + SkinWidth, mask, QueryTriggerInteraction.Ignore))
            {
                return hit.normal.y >= MinGroundNormalY;
            }
            return false;
        }

        /// <summary>Sweep the capsule along <paramref name="displacement"/>, sliding along anything it hits.</summary>
        private static Vector3 CollideAndSlide(Vector3 position, Vector3 displacement,
            CapsuleCollider capsule, LayerMask mask)
        {
            Vector3 pos = position;
            Vector3 remaining = displacement;

            for (int i = 0; i < MaxSlideIterations; i++)
            {
                float distance = remaining.magnitude;
                if (distance < 1e-5f)
                    break;

                Vector3 direction = remaining / distance;
                GetCapsulePoints(pos, capsule, out Vector3 top, out Vector3 bottom, out float radius);

                if (Physics.CapsuleCast(top, bottom, radius, direction, out RaycastHit hit,
                        distance + SkinWidth, mask, QueryTriggerInteraction.Ignore))
                {
                    float allowed = Mathf.Max(hit.distance - SkinWidth, 0f);
                    pos += direction * allowed;

                    // Project whatever motion is left onto the surface so we slide instead of stop.
                    Vector3 leftover = remaining - direction * allowed;
                    remaining = Vector3.ProjectOnPlane(leftover, hit.normal);
                }
                else
                {
                    pos += remaining;
                    break;
                }
            }

            return pos;
        }

        /// <summary>Push the capsule out of anything it ended up overlapping (anti-tunnelling safety net).</summary>
        private static Vector3 Depenetrate(Vector3 position, CapsuleCollider capsule, LayerMask mask)
        {
            Vector3 pos = position;
            Quaternion rotation = capsule.transform.rotation;

            for (int pass = 0; pass < 2; pass++)
            {
                GetCapsulePoints(pos, capsule, out Vector3 top, out Vector3 bottom, out float radius);
                int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, OverlapBuffer, mask,
                    QueryTriggerInteraction.Ignore);

                bool resolvedAny = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = OverlapBuffer[i];
                    if (other == capsule)
                        continue;

                    if (Physics.ComputePenetration(
                            capsule, pos, rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 dir, out float dist))
                    {
                        pos += dir * dist;
                        resolvedAny = true;
                    }
                }

                if (!resolvedAny)
                    break;
            }

            return pos;
        }

        /// <summary>
        /// World-space sphere centres and radius of the capsule, evaluated at <paramref name="position"/>
        /// (the predicted transform position) rather than the collider's current location.
        /// Assumes a Y-axis capsule, which is the FPS body convention.
        /// </summary>
        private static void GetCapsulePoints(Vector3 position, CapsuleCollider capsule,
            out Vector3 top, out Vector3 bottom, out float radius)
        {
            Transform t = capsule.transform;
            Vector3 scale = t.lossyScale;

            radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
            float halfSpine = Mathf.Max(0f, height * 0.5f - radius);

            Vector3 axis = t.rotation * Vector3.up; // yaw-only body keeps this world-up
            Vector3 center = position + t.rotation * Vector3.Scale(capsule.center, scale);

            top = center + axis * halfSpine;
            bottom = center - axis * halfSpine;
        }
    }
}
