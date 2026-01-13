using UnityEngine;
using AITest.WorldModel;
using AITest.UI;

namespace AITest.Enemy
{
    /// <summary>
    /// Chase Executor - Hard interrupt chase system
    /// 
    /// PROMPT 14: Direct player pursuit (outside Q-learning)
    /// - Activated when SeePlayerNow = true
    /// - Replans at throttled frequency (0.3s)
    /// - Continues to LastSeenPos if LOS breaks
    /// - Captures player (distance + LOS check)
    /// - Raises OnPlayerCaptured event
    /// - Feeds big reward to Q-learning
    /// </summary>
    public class ChaseExecutor : MonoBehaviour
    {
        [Header("Chase Settings")]
        [Tooltip("Replan frequency while chasing (seconds)")]
        [Range(0.1f, 1f)] public float replanInterval = 0.3f;
        
        [Tooltip("Continue chasing to LastSeenPos if LOS breaks")]
        public bool continueToLastSeen = true;
        
        [Tooltip("Timeout after losing LOS (seconds)")]
        [Range(1f, 10f)] public float lostTargetTimeout = 3f;
        
        [Header("Capture Settings")]
        [Tooltip("Capture radius (meters)")]
        [Range(0.5f, 3f)] public float captureRadius = 1.5f;
        
        [Tooltip("Require LOS for capture")]
        public bool requireLOSForCapture = false;
        
        [Tooltip("What happens on capture")]
        public CaptureAction onCaptureAction = CaptureAction.ResetPlayer;
        
        [Header("Rewards")]
        [Tooltip("Big reward for capture")]
        [Range(10f, 100f)] public float captureReward = 50f;
        
        [Header("Components")]
        public EnemyContext context;
        public AIAgentMover mover;
        public LearningMetrics metrics;
        
        [Header("Debug")]
        public bool showDebugLogs = true;
        
        // Chase state
        public bool IsChasing { get; private set; }
        private float nextReplanTime;
        private float lostTargetTime;
        private Vector2 lastKnownPlayerPos;
        private float chaseStartTime;
        
        // Events
        public System.Action<float, Vector2> OnPlayerCaptured;

        private void Awake()
        {
            // Auto-find components
            if (!context) context = GetComponent<EnemyContext>();
            if (!mover) mover = GetComponent<AIAgentMover>();
            if (!metrics) metrics = FindObjectOfType<LearningMetrics>();
        }

        /// <summary>
        /// ? PROMPT 14: Start chase
        /// </summary>
        public void StartChase()
        {
            if (IsChasing)
                return; // Already chasing
            
            IsChasing = true;
            chaseStartTime = Time.time;
            nextReplanTime = Time.time;
            lostTargetTime = -999f;
            
            lastKnownPlayerPos = context.GetPlayerPosition();
            
            if (showDebugLogs)
                Debug.Log($"<color=red>[ChaseExecutor] ??? CHASE STARTED ???</color>");
            
            // ? First replan
            ReplanChase();
        }

        /// <summary>
        /// ? PROMPT 14: Update chase (called every frame by EnemyBrain)
        /// </summary>
        public ChaseStatus UpdateChase()
        {
            if (!IsChasing)
                return ChaseStatus.Inactive;
            
            // ? 1. Check if player still visible
            bool canSeePlayer = context.CanSeePlayer();
            
            if (canSeePlayer)
            {
                // ? Player visible - continue chase
                lastKnownPlayerPos = context.GetPlayerPosition();
                lostTargetTime = -999f; // Reset timeout
                
                // ? 2. Check capture
                if (CheckCapture())
                {
                    OnCaptureSuccess();
                    return ChaseStatus.Captured;
                }
                
                // ? 3. Replan (throttled)
                if (Time.time >= nextReplanTime)
                {
                    ReplanChase();
                    nextReplanTime = Time.time + replanInterval;
                }
                
                return ChaseStatus.Chasing;
            }
            else
            {
                // ? LOS broken
                if (lostTargetTime < 0f)
                {
                    // Just lost target
                    lostTargetTime = Time.time;
                    
                    if (continueToLastSeen)
                    {
                        // ? Continue to last seen position
                        mover.SetDestination(lastKnownPlayerPos);
                        
                        if (showDebugLogs)
                            Debug.Log($"<color=yellow>[ChaseExecutor] LOS BROKEN - continuing to last seen pos</color>");
                    }
                }
                
                // ? 4. Check timeout
                float timeSinceLost = Time.time - lostTargetTime;
                
                if (timeSinceLost > lostTargetTimeout)
                {
                    // ? Timeout - end chase
                    OnChaseFailed();
                    return ChaseStatus.Lost;
                }
                
                // ? 5. Check if reached last seen pos
                if (continueToLastSeen && mover.ReachedDestination)
                {
                    // ? Arrived at last seen pos but player not there
                    OnChaseFailed();
                    return ChaseStatus.Lost;
                }
                
                return ChaseStatus.LosingTarget;
            }
        }

        /// <summary>
        /// Stop chase
        /// </summary>
        public void StopChase()
        {
            if (!IsChasing)
                return;
            
            IsChasing = false;
            mover.Stop();
            
            if (showDebugLogs)
                Debug.Log($"<color=yellow>[ChaseExecutor] Chase stopped</color>");
        }

        /// <summary>
        /// Replan chase path
        /// </summary>
        private void ReplanChase()
        {
            if (!context.CanSeePlayer())
                return;
            
            Vector2 playerPos = context.GetPlayerPosition();
            
            // ? Set destination to player position
            mover.SetDestination(playerPos);
            
            if (showDebugLogs)
                Debug.Log($"<color=red>[ChaseExecutor] REPLAN ? {playerPos}</color>");
        }

        /// <summary>
        /// ? PROMPT 14: Check capture condition
        /// </summary>
        private bool CheckCapture()
        {
            if (!context.CanSeePlayer())
                return false;
            
            Vector2 playerPos = context.GetPlayerPosition();
            Vector2 enemyPos = context.Position;
            
            // ? 1. Distance check
            float dist = Vector2.Distance(enemyPos, playerPos);
            
            if (dist > captureRadius)
                return false;
            
            // ? 2. LOS check (optional)
            if (requireLOSForCapture)
            {
                // Raycast to player (use obstacle layer from pathfinder)
                Vector2 toPlayer = playerPos - enemyPos;
                
                // Try to get obstacle layer from context or use default
                LayerMask obstacleLayer = LayerMask.GetMask("Obstacles", "Wall");
                
                RaycastHit2D hit = Physics2D.Raycast(enemyPos, toPlayer.normalized, dist, obstacleLayer);
                
                if (hit.collider != null)
                {
                    // Wall blocking
                    return false;
                }
            }
            
            // ? Capture!
            return true;
        }

        /// <summary>
        /// ? PROMPT 14: Capture success
        /// </summary>
        private void OnCaptureSuccess()
        {
            float captureTime = Time.time - chaseStartTime;
            Vector2 capturePos = context.GetPlayerPosition();
            
            IsChasing = false;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=lime>[ChaseExecutor] ??? PLAYER CAPTURED! ???</color>");
                Debug.Log($"  Time: {captureTime:F2}s");
                Debug.Log($"  Position: {capturePos}");
            }
            
            // ? Raise event
            OnPlayerCaptured?.Invoke(captureTime, capturePos);
            
            // ? Update metrics
            if (metrics)
            {
                metrics.MarkCapture();
            }
            
            // ? Handle capture action
            HandleCaptureAction(capturePos);
        }

        /// <summary>
        /// Chase failed (lost target)
        /// </summary>
        private void OnChaseFailed()
        {
            float chaseDuration = Time.time - chaseStartTime;
            
            IsChasing = false;
            
            if (showDebugLogs)
                Debug.Log($"<color=yellow>[ChaseExecutor] Chase FAILED (lost target after {chaseDuration:F2}s)</color>");
            
            // ? Mark room as searched
            string lastSeenRoom = context.worldModel.LastSeenRoom;
            if (!string.IsNullOrEmpty(lastSeenRoom) && lastSeenRoom != "None")
            {
                context.worldModel.MarkRoomSearched(lastSeenRoom);
                
                if (showDebugLogs)
                    Debug.Log($"<color=orange>[ChaseExecutor] Marked {lastSeenRoom} as searched</color>");
            }
        }

        /// <summary>
        /// Handle capture action
        /// </summary>
        private void HandleCaptureAction(Vector2 capturePos)
        {
            switch (onCaptureAction)
            {
                case CaptureAction.ResetPlayer:
                    ResetPlayerToSpawn();
                    break;
                
                case CaptureAction.EndRun:
                    EndRun();
                    break;
                
                case CaptureAction.DoNothing:
                    // Just stop (for testing)
                    break;
            }
        }

        /// <summary>
        /// Reset player to spawn
        /// </summary>
        private void ResetPlayerToSpawn()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                // Find spawn point (or use default)
                var spawn = GameObject.Find("PlayerSpawn");
                if (spawn)
                {
                    player.transform.position = spawn.transform.position;
                }
                else
                {
                    // Default spawn
                    player.transform.position = Vector2.zero;
                }
                
                if (showDebugLogs)
                    Debug.Log("<color=cyan>[ChaseExecutor] Player reset to spawn</color>");
            }
        }

        /// <summary>
        /// End run (pause game or show UI)
        /// </summary>
        private void EndRun()
        {
            if (showDebugLogs)
                Debug.Log("<color=red>[ChaseExecutor] RUN ENDED - Player captured!</color>");
            
            // TODO: Show end screen or restart level
            Time.timeScale = 0f; // Pause game
        }

        private void OnDrawGizmos()
        {
            if (!IsChasing)
                return;
            
            // Draw capture radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, captureRadius);
            
            // Draw line to player
            if (context && context.CanSeePlayer())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, context.GetPlayerPosition());
            }
            
            // Draw last known position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastKnownPlayerPos, 0.5f);
        }
    }

    /// <summary>
    /// Chase status
    /// </summary>
    public enum ChaseStatus
    {
        Inactive = 0,     // Not chasing
        Chasing = 1,      // Actively chasing (player visible)
        LosingTarget = 2, // LOS broken, continuing to last seen
        Captured = 3,     // Player captured!
        Lost = 4          // Lost target (timeout)
    }

    /// <summary>
    /// Capture action
    /// </summary>
    public enum CaptureAction
    {
        DoNothing = 0,    // Just stop (for testing)
        ResetPlayer = 1,  // Reset player to spawn
        EndRun = 2        // End run (pause/UI)
    }
}
