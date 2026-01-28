using UnityEngine;

namespace AITest.Enemy
{
    /// <summary>
    /// AI Character Controller - Clean Rewrite
    /// Handles physical movement using simple Transform translation or Rigidbody2D.
    /// Ensures Inspector variables (moveSpeed) are respected.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class AICharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Movement speed in meters/second")]
        [Range(0f, 10f)] public float moveSpeed = 3.5f;

        [Tooltip("Rotation speed in degrees/second")]
        [Range(90f, 720f)] public float rotationSpeed = 360f;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // References
        private Rigidbody2D rb;
        
        // State
        private Vector2 targetPosition;
        private bool hasTarget = false;

        public bool IsMoving => hasTarget && moveSpeed > 0.01f;
        public Vector2 Velocity => rb ? rb.linearVelocity : Vector2.zero;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            
            // Ensure Rigidbody is Kinematic for AI control
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }

        private void FixedUpdate()
        {
            if (hasTarget && moveSpeed > 0.01f)
            {
                MoveTowardsTarget();
            }
            else
            {
                // Stop if no target or speed is 0
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Set a new target position to move towards
        /// </summary>
        public void GoTo(Vector2 position)
        {
            targetPosition = position;
            hasTarget = true;
        }

        /// <summary>
        /// Stop movement immediately
        /// </summary>
        public void Stop()
        {
            hasTarget = false;
            rb.linearVelocity = Vector2.zero;
            if (showDebugLogs) Debug.Log("<color=yellow>[AIController] Stopped.</color>");
        }

        /// <summary>
        /// Check if arrived at target within threshold
        /// </summary>
        public bool Arrived(float threshold = 0.1f)
        {
            if (!hasTarget) return true;
            return Vector2.Distance(rb.position, targetPosition) <= threshold;
        }

        /// <summary>
        /// Frame-based movement logic
        /// </summary>
        private void MoveTowardsTarget()
        {
            // 1. Calculate direction
            Vector2 direction = (targetPosition - rb.position).normalized;
            Vector2 position = rb.position;
            float distance = Vector2.Distance(position, targetPosition);

            // 2. Calculate step size (respector Time.deltaTime and moveSpeed)
            float step = moveSpeed * Time.deltaTime;
            
            // Set velocity so other scripts (Perception) can read it
            Vector2 actualVelocity = direction * moveSpeed;
            rb.linearVelocity = actualVelocity;

            if (distance <= step)
            {
                // Snap to target if very close
                rb.MovePosition(targetPosition);
                // Don't clear target automatically, let external logic decide when to stop or set next waypoint
            }
            else
            {
                // Move towards target
                Vector2 newPos = position + (direction * step);
                rb.MovePosition(newPos);
            }

            // 3. Rotate towards direction
            if (direction != Vector2.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void OnDrawGizmos()
        {
            if (showDebugLogs && hasTarget)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, targetPosition);
                Gizmos.DrawWireSphere(targetPosition, 0.2f);
            }
        }
    }
}
