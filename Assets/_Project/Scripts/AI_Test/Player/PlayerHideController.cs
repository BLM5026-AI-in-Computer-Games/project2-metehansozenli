using UnityEngine;
using AITest.World;
using AITest.Core;

namespace AITest.Player
{
    /// <summary>
    /// Player Hide Controller - Player hiding mechanics
    /// 
    /// PROMPT 13: Full hiding system
    /// - Enter/exit hide spots with interact key
    /// - Movement reduction while hiding
    /// - Visibility suppression (enemy vision fails)
    /// - Noise profile changes (movement suppressed)
    /// - Exit noise emission (optional)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerHideController : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Key to enter/exit hide spots")]
        public KeyCode interactKey = KeyCode.E;
        
        [Tooltip("Maximum distance to interact with hide spot")]
        [Range(1f, 3f)] public float interactRange = 2f;
        
        [Header("Hiding Behavior")]
        [Tooltip("Movement speed multiplier while hiding")]
        [Range(0f, 1f)] public float hiddenMovementMultiplier = 0f; // 0 = no movement
        
        [Tooltip("Block all input while hiding")]
        public bool blockInputWhileHiding = true; // ⚡ NEW: Completely block movement
        
        [Tooltip("Emit noise when exiting hide spot")]
        public bool emitExitNoise = true;
        
        [Tooltip("Exit noise radius")]
        [Range(5f, 15f)] public float exitNoiseRadius = 10f;
        
        [Header("State")]
        [Tooltip("Currently hiding in a hide spot")]
        public bool IsHiding = false;
        
        [Tooltip("Current hide spot (null if not hiding)")]
        public HideSpot CurrentHideSpot = null;
        
        [Header("Debug")]
        public bool showDebugLogs = true;
        public bool showDebugGizmos = true;
        
        // Components
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer; // ⚡ Player görünürlük kontrolü
        private MonoBehaviour playerMovement; // Generic reference (optional)
        
        // Interaction state
        private HideSpot nearestHideSpot = null;
        private float originalMoveSpeed = 5f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>(); // ⚡ Sprite renderer ref
            
            // Try to find any movement component (optional)
            playerMovement = GetComponent<MonoBehaviour>(); // Placeholder
            
            // Store original speed if available via reflection (optional)
            if (playerMovement != null)
            {
                var speedField = playerMovement.GetType().GetField("moveSpeed");
                if (speedField != null)
                {
                    originalMoveSpeed = (float)speedField.GetValue(playerMovement);
                }
            }
        }

        private void Update()
        {
            // ⚡ CRITICAL: Block ALL movement if hiding!
            if (IsHiding && blockInputWhileHiding)
            {
                // Force freeze rigidbody
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }
            
            // ⚡ Find nearest hide spot (only if NOT hiding)
            if (!IsHiding)
            {
                UpdateNearestHideSpot();
            }
            
            // ⚡ Check interact input (E key always works)
            if (Input.GetKeyDown(interactKey))
            {
                if (IsHiding)
                {
                    ExitHideSpot();
                }
                else
                {
                    TryEnterHideSpot();
                }
            }
        }

        /// <summary>
        /// Find nearest interactable hide spot
        /// </summary>
        private void UpdateNearestHideSpot()
        {
            if (IsHiding)
            {
                nearestHideSpot = null;
                return;
            }
            
            if (!WorldRegistry.Instance)
            {
                nearestHideSpot = null;
                return;
            }
            
            var allHideSpots = WorldRegistry.Instance.GetAllHideSpots();
            
            HideSpot closest = null;
            float closestDist = interactRange;
            
            foreach (var spot in allHideSpots)
            {
                if (!spot || !spot.CanContainPlayer)
                    continue;
                
                if (spot.IsOccupied)
                    continue; // Already occupied
                
                float dist = Vector2.Distance(transform.position, spot.Position);
                
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = spot;
                }
            }
            
            nearestHideSpot = closest;
        }

        /// <summary>
        /// ⚡ PROMPT 13: Try enter hide spot
        /// </summary>
        private void TryEnterHideSpot()
        {
            if (nearestHideSpot == null)
            {
                if (showDebugLogs)
                    Debug.Log("[PlayerHide] No hide spot nearby");
                
                return;
            }
            
            // ⚡ Attempt to enter
            if (nearestHideSpot.TryEnter(gameObject))
            {
                IsHiding = true;
                CurrentHideSpot = nearestHideSpot;
                
                // ⚡ Snap to hide anchor
                if (nearestHideSpot.HideAnchor != null)
                {
                    transform.position = nearestHideSpot.HideAnchor.position;
                }
                
                // ⚡ HIDE PLAYER SPRITE (invisible)
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }
                
                // ⚡ Reduce movement speed
                if (playerMovement)
                {
                    var speedField = playerMovement.GetType().GetField("moveSpeed");
                    if (speedField != null)
                    {
                        speedField.SetValue(playerMovement, originalMoveSpeed * hiddenMovementMultiplier);
                    }
                }
                
                if (showDebugLogs)
                    Debug.Log($"<color=cyan>[PlayerHide] Entered hide spot: {nearestHideSpot.spotId} (INVISIBLE)</color>");
            }
            else
            {
                if (showDebugLogs)
                    Debug.Log("[PlayerHide] Failed to enter hide spot (occupied or disabled)");
            }
        }

        /// <summary>
        /// ⚡ PROMPT 13: Exit hide spot
        /// </summary>
        private void ExitHideSpot()
        {
            if (!IsHiding || CurrentHideSpot == null)
                return;
            
            // ⚡ Exit hide spot
            CurrentHideSpot.Exit(gameObject);
            
            // ⚡ SHOW PLAYER SPRITE (visible again)
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            
            // ⚡ Emit exit noise (optional)
            if (emitExitNoise)
            {
                EmitExitNoise();
            }
            
            // ⚡ Restore movement speed
            if (playerMovement)
            {
                var speedField = playerMovement.GetType().GetField("moveSpeed");
                if (speedField != null)
                {
                    speedField.SetValue(playerMovement, originalMoveSpeed);
                }
            }
            
            if (showDebugLogs)
                Debug.Log($"<color=yellow>[PlayerHide] Exited hide spot: {CurrentHideSpot.spotId} (VISIBLE)</color>");
            
            IsHiding = false;
            CurrentHideSpot = null;
        }

        /// <summary>
        /// Emit noise when exiting (rustle sound)
        /// </summary>
        private void EmitExitNoise()
        {
            if (!NoiseBus.Instance)
                return;
            
            // Get current room
            string roomId = "None";
            if (WorldRegistry.Instance && WorldRegistry.Instance.TryGetRoomAtPosition(transform.position, out string room))
            {
                roomId = room;
            }
            
            // ? Emit noise event
            NoiseBus.Instance.Emit(
                transform.position,
                exitNoiseRadius,
                roomId,
                isGlobal: false
            );
            
            if (showDebugLogs)
                Debug.Log($"<color=orange>[PlayerHide] Exit noise emitted (radius={exitNoiseRadius}m)</color>");
        }

        /// <summary>
        /// Get visibility modifier for enemy vision
        /// </summary>
        public float GetVisibilityModifier()
        {
            if (!IsHiding)
                return 1f; // Normal visibility
            
            // ? Hidden: visibility suppressed
            return 0f; // Completely hidden (unless revealed by CanRevealToEnemy)
        }

        /// <summary>
        /// Get noise modifier for player movement
        /// </summary>
        public float GetNoiseModifier()
        {
            if (!IsHiding)
                return 1f; // Normal noise
            
            // ? Hidden: movement noise suppressed
            return 0f; // Silent movement while hiding
        }

        /// <summary>
        /// Can enemy reveal player in current hide spot?
        /// </summary>
        public bool CanBeRevealedByEnemy(Vector2 enemyPos, LayerMask obstaclesMask)
        {
            if (!IsHiding || CurrentHideSpot == null)
                return true; // Not hiding = always visible
            
            // ? Check if hide spot can reveal to enemy
            return CurrentHideSpot.CanRevealToEnemy(enemyPos, obstaclesMask);
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos)
                return;
            
            // Draw interact range
            Gizmos.color = IsHiding ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
            
            // Draw line to nearest hide spot
            if (nearestHideSpot != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, nearestHideSpot.Position);
            }
            
            // Draw current hide spot
            if (IsHiding && CurrentHideSpot != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(CurrentHideSpot.Position, 1.5f);
            }
        }
    }
}
