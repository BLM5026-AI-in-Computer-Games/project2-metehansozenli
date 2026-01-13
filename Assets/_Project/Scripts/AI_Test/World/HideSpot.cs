using UnityEngine;

namespace AITest.World
{
    /// <summary>
    /// Hide spot component - Tracks hiding location with Bayesian learning
    /// 
    /// USAGE:
    /// 1. Create GameObject at hiding location (e.g., "HideSpot_A1")
    /// 2. Add CircleCollider2D (Trigger, Radius: 1.5m)
    /// 3. Attach this script
    /// 4. Set roomId and spotIndex
    /// 5. WorldRegistry will auto-register on Awake
    /// 
    /// BAYESIAN LEARNING:
    /// - Tracks: times checked, times player found
    /// - Probability = (found + 1) / (checked + 2) (Laplace smoothing)
    /// - Used by enemy AI to prioritize hiding spots
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HideSpot : MonoBehaviour
    {
        [Header("Identification")]
        [Tooltip("Room ID this hide spot belongs to (e.g., A, B)")]
        public string roomId = "A";
        
        [Tooltip("Spot index within room (0-based)")]
        public int spotIndex = 0;
        
        [Header("Hide Spot Properties")]
        [Tooltip("Type of hiding spot (visual hint)")]
        public HideSpotType spotType = HideSpotType.Generic;
        
        [Tooltip("How long enemy should check this spot (seconds)")]
        [Range(0.5f, 5f)]
        public float checkDuration = 1.5f;
        
        [Header("Learning Data (Runtime)")]
        [Tooltip("Total times enemy checked this spot")]
        [SerializeField] private int timesChecked = 0;
        
        [Tooltip("Times player was found here")]
        [SerializeField] private int timesFoundPlayer = 0;

        [Tooltip("Times player used this spot (Hidden Here)")]
        [SerializeField] private int timesUsed = 0;
        
        [Tooltip("Last time this spot was checked (Time.time)")]
        [SerializeField] private float lastCheckedTime = -999f;
        
        [Header("Player Hiding (PROMPT 13)")]
        [Tooltip("Can this spot contain a hiding player?")]
        public bool CanContainPlayer = true;
        
        [Tooltip("Where player snaps when hiding (null = this.transform)")]
        public Transform HideAnchor = null;
        
        [Tooltip("Enemy can detect hiding player within this distance + LOS")]
        [Range(1f, 5f)] public float RevealDistance = 1.0f; // ? Changed from 2f to 1f
        
        [Tooltip("Currently occupied by player")]
        [SerializeField] private GameObject occupant = null;
        
        [Header("Occupancy Tracking (Optional)")]
        [Tooltip("Is player currently hiding here?")]
        [SerializeField] private bool isOccupied = false;
        
        [Tooltip("Has enemy checked this spot? (used to reveal hidden player)")]
        [SerializeField] private bool hasBeenChecked = false;
        
        [Header("Visual Debug")]
        [Tooltip("Show spot info in Scene view")]
        public bool showDebugInfo = true;

        // Cached components
        private Collider2D spotCollider;
        
        // Public properties
        public Vector2 Position => transform.position;
        public int TimesChecked => timesChecked;
        public int TimesFoundPlayer => timesFoundPlayer;
        public float LastCheckedTime => lastCheckedTime;
        public bool IsOccupied => isOccupied;
        public bool IsPlayerHiding => isOccupied; // Player is hiding if occupied and stationary
        public float TimeSinceLastCheck => Time.time - lastCheckedTime;
        public string spotId => $"{roomId}-{spotIndex}";
        public bool HasBeenChecked => hasBeenChecked;

        /// <summary>
        /// Bayesian probability (Prioritize frequently used spots)
        /// P = (timesUsed + 1) / (timesChecked + 1)
        /// Clamped 0..1 not strictly required for sorting, but good for viz.
        /// </summary>
        public float Probability
        {
            get
            {
                // Numerator: Activity Weight
                // - Finding the player is a HUGE learning event (x5 weight)
                // - Player using it is a Strong event (x2 weight)
                // Denominator: Investigation penalty
                // - Checking empty spots reduces confidence slowly
                
                float numerator = (timesFoundPlayer * 5.0f) + (timesUsed * 2.0f) + 0.5f;
                float denominator = timesChecked + 2.0f;
                
                return numerator / denominator;
            }
        }

        private void Awake()
        {
            // Cache collider
            spotCollider = GetComponent<Collider2D>();
            
            if (!spotCollider)
            {
                Debug.LogError($"[HideSpot] {gameObject.name} - Collider2D not found!");
                return;
            }
            
            // Force trigger mode
            if (!spotCollider.isTrigger)
            {
                spotCollider.isTrigger = true;
                Debug.LogWarning($"[HideSpot] {roomId}-{spotIndex} - Collider was not Trigger! Fixed.");
            }
            
            // Auto-register with WorldRegistry
            if (WorldRegistry.Instance)
            {
                WorldRegistry.Instance.RegisterHideSpot(this);
            }
            else
            {
                Debug.LogWarning($"[HideSpot] {roomId}-{spotIndex} - WorldRegistry not found!");
            }
        }

        private void OnDestroy()
        {
            // Unregister from WorldRegistry
            if (WorldRegistry.Instance)
            {
                WorldRegistry.Instance.UnregisterHideSpot(this);
            }
        }

        /// <summary>
        /// Mark this spot as checked by enemy
        /// </summary>
        /// <param name="playerFound">Was player found here?</param>
        public void MarkChecked(bool playerFound)
        {
            timesChecked++;
            lastCheckedTime = Time.time;
            hasBeenChecked = true; // Mark as checked so player can be revealed
            
            if (playerFound)
            {
                timesFoundPlayer++;
            }
        }
        
        /// <summary>
        /// Enemy interacts with this hide spot (like player pressing E)
        /// Returns true if player is found
        /// </summary>
        public bool Interact()
        {
            Debug.Log($"<color=orange>[HideSpot] {spotId}: Enemy INTERACT!</color>");
            
            // Mark as checked
            bool playerFound = isOccupied;
            MarkChecked(playerFound);
            
            if (playerFound)
            {
                Debug.Log($"<color=red>[HideSpot] {spotId}: ✓ PLAYER CAUGHT while hiding!</color>");
                return true;
            }
            else
            {
                Debug.Log($"<color=yellow>[HideSpot] {spotId}: ✗ Empty (false alarm)</color>");
                return false;
            }
        }

        /// <summary>
        /// Reset learning data (for testing)
        /// </summary>
        public void ResetLearning()
        {
            timesChecked = 0;
            timesFoundPlayer = 0;
            timesUsed = 0;
            lastCheckedTime = -999f;
            Debug.Log($"<color=yellow>[HideSpot] {roomId}-{spotIndex}: Learning data reset</color>");
        }

        /// <summary>
        /// ? PROMPT 13: Try enter hide spot
        /// </summary>
        public bool TryEnter(GameObject player)
        {
            if (!CanContainPlayer)
                return false;
            
            if (isOccupied && occupant != player)
                return false; // Already occupied by someone else
            
            // ? Occupy spot
            isOccupied = true;
            occupant = player;
            hasBeenChecked = false; // Reset check status (enemy must check again to find)
            
            // ? LEARN: Player chose to hide here!
            timesUsed++;
            
            // ? HIDE PLAYER SPRITE (both manual player and NPC)
            // Use GetComponentInChildren to catch child sprites
            var spriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            Debug.Log($"<color=cyan>[HideSpot] {spotId}: Player entered (INVISIBLE) | P={(Probability):F2}</color>");
            return true;
        }

        /// <summary>
        /// ? PROMPT 13: Exit hide spot
        /// </summary>
        public void Exit(GameObject player)
        {
            if (occupant != player)
            {
                // Safety check: only occupant can exit
                // But if force exit is needed, we allow it if player is passed
                if (occupant == null) return; 
            }
            
            // ? Release spot
            isOccupied = false;
            occupant = null;
            hasBeenChecked = false;
            
            // ? SHOW PLAYER SPRITE (both manual player and NPC)
            var spriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            
            Debug.Log($"<color=yellow>[HideSpot] {spotId}: Player exited (VISIBLE)</color>");
        }

        /// <summary>
        /// ? PROMPT 13: Can enemy reveal hidden player?
        /// ONLY if enemy has checked this spot!
        /// </summary>
        public bool CanRevealToEnemy(Vector2 enemyPos, LayerMask obstaclesMask)
        {
            if (!isOccupied)
                return false; // No one hiding
            
            // ? CRITICAL: Enemy must have checked this spot to reveal player!
            if (!hasBeenChecked)
                return false; // Enemy hasn't checked - player stays hidden
            
            // ? CRITICAL: Distance check - Must be VERY close to reveal
            float dist = Vector2.Distance(enemyPos, Position);
            
            // ? Player is FULLY HIDDEN - only reveal if enemy is RIGHT NEXT TO spot
            if (dist > RevealDistance)
                return false; // Too far - player stays hidden
            
            // ? LOS check (must have clear line of sight)
            Vector2 direction = (Position - enemyPos).normalized;
            RaycastHit2D hit = Physics2D.Raycast(enemyPos, direction, dist, obstaclesMask);
            
            if (hit.collider != null)
            {
                // ? Wall blocking - player stays hidden
                return false;
            }
            
            // ? Enemy has checked spot + is RIGHT NEXT to it + clear LOS = REVEALED!
            Debug.Log($"<color=red>[HideSpot] {spotId}: Player REVEALED to enemy (dist={dist:F2}m)</color>");
            return true;
        }

        #region Trigger Callbacks (Occupancy Tracking)

        // REMOVED: OnTriggerStay2D and OnTriggerExit2D auto-occupancy logic.
        // Hiding is now an explicit action via TryEnter/Exit called by PlayerHideController or NPC Bot.

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!spotCollider)
                spotCollider = GetComponent<Collider2D>();
            
            // Probability-based color (green ? yellow ? red)
            float prob = Application.isPlaying ? Probability : 0.33f;
            Color color = Color.Lerp(Color.green, Color.red, prob);
            color.a = 0.5f;
            
            // Occupied indicator
            if (Application.isPlaying && isOccupied)
            {
                color = new Color(1f, 0f, 1f, 0.8f); // Magenta = occupied
            }
            
            Gizmos.color = color;
            
            // Draw spot area
            if (spotCollider is CircleCollider2D circle)
            {
                float radius = circle.radius;
                Gizmos.DrawSphere(transform.position, radius);
                
                // Outline
                Gizmos.color = new Color(color.r, color.g, color.b, 1f);
                Gizmos.DrawWireSphere(transform.position, radius);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1f);
            }
            
            #if UNITY_EDITOR
            // Debug label
            if (showDebugInfo)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 12;
                style.alignment = TextAnchor.MiddleCenter;
                
                string label = $"Hide {roomId}-{spotIndex}";
                
                if (Application.isPlaying)
                {
                    label += $"\nProb: {Probability:P0}";
                    label += $"\n({timesFoundPlayer}/{timesChecked})";
                    
                    if (isOccupied)
                        label += "\n?? OCCUPIED";
                }
                
                Vector3 labelPos = (Vector2)transform.position + Vector2.up * 1.5f;
                UnityEditor.Handles.Label(labelPos, label, style);
            }
            #endif
        }

        #endregion
    }

    /// <summary>
    /// Hide spot types (visual categorization)
    /// </summary>
    public enum HideSpotType
    {
        Generic = 0,     // Default
        Corner = 1,      // Room corner
        Behind = 2,      // Behind furniture
        Shadow = 3,      // Dark area
        Vent = 4,        // Air vent
        Closet = 5       // Closet/cabinet
    }
}
