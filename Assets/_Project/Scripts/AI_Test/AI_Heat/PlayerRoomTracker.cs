using UnityEngine;

namespace AITest.Heat
{
    /// <summary>
    /// Player Room Tracker - Detects room transitions and reports to TransitionHeatGraph
    /// 
    /// Attach to: Player GameObject
    /// Requires: Collider2D (for trigger detection)
    /// 
    /// Heat Weight Rules:
    /// - Normal walk: 1.0x
    /// - Sprint: 2.0x
    /// - Interaction: Adds node heat to current room
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerRoomTracker : MonoBehaviour
    {
        [Header("Sprint Detection")]
        [Tooltip("PlayerHideController or PlayerTrainingBotController component")]
        public MonoBehaviour playerController;
        
        [Tooltip("Property/field name for sprint status (e.g., 'currentLocomotion' or 'isSprinting')")]
        public string sprintPropertyName = "currentLocomotion";
        
        [Header("Node Heat Tracking")]
        [Tooltip("Add node heat to current room every frame (player dwelling)")]
        public bool trackNodeHeat = true;
        
        [Tooltip("Node heat per frame when player is dwelling in room")]
        [Range(0.001f, 0.1f)]
        public float nodeHeatPerFrame = 0.01f;
        
        [Tooltip("Track player interactions with quest objects")]
        public bool trackInteractions = true;
        
        [Header("Debounce")]
        [Tooltip("Minimum time between room changes (seconds)")]
        [Range(0.1f, 2f)]
        public float roomChangeDebounce = 0.5f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // State tracking
        private string currentRoomId = null;
        private string previousRoomId = null;
        private float lastRoomChangeTime = -999f;
        
        // Reflection cache for sprint detection
        private System.Reflection.PropertyInfo sprintProperty;
        private System.Reflection.FieldInfo sprintField;
        
        private void Awake()
        {
            // Auto-find player controller if not set
            if (playerController == null)
            {
                playerController = GetComponent<MonoBehaviour>();
            }
            
            // Cache reflection for sprint detection
            if (playerController != null && !string.IsNullOrEmpty(sprintPropertyName))
            {
                var type = playerController.GetType();
                sprintProperty = type.GetProperty(sprintPropertyName);
                sprintField = type.GetField(sprintPropertyName);
            }
        }

        private void Update()
        {
            // ✅ Track node heat: Add heat to current room every frame (player dwelling)
            if (trackNodeHeat && !string.IsNullOrEmpty(currentRoomId) && TransitionHeatGraph.Instance != null)
            {
                TransitionHeatGraph.Instance.AddNodeHeat(currentRoomId, nodeHeatPerFrame);
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if this is a room trigger
            var roomTrigger = other.GetComponent<AITest.World.RoomZone>();
            if (roomTrigger == null)
                return;
            
            string newRoomId = roomTrigger.roomId;
            
            // Debounce - prevent rapid room changes
            if (Time.time - lastRoomChangeTime < roomChangeDebounce)
                return;
            
            // Ignore if same room
            if (newRoomId == currentRoomId)
                return;
            
            // Record transition
            if (currentRoomId != null && newRoomId != null)
            {
                RecordTransition(currentRoomId, newRoomId);
            }
            
            // Update state
            previousRoomId = currentRoomId;
            currentRoomId = newRoomId;
            lastRoomChangeTime = Time.time;
            
            if (showDebugLogs)
                Debug.Log($"<color=cyan>[PlayerRoomTracker] Entered room: {currentRoomId}</color>");
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            // Optional: Track room exits
            var roomTrigger = other.GetComponent<AITest.World.RoomZone>();
            if (roomTrigger == null)
                return;
            
            // Clear current room when exiting (transitioning to corridor/next room)
            if (roomTrigger.roomId == currentRoomId)
            {
                previousRoomId = currentRoomId;
                currentRoomId = null;
            }
        }
        
        private void RecordTransition(string fromRoom, string toRoom)
        {
            if (TransitionHeatGraph.Instance == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[PlayerRoomTracker] TransitionHeatGraph not found in scene!");
                return;
            }
            
            // Determine weight based on player state
            float weight = GetTransitionWeight();
            
            // Record transition
            TransitionHeatGraph.Instance.AddTransitionHeat(fromRoom, toRoom, weight);
        }
        
        private float GetTransitionWeight()
        {
            float baseWeight = TransitionHeatGraph.Instance?.walkWeight ?? 1.0f;
            
            // Check if sprinting
            if (IsSprinting())
            {
                float sprintMultiplier = TransitionHeatGraph.Instance?.sprintWeightMultiplier ?? 2.0f;
                return baseWeight * sprintMultiplier;
            }
            
            return baseWeight;
        }
        
        private bool IsSprinting()
        {
            if (playerController == null)
                return false;
            
            try
            {
                // Try property first
                if (sprintProperty != null)
                {
                    var value = sprintProperty.GetValue(playerController);
                    
                    // Check if it's an enum (e.g., LocomotionMode.Sprint)
                    if (value is System.Enum)
                    {
                        return value.ToString().Contains("Sprint");
                    }
                    
                    // Check if it's a bool
                    if (value is bool)
                    {
                        return (bool)value;
                    }
                }
                
                // Try field
                if (sprintField != null)
                {
                    var value = sprintField.GetValue(playerController);
                    
                    if (value is System.Enum)
                    {
                        return value.ToString().Contains("Sprint");
                    }
                    
                    if (value is bool)
                    {
                        return (bool)value;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerRoomTracker] Failed to read sprint status: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Call this when player interacts with quest objects
        /// </summary>
        public void RecordInteraction()
        {
            if (!trackInteractions || currentRoomId == null)
                return;
            
            if (TransitionHeatGraph.Instance == null)
                return;
            
            float interactionBoost = TransitionHeatGraph.Instance.interactionNodeBoost;
            TransitionHeatGraph.Instance.AddNodeHeat(currentRoomId, interactionBoost);
            
            if (showDebugLogs)
                Debug.Log($"[PlayerRoomTracker] Interaction in room: {currentRoomId} (+{interactionBoost})");
        }
        
        /// <summary>
        /// Get current room ID (public accessor for debugging)
        /// </summary>
        public string GetCurrentRoom()
        {
            return currentRoomId;
        }
    }
}
