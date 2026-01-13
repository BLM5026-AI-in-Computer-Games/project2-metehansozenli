using UnityEngine;
using AITest.WorldModel;
using AITest.World;
using AITest.Heat;

namespace AITest.Learning
{
    /// <summary>
    /// State Extractor - Convert WorldModel to discrete RLStateKey
    /// 
    /// PROMPT 5: Feature extraction with discretization
    /// - Binary features: see/hear/clue flags (with hysteresis)
    /// - Time bins: exponential bucketing (0-2s, 2-6s, 6-15s, 15+s)
    /// - Distance bins: close/medium/far
    /// - Heatmap confidence: low/med/high
    /// - Search progress: early/mid/late
    /// </summary>
    public class StateExtractor : MonoBehaviour
    {
        [Header("References")]
        public EnemyWorldModel worldModel;
        public Transform enemyTransform;
        
        [Header("Time Thresholds (seconds)")]
        [Tooltip("Bin 0: 0-X seconds")]
        public float timeBin0Threshold = 2f;
        
        [Tooltip("Bin 1: X-Y seconds")]
        public float timeBin1Threshold = 6f;
        
        [Tooltip("Bin 2: Y-Z seconds")]
        public float timeBin2Threshold = 15f;
        // Bin 3: Z+ seconds (implicit)
        
        [Header("Distance Thresholds (meters)")]
        [Tooltip("Close threshold")]
        public float distCloseThreshold = 10f;
        
        [Tooltip("Medium threshold")]
        public float distMediumThreshold = 20f;
        // Far = beyond medium
        
        [Header("Heatmap Thresholds")]
        [Tooltip("Low confidence threshold")]
        [Range(0f, 1f)] public float heatLowThreshold = 0.3f;
        
        [Tooltip("High confidence threshold")]
        [Range(0f, 1f)] public float heatHighThreshold = 0.7f;
        
        [Header("Hide Spot Detection")]
        [Tooltip("Distance to consider 'near' hide spots")]
        public float hideSpotNearRadius = 5f;
        
        [Header("Search Progress")]
        [Tooltip("Total search time (for progress calculation)")]
        public float totalSearchTime = 60f; // 1 minute mission
        
        [Header("Hysteresis (Noise Reduction)")]
        [Tooltip("Enable hysteresis for binary features")]
        public bool enableHysteresis = true;
        
        [Tooltip("Recent memory threshold (seconds)")]
        public float recentThreshold = 20f;
        
        [Tooltip("Hysteresis buffer (seconds) - prevents flapping")]
        public float hysteresisBuffer = 2f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Hysteresis state tracking
        private bool lastHearRecently = false;
        private bool lastClueRecently = false;
        private float lastHearStateChangeTime = -999f;
        private float lastClueStateChangeTime = -999f;
        
        // Last action tracking
        private int lastActionValue = 0; // Default: GoToLastSeen
        
        // Session start time
        private float sessionStartTime;

        private void Awake()
        {
            // Auto-find components
            if (!worldModel)
                worldModel = GetComponent<EnemyWorldModel>();
            
            if (!enemyTransform)
                enemyTransform = transform;
            
            sessionStartTime = Time.time;
        }

        /// <summary>
        /// ? PROMPT 5: Extract discrete state from WorldModel
        /// </summary>
        public RLStateKey ExtractState()
        {
            RLStateKey state = new RLStateKey();
            
            if (!worldModel)
            {
                Debug.LogWarning("[StateExtractor] WorldModel not found! Returning zero state.");
                return state;
            }
            
            // 1. SEE PLAYER (binary)
            state.seePlayer = (byte)(worldModel.SeePlayerNow ? 1 : 0);
            
            // 2. HEAR RECENTLY (binary with hysteresis)
            state.hearRecently = (byte)(GetHearRecentlyWithHysteresis() ? 1 : 0);
            
            // 3. CLUE RECENTLY (binary with hysteresis)
            state.clueRecently = (byte)(GetClueRecentlyWithHysteresis() ? 1 : 0);
            
            // 4. TIME SINCE SEEN (4 bins)
            state.timeSinceSeenBin = (byte)GetTimeBin(worldModel.TimeSinceSeen);
            
            // 5. TIME SINCE HEARD (4 bins)
            state.timeSinceHeardBin = (byte)GetTimeBin(worldModel.TimeSinceHeard);
            
            // 6. HEAT CONFIDENCE (3 bins: low/med/high)
            float heatVal01 = 0f;
            if (TransitionHeatGraph.Instance)
            {
                float peakVal = TransitionHeatGraph.Instance.GetPeakValue();
                float cap = Mathf.Max(TransitionHeatGraph.Instance.maxHeatCap, 1f);
                heatVal01 = Mathf.Clamp01(peakVal / cap);
            }
            state.heatConfidence = (byte)GetHeatConfidenceBin(heatVal01);
            
            // 7. DIST TO HEAT PEAK (3 bins: close/medium/far)
            Vector2 peakPos = Vector2.zero;
            if (TransitionHeatGraph.Instance && WorldRegistry.Instance)
            {
                string peakRoom = TransitionHeatGraph.Instance.GetPeakRoom();
                var room = WorldRegistry.Instance.GetRoom(peakRoom);
                if (room != null)
                    peakPos = room.Center;
            }
            state.distToHeatPeakBin = (byte)GetDistanceBin(peakPos);
            
            // 8. DIST TO LAST HEARD (3 bins)
            state.distToLastHeardBin = (byte)GetDistanceBin(worldModel.LastHeardPos);
            
            // 9. IN SAME ROOM AS HEAT PEAK (binary)
            string peakRoomId = TransitionHeatGraph.Instance ? TransitionHeatGraph.Instance.GetPeakRoom() : "None";
            state.inSameRoomAsHeatPeak = (byte)(IsInSameRoomAs(peakRoomId) ? 1 : 0);
            
            // 10. NEAR HIDE SPOTS (binary)
            state.nearHideSpots = (byte)(IsNearHideSpots() ? 1 : 0);
            
            // 11. SEARCH PROGRESS (3 bins: early/mid/late)
            state.searchProgressBin = (byte)GetSearchProgressBin();
            
            // 12. LAST ACTION (6 values: 0-5)
            state.lastAction = (byte)lastActionValue;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[StateExtractor] State: {state}</color>");
            }
            
            return state;
        }

        /// <summary>
        /// Set last action (called by EnemyBrain after action execution)
        /// </summary>
        public void SetLastAction(AITest.Enemy.EnemyMode mode)
        {
            // Map EnemyMode to 0-5
            lastActionValue = (int)mode;
        }

        /// <summary>
        /// Get time bin (0-3)
        /// 0: 0-2s (very recent)
        /// 1: 2-6s (recent)
        /// 2: 6-15s (old)
        /// 3: 15+s (very old / none)
        /// </summary>
        private int GetTimeBin(float time)
        {
            if (time < timeBin0Threshold) return 0;
            if (time < timeBin1Threshold) return 1;
            if (time < timeBin2Threshold) return 2;
            return 3;
        }

        /// <summary>
        /// Get heat confidence bin (0-2)
        /// 0: Low (0-0.3)
        /// 1: Medium (0.3-0.7)
        /// 2: High (0.7-1.0)
        /// </summary>
        private int GetHeatConfidenceBin(float heatValue)
        {
            if (heatValue < heatLowThreshold) return 0;   // Low
            if (heatValue < heatHighThreshold) return 1;  // Medium
            return 2;                                     // High
        }

        /// <summary>
        /// Get distance bin (0-2)
        /// 0: Close (0-10m)
        /// 1: Medium (10-20m)
        /// 2: Far (20+m)
        /// </summary>
        private int GetDistanceBin(Vector2 targetPos)
        {
            if (targetPos == Vector2.zero)
                return 2; // No target = far
            
            float dist = Vector2.Distance(enemyTransform.position, targetPos);
            
            if (dist < distCloseThreshold) return 0;    // Close
            if (dist < distMediumThreshold) return 1;   // Medium
            return 2;                                   // Far
        }

        /// <summary>
        /// Check if enemy is in same room as target
        /// </summary>
        private bool IsInSameRoomAs(string targetRoom)
        {
            if (string.IsNullOrEmpty(targetRoom) || targetRoom == "None")
                return false;
            
            if (!WorldRegistry.Instance)
                return false;
            
            // Get enemy's current room
            if (WorldRegistry.Instance.TryGetRoomAtPosition(enemyTransform.position, out string enemyRoom))
            {
                return enemyRoom == targetRoom;
            }
            
            return false;
        }

        /// <summary>
        /// Check if near hide spots (within radius)
        /// </summary>
        private bool IsNearHideSpots()
        {
            if (!WorldRegistry.Instance)
                return false;
            
            var allSpots = WorldRegistry.Instance.GetAllHideSpots();
            
            Vector2 enemyPos = enemyTransform.position;
            
            foreach (var spot in allSpots)
            {
                if (!spot) continue;
                
                float dist = Vector2.Distance(enemyPos, spot.Position);
                
                if (dist < hideSpotNearRadius)
                    return true; // Found a nearby hide spot
            }
            
            return false;
        }

        /// <summary>
        /// Get search progress bin (0-2)
        /// PROMPT 15: Uses QuestManager progress if available, else time-based
        /// 0: Early (0-33%)
        /// 1: Mid (33-66%)
        /// 2: Late (66-100%)
        /// </summary>
        private int GetSearchProgressBin()
        {
            float progress;
            
            // ? PROMPT 15: Try to use QuestManager progress
            if (AITest.Quest.QuestManager.Instance != null)
            {
                progress = AITest.Quest.QuestManager.Instance.Progress01;
            }
            else
            {
                // Fallback: Time-based progress
                float elapsed = Time.time - sessionStartTime;
                progress = elapsed / totalSearchTime;
            }
            
            // Clamp to 0-1
            progress = Mathf.Clamp01(progress);
            
            // Bin into 3 categories
            if (progress < 0.33f) return 0;  // Early
            if (progress < 0.66f) return 1;  // Mid
            return 2;                        // Late
        }

        /// <summary>
        /// ? HYSTERESIS: Hear recently with state change buffer
        /// Prevents rapid flapping between true/false
        /// </summary>
        private bool GetHearRecentlyWithHysteresis()
        {
            if (!enableHysteresis)
            {
                // No hysteresis - direct threshold
                return worldModel.TimeSinceHeard < recentThreshold;
            }
            
            bool rawValue = worldModel.TimeSinceHeard < recentThreshold;
            
            // State change detection
            if (rawValue != lastHearRecently)
            {
                // Check buffer time (avoid rapid flapping)
                float timeSinceLastChange = Time.time - lastHearStateChangeTime;
                
                if (timeSinceLastChange > hysteresisBuffer)
                {
                    // Confirmed state change
                    lastHearRecently = rawValue;
                    lastHearStateChangeTime = Time.time;
                }
                // Else: Ignore (too soon after last change)
            }
            
            return lastHearRecently;
        }

        /// <summary>
        /// ? HYSTERESIS: Clue recently with state change buffer
        /// </summary>
        private bool GetClueRecentlyWithHysteresis()
        {
            if (!enableHysteresis)
            {
                return worldModel.TimeSinceClue < recentThreshold;
            }
            
            bool rawValue = worldModel.TimeSinceClue < recentThreshold;
            
            if (rawValue != lastClueRecently)
            {
                float timeSinceLastChange = Time.time - lastClueStateChangeTime;
                
                if (timeSinceLastChange > hysteresisBuffer)
                {
                    lastClueRecently = rawValue;
                    lastClueStateChangeTime = Time.time;
                }
            }
            
            return lastClueRecently;
        }

        /// <summary>
        /// Reset hysteresis state (for testing)
        /// </summary>
        public void ResetHysteresis()
        {
            lastHearRecently = false;
            lastClueRecently = false;
            lastHearStateChangeTime = -999f;
            lastClueStateChangeTime = -999f;
            
            Debug.Log("<color=yellow>[StateExtractor] Hysteresis reset</color>");
        }

        /// <summary>
        /// Get state space size (for debugging)
        /// </summary>
        public int GetStateSpaceSize()
        {
            // 2 * 2 * 2 * 4 * 4 * 3 * 3 * 3 * 2 * 2 * 3 * 6 = 497,664
            return 2 * 2 * 2 * 4 * 4 * 3 * 3 * 3 * 2 * 2 * 3 * 6;
        }
    }
}
