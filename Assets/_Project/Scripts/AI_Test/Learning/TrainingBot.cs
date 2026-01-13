using UnityEngine;
using System.Collections.Generic;

namespace AITest.Learning
{
    /// <summary>
    /// Training Bot - Simulates Player for Q-Learning Training
    /// 
    /// PROFESSOR FEEDBACK: "Script kullanarak/bot kullanarak eðitim yapmaný öneririm.
    /// Gerçek oyuncuyla temiz bir eðitim eðrisi alamazsýn."
    /// 
    /// Bot Types:
    /// 1. RandomBot: Random movement (baseline)
    /// 2. PatternBot: Circular/square patterns (predictable)
    /// 3. HideBot: Runs to hide spots (realistic)
    /// 
    /// This provides CONSISTENT behavior for stable learning.
    /// </summary>
    public class TrainingBot : MonoBehaviour
    {
        [Header("Bot Type")]
        [Tooltip("Bot behavior mode")]
        public BotType botType = BotType.Random;
        
        [Header("Movement Settings")]
        [Tooltip("Movement speed (m/s)")]
        public float moveSpeed = 4f;
        
        [Tooltip("Sprint speed (m/s)")]
        public float sprintSpeed = 7f;
        
        [Tooltip("Sprint probability (0-1)")]
        [Range(0f, 1f)] public float sprintProbability = 0.3f;
        
        [Header("Random Bot Settings")]
        [Tooltip("Min time before direction change (seconds)")]
        public float minDirectionChangeTime = 1f;
        
        [Tooltip("Max time before direction change (seconds)")]
        public float maxDirectionChangeTime = 4f;
        
        [Header("Pattern Bot Settings")]
        [Tooltip("Pattern type for PatternBot")]
        public PatternType patternType = PatternType.Circle;
        
        [Tooltip("Pattern radius (meters)")]
        public float patternRadius = 5f;
        
        [Tooltip("Pattern speed (revolution per minute)")]
        public float patternSpeed = 10f;
        
        [Header("Hide Bot Settings")]
        [Tooltip("Hide spot check interval (seconds)")]
        public float hideSpotCheckInterval = 2f;
        
        [Tooltip("Max distance to consider hide spot (meters)")]
        public float maxHideSpotDistance = 20f;
        
        [Header("Boundaries")]
        [Tooltip("Movement area bounds (leave empty for no bounds)")]
        public Bounds movementBounds;
        
        [Tooltip("Use movement bounds")]
        public bool useBounds = false;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        public bool showGizmos = true;
        
        // Components
        private Rigidbody2D rb;
        
        // State
        private Vector2 currentDirection;
        private float nextDirectionChangeTime;
        private bool isSprinting;
        
        // Pattern state
        private float patternAngle;
        private Vector2 patternCenter;
        
        // Hide bot state
        private Vector2 targetHideSpot;
        private float nextHideSpotCheckTime;
        
        public enum BotType
        {
            Random,     // Random movement
            Pattern,    // Circular/square pattern
            Hide        // Runs to hide spots
        }
        
        public enum PatternType
        {
            Circle,
            Square
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            
            if (!rb)
            {
                Debug.LogError("[TrainingBot] Rigidbody2D not found! Adding one...");
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            
            // Setup rigidbody for 2D top-down
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.freezeRotation = true;
            
            // Initialize
            patternCenter = transform.position;
        }

        private void Start()
        {
            Initialize();
        }

        private void FixedUpdate()
        {
            switch (botType)
            {
                case BotType.Random:
                    UpdateRandomBot();
                    break;
                    
                case BotType.Pattern:
                    UpdatePatternBot();
                    break;
                    
                case BotType.Hide:
                    UpdateHideBot();
                    break;
            }
            
            // Apply bounds
            if (useBounds)
            {
                ApplyBounds();
            }
        }

        /// <summary>
        /// Initialize bot
        /// </summary>
        public void Initialize()
        {
            currentDirection = Random.insideUnitCircle.normalized;
            nextDirectionChangeTime = Time.time + Random.Range(minDirectionChangeTime, maxDirectionChangeTime);
            isSprinting = Random.value < sprintProbability;
            
            patternAngle = 0f;
            patternCenter = transform.position; // Use current position (set by EpisodeManager)
            
            targetHideSpot = Vector2.zero;
            nextHideSpotCheckTime = Time.time + hideSpotCheckInterval;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[TrainingBot] Initialized as {botType} at {transform.position}</color>");
            }
        }

        /// <summary>
        /// Reset to random position (for episode start)
        /// NOTE: EpisodeManager handles spawn positioning, this just reinitializes behavior
        /// </summary>
        public void ResetToRandomPosition()
        {
            // EpisodeManager already set position, just reinitialize
            Initialize();
        }

        #region Bot Behaviors

        /// <summary>
        /// Random Bot: Random direction changes
        /// </summary>
        private void UpdateRandomBot()
        {
            // Change direction periodically
            if (Time.time >= nextDirectionChangeTime)
            {
                currentDirection = Random.insideUnitCircle.normalized;
                nextDirectionChangeTime = Time.time + Random.Range(minDirectionChangeTime, maxDirectionChangeTime);
                isSprinting = Random.value < sprintProbability;
                
                if (showDebugLogs)
                {
                    Debug.Log($"<color=yellow>[RandomBot] New direction: {currentDirection}, sprint: {isSprinting}</color>");
                }
            }
            
            // Move
            float speed = isSprinting ? sprintSpeed : moveSpeed;
            rb.linearVelocity = currentDirection * speed;
        }

        /// <summary>
        /// Pattern Bot: Follows geometric pattern
        /// </summary>
        private void UpdatePatternBot()
        {
            switch (patternType)
            {
                case PatternType.Circle:
                    UpdateCirclePattern();
                    break;
                    
                case PatternType.Square:
                    UpdateSquarePattern();
                    break;
            }
        }

        /// <summary>
        /// Circle pattern movement
        /// </summary>
        private void UpdateCirclePattern()
        {
            // Update angle
            float angularSpeed = patternSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
            patternAngle += angularSpeed;
            
            // Calculate target position on circle
            Vector2 offset = new Vector2(
                Mathf.Cos(patternAngle) * patternRadius,
                Mathf.Sin(patternAngle) * patternRadius
            );
            
            Vector2 targetPos = patternCenter + offset;
            
            // Move towards target
            Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
            float speed = isSprinting ? sprintSpeed : moveSpeed;
            rb.linearVelocity = direction * speed;
        }

        /// <summary>
        /// Square pattern movement
        /// </summary>
        private void UpdateSquarePattern()
        {
            // Determine which side of square (0-3)
            int side = Mathf.FloorToInt(patternAngle / 90f) % 4;
            
            Vector2 targetPos = patternCenter;
            
            switch (side)
            {
                case 0: // Right
                    targetPos += new Vector2(patternRadius, 0);
                    break;
                case 1: // Up
                    targetPos += new Vector2(0, patternRadius);
                    break;
                case 2: // Left
                    targetPos += new Vector2(-patternRadius, 0);
                    break;
                case 3: // Down
                    targetPos += new Vector2(0, -patternRadius);
                    break;
            }
            
            // Move towards corner
            Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
            float speed = isSprinting ? sprintSpeed : moveSpeed;
            rb.linearVelocity = direction * speed;
            
            // Check if reached corner
            if (Vector2.Distance(transform.position, targetPos) < 0.5f)
            {
                patternAngle += 90f; // Next corner
            }
        }

        /// <summary>
        /// Hide Bot: Runs to nearest hide spot
        /// </summary>
        private void UpdateHideBot()
        {
            // Check for new hide spot periodically
            if (Time.time >= nextHideSpotCheckTime)
            {
                FindNearestHideSpot();
                nextHideSpotCheckTime = Time.time + hideSpotCheckInterval;
            }
            
            // Move to target hide spot
            if (targetHideSpot != Vector2.zero)
            {
                Vector2 direction = (targetHideSpot - (Vector2)transform.position).normalized;
                float speed = isSprinting ? sprintSpeed : moveSpeed;
                rb.linearVelocity = direction * speed;
                
                // Check if reached hide spot
                if (Vector2.Distance(transform.position, targetHideSpot) < 1f)
                {
                    // Stay briefly, then find new spot
                    targetHideSpot = Vector2.zero;
                }
            }
            else
            {
                // No hide spot ? Random movement
                UpdateRandomBot();
            }
        }

        /// <summary>
        /// Find nearest hide spot
        /// </summary>
        private void FindNearestHideSpot()
        {
            if (!AITest.World.WorldRegistry.Instance)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[HideBot] WorldRegistry not found!");
                return;
            }
            
            var allHideSpots = AITest.World.WorldRegistry.Instance.GetAllHideSpots();
            
            if (allHideSpots == null || allHideSpots.Count == 0)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[HideBot] No hide spots found!");
                return;
            }
            
            // Find nearest hide spot
            AITest.World.HideSpot nearestSpot = null;
            float minDistance = float.MaxValue;
            
            foreach (var spot in allHideSpots)
            {
                if (!spot) continue;
                
                float dist = Vector2.Distance(transform.position, spot.Position);
                
                if (dist < minDistance && dist < maxHideSpotDistance)
                {
                    minDistance = dist;
                    nearestSpot = spot;
                }
            }
            
            if (nearestSpot != null)
            {
                targetHideSpot = nearestSpot.Position;
                isSprinting = Random.value < 0.7f; // 70% chance to sprint towards hide spot
                
                if (showDebugLogs)
                {
                    Debug.Log($"<color=green>[HideBot] Found hide spot at {targetHideSpot}, distance: {minDistance:F2}m</color>");
                }
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Apply movement bounds
        /// </summary>
        private void ApplyBounds()
        {
            Vector2 pos = transform.position;
            
            pos.x = Mathf.Clamp(pos.x, movementBounds.min.x, movementBounds.max.x);
            pos.y = Mathf.Clamp(pos.y, movementBounds.min.y, movementBounds.max.y);
            
            transform.position = pos;
        }

        /// <summary>
        /// Change bot type at runtime
        /// </summary>
        public void SetBotType(BotType type)
        {
            botType = type;
            Initialize();
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[TrainingBot] Changed to {type}</color>");
            }
        }

        /// <summary>
        /// Get current speed
        /// </summary>
        public float GetCurrentSpeed()
        {
            return rb.linearVelocity.magnitude;
        }

        /// <summary>
        /// Is bot sprinting?
        /// </summary>
        public bool IsSprinting()
        {
            return isSprinting;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // Draw movement bounds
            if (useBounds)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(movementBounds.center, movementBounds.size);
            }
            
            // Draw current direction
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, currentDirection * 2f);
            
            // Pattern bot: Draw pattern
            if (botType == BotType.Pattern && Application.isPlaying)
            {
                Gizmos.color = Color.green;
                
                if (patternType == PatternType.Circle)
                {
                    // Draw circle
                    DrawCircleGizmo(patternCenter, patternRadius, 32);
                }
                else if (patternType == PatternType.Square)
                {
                    // Draw square
                    Vector2 topLeft = patternCenter + new Vector2(-patternRadius, patternRadius);
                    Vector2 topRight = patternCenter + new Vector2(patternRadius, patternRadius);
                    Vector2 bottomRight = patternCenter + new Vector2(patternRadius, -patternRadius);
                    Vector2 bottomLeft = patternCenter + new Vector2(-patternRadius, -patternRadius);
                    
                    Gizmos.DrawLine(topLeft, topRight);
                    Gizmos.DrawLine(topRight, bottomRight);
                    Gizmos.DrawLine(bottomRight, bottomLeft);
                    Gizmos.DrawLine(bottomLeft, topLeft);
                }
            }
            
            // Hide bot: Draw target
            if (botType == BotType.Hide && targetHideSpot != Vector2.zero)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, targetHideSpot);
                Gizmos.DrawWireSphere(targetHideSpot, 0.5f);
            }
        }

        private void DrawCircleGizmo(Vector2 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector2 prevPoint = center + new Vector2(radius, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 newPoint = center + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
                
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        #endregion
    }
}
