using UnityEngine;
using AITest.Pathfinding;

namespace AITest.Enemy
{
    /// <summary>
    /// AI Agent Mover - Wrapper around existing Pathfinder
    /// 
    /// Renamed from EnemyMover2D to be generic.
    /// </summary>
    [RequireComponent(typeof(Pathfinder))]
    [RequireComponent(typeof(AICharacterController))]
    public class AIAgentMover : MonoBehaviour
    {
        [Header("References")]
        public Pathfinder pathfinder;
        public AICharacterController controller;
        
        [Header("Settings")]
        [Tooltip("Replan interval (seconds) - prevents constant replanning")]
        [Range(0.1f, 2f)] public float replanInterval = 0.5f;
        
        [Tooltip("Arrival radius (meters)")]
        [Range(0.1f, 2f)] public float arrivalRadius = 0.6f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // State
        private Vector2? currentDestination;
        private float lastReplanTime;
        
        // Public properties
        public bool ReachedDestination => pathfinder && pathfinder.ReachedTarget;
        public Vector2 CurrentPosition => transform.position;
        public bool HasPath => pathfinder && pathfinder.HasPath;
        public bool IsMoving => controller && controller.IsMoving;
        public Vector2? Destination => currentDestination;
        public Vector2 Velocity => controller ? controller.Velocity : Vector2.zero;

        private void Awake()
        {
            // Auto-find components
            if (!pathfinder) pathfinder = GetComponent<Pathfinder>();
            if (!controller) controller = GetComponent<AICharacterController>();
        }

        /// <summary>
        /// ? PROMPT 7: Set destination (requests path)
        /// </summary>
        public void SetDestination(Vector2 worldPosition)
        {
            // Check if destination changed significantly
            if (currentDestination.HasValue)
            {
                float dist = Vector2.Distance(currentDestination.Value, worldPosition);
                
                if (dist < 0.5f && Time.time - lastReplanTime < replanInterval)
                {
                    // Same destination, skip replan
                    return;
                }
            }
            
            // 1. Request path from Pathfinder
            if (pathfinder != null)
            {
                pathfinder.SetTarget(worldPosition);
            }
            
            currentDestination = worldPosition;
            lastReplanTime = Time.time;
            
            if (showDebugLogs)
                Debug.Log($"<color=cyan>[AIAgentMover] Path requested to: {worldPosition}</color>");
        }

        private void Update()
        {
            if (pathfinder == null || controller == null) return;
            
            if (pathfinder.HasPath)
            {
                // Get next node from path
                Vector2? nextPoint = pathfinder.GetNextWaypoint();
                
                if (nextPoint.HasValue)
                {
                    controller.GoTo(nextPoint.Value);
                }
                else if (pathfinder.ReachedTarget)
                {
                    controller.Stop();
                }
            }
            else if (currentDestination.HasValue)
            {
                // Fallback: If no path found but we have a destination (maybe waiting for calculation), stop or move direct
                // Ideally, pathfinder handles this. For now, let's just wait.
                 if (Vector2.Distance(transform.position, currentDestination.Value) < arrivalRadius)
                {
                    controller.Stop();
                }
            }
        }

        /// <summary>
        /// Stop movement
        /// </summary>
        public void Stop()
        {
            controller.Stop();
            
            if (showDebugLogs)
                Debug.Log($"<color=yellow>[AIAgentMover] Stopped at {CurrentPosition}</color>");
        }

        /// <summary>
        /// Clear destination
        /// </summary>
        public void ClearDestination()
        {
            currentDestination = null;
            if (pathfinder != null) pathfinder.ClearPath();
            Stop();
        }

        /// <summary>
        /// Force replan (ignores replan interval)
        /// </summary>
        public void ForceReplan()
        {
            if (currentDestination.HasValue)
            {
                controller.GoTo(currentDestination.Value);
                lastReplanTime = Time.time;
                
                if (showDebugLogs)
                    Debug.Log("<color=orange>[AIAgentMover] Force replan</color>");
            }
        }

        /// <summary>
        /// Get distance to destination
        /// </summary>
        public float GetDistanceToDestination()
        {
            if (!currentDestination.HasValue)
                return float.MaxValue;
            
            return Vector2.Distance(CurrentPosition, currentDestination.Value);
        }

        /// <summary>
        /// Check if at position
        /// </summary>
        public bool IsAtPosition(Vector2 targetPos, float threshold = -1f)
        {
            if (threshold < 0f)
                threshold = arrivalRadius;
            
            return Vector2.Distance(CurrentPosition, targetPos) < threshold;
        }

        /// <summary>
        /// Check if arrived at destination (delegates to EnemyController)
        /// </summary>
        public bool Arrived()
        {
            return controller && controller.Arrived(arrivalRadius);
        }

        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!showDebugLogs) return;
            
            // Draw destination marker
            if (currentDestination.HasValue)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(currentDestination.Value, arrivalRadius);
                
                // Draw line to destination
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawLine(CurrentPosition, currentDestination.Value);
            }
        }
        #endregion
    }
}
