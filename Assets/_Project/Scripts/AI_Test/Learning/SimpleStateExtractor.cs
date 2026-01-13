using UnityEngine;
using AITest.WorldModel;
using AITest.World;
using AITest.Heat;

namespace AITest.Learning
{
    /// <summary>
    /// Simple State Extractor - MINIMAL MDP for Tabular Q-Learning
    /// 
    /// PROFESSOR FEEDBACK: State space too large for tabular Q-learning.
    /// Solution: Compress to SMALL state space
    /// 
    /// State Features (6 features - UPDATED):
    /// 1. playerVisible: bool (2 values: visible/not visible)
    /// 2. distanceBucket: int (3 values: close/medium/far)
    /// 3. lastSeenSector: int (3 values: same/different/unknown)
    /// 4. timeSinceContactBucket: int (3 values: 0-2s/3-6s/7+s)
    /// 5. heatHereBucket: int (3 values: cold/warm/hot) - NEW
    /// 6. heatNearbyBucket: int (3 values: cold/warm/hot) - NEW
    /// 
    /// State Space Size: 2 × 3 × 3 × 3 × 3 × 3 = 486 states
    /// 
    /// This is still manageable for 500 episodes (~1 visit per state on average).
    /// </summary>
    public class SimpleStateExtractor : MonoBehaviour
    {
        [Header("References")]
        public EnemyWorldModel worldModel;
        public Transform enemyTransform;
        public Transform playerTransform;
        
        [Header("Distance Thresholds")]
        [Tooltip("Close threshold (meters)")]
        public float distCloseThreshold = 8f;
        
        [Tooltip("Far threshold (meters)")]
        public float distFarThreshold = 16f;
        // Medium = between close and far
        
        [Header("Time Thresholds")]
        [Tooltip("Recent contact (0-2 seconds)")]
        public float timeRecentThreshold = 2f;
        
        [Tooltip("Old contact (3-6 seconds)")]
        public float timeOldThreshold = 6f;
        // Very old = 7+ seconds
        
        [Header("Sector Tolerance")]
        [Tooltip("Distance to consider 'same sector' (meters)")]
        public float sameSectorRadius = 5f;
        
        [Header("Heat Thresholds")]
        [Tooltip("Heat threshold for 'warm' bucket")]
        public float heatWarmThreshold = 5f;
        
        [Tooltip("Heat threshold for 'hot' bucket")]
        public float heatHotThreshold = 20f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Last action tracking
        private int lastActionValue = 0;
        
        // Components
        private AITest.Heat.EnemyRoomTracker roomTracker;

        private void Awake()
        {
            // Auto-find components
            if (!worldModel)
                worldModel = GetComponent<EnemyWorldModel>();
            
            if (!enemyTransform)
                enemyTransform = transform;
            
            // Find player (fallback)
            if (!playerTransform)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player)
                    playerTransform = player.transform;
            }
            
            // Find room tracker
            roomTracker = GetComponent<AITest.Heat.EnemyRoomTracker>();
        }

        /// <summary>
        /// Extract SIMPLE state (6 features → 486 states)
        /// </summary>
        public SimpleRLStateKey ExtractState()
        {
            SimpleRLStateKey state = new SimpleRLStateKey();
            
            if (!worldModel)
            {
                Debug.LogWarning("[SimpleStateExtractor] WorldModel not found! Returning zero state.");
                return state;
            }
            
            // 1. PLAYER VISIBLE (binary: 0/1)
            state.playerVisible = (byte)(worldModel.SeePlayerNow ? 1 : 0);
            
            // 2. DISTANCE BUCKET (3 values: 0=close, 1=medium, 2=far)
            state.distanceBucket = (byte)GetDistanceBucket();
            
            // 3. LAST SEEN SECTOR (3 values: 0=same, 1=different, 2=unknown)
            state.lastSeenSector = (byte)GetLastSeenSectorBucket();
            
            // 4. TIME SINCE CONTACT (3 values: 0=recent, 1=old, 2=very old)
            state.timeSinceContactBucket = (byte)GetTimeSinceContactBucket();
            
            // 5. HEAT HERE BUCKET (3 values: 0=cold, 1=warm, 2=hot) - NEW
            state.heatHereBucket = (byte)GetHeatHereBucket();
            
            // 6. HEAT NEARBY BUCKET (3 values: 0=cold, 1=warm, 2=hot) - NEW
            state.heatNearbyBucket = (byte)GetHeatNearbyBucket();
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[SimpleStateExtractor] State: visible={state.playerVisible}, " +
                          $"dist={state.distanceBucket}, sector={state.lastSeenSector}, time={state.timeSinceContactBucket} " +
                          $"(key={state.GetHashKey()})</color>");
            }
            
            return state;
        }

        /// <summary>
        /// Set last action (for future use)
        /// </summary>
        public void SetLastAction(AITest.Enemy.EnemyMode mode)
        {
            lastActionValue = (int)mode;
        }

        /// <summary>
        /// Get distance bucket (0-2)
        /// 0: Close (0-8m) - Player is nearby
        /// 1: Medium (8-16m) - Player is at medium distance
        /// 2: Far (16+m) - Player is far away or unknown
        /// </summary>
        private int GetDistanceBucket()
        {
            Vector2 targetPos = Vector2.zero;
            
            // Determine target position (priority: see > heard > heatmap)
            if (worldModel.SeePlayerNow && playerTransform)
            {
                targetPos = playerTransform.position;
            }
            else if (worldModel.TimeSinceHeard < 10f && worldModel.LastHeardPos != Vector2.zero)
            {
                targetPos = worldModel.LastHeardPos;
            }
            else
            {
                // Fallback: center of hottest room by node heat
                targetPos = Vector2.zero;
                if (TransitionHeatGraph.Instance && AITest.World.WorldRegistry.Instance)
                {
                    string peakRoom = TransitionHeatGraph.Instance.GetPeakRoom();
                    var room = AITest.World.WorldRegistry.Instance.GetRoom(peakRoom);
                    if (room != null)
                        targetPos = room.Center;
                }
            }
            // If no target found, consider it far
            if (targetPos == Vector2.zero)
                return 2;
            
            float dist = Vector2.Distance(enemyTransform.position, targetPos);
            
            if (dist < distCloseThreshold) return 0;    // Close
            if (dist < distFarThreshold) return 1;      // Medium
            return 2;                                   // Far
        }

        /// <summary>
        /// Get last seen sector bucket (0-2)
        /// 0: Same sector - Enemy is in the same area as last known player position
        /// 1: Different sector - Enemy is in a different area
        /// 2: Unknown - No recent contact (7+ seconds)
        /// </summary>
        private int GetLastSeenSectorBucket()
        {
            // If no recent contact ? Unknown
            float timeSinceAnyContact = Mathf.Min(worldModel.TimeSinceSeen, worldModel.TimeSinceHeard);
            
            if (timeSinceAnyContact > 7f)
                return 2; // Unknown
            
            // Get last known position
            Vector2 lastKnownPos = Vector2.zero;
            
            if (worldModel.TimeSinceSeen < worldModel.TimeSinceHeard)
            {
                lastKnownPos = worldModel.LastSeenPos;
            }
            else
            {
                lastKnownPos = worldModel.LastHeardPos;
            }
            
            if (lastKnownPos == Vector2.zero)
                return 2; // Unknown
            
            // Check if in same sector
            float dist = Vector2.Distance(enemyTransform.position, lastKnownPos);
            
            if (dist < sameSectorRadius)
                return 0; // Same sector
            else
                return 1; // Different sector
        }

        /// <summary>
        /// Get time since contact bucket (0-2)
        /// 0: Recent (0-2 seconds) - Just saw/heard player
        /// 1: Old (3-6 seconds) - Some time has passed
        /// 2: Very old (7+ seconds) - Long time since contact
        /// </summary>
        private int GetTimeSinceContactBucket()
        {
            // Use the MINIMUM time (most recent contact)
            float timeSinceContact = Mathf.Min(worldModel.TimeSinceSeen, worldModel.TimeSinceHeard);
            
            if (timeSinceContact < timeRecentThreshold) return 0;  // Recent
            if (timeSinceContact < timeOldThreshold) return 1;     // Old
            return 2;                                              // Very old
        }

        /// <summary>
        /// Get heat here bucket (0-2)
        /// 0: Cold (< 5 heat) - Little traffic in current room
        /// 1: Warm (5-20 heat) - Moderate traffic
        /// 2: Hot (>20 heat) - Heavy traffic/choke point
        /// </summary>
        private int GetHeatHereBucket()
        {
            if (!roomTracker || !TransitionHeatGraph.Instance)
                return 0; // Default to cold
            
            string currentRoom = roomTracker.GetCurrentRoom();
            if (string.IsNullOrEmpty(currentRoom))
                return 0; // Not in any room
            
            float chokeScore = TransitionHeatGraph.Instance.GetChokeScore(currentRoom);
            
            if (chokeScore < heatWarmThreshold) return 0;  // Cold
            if (chokeScore < heatHotThreshold) return 1;   // Warm
            return 2;                                       // Hot
        }

        /// <summary>
        /// Get heat nearby bucket (0-2)
        /// 0: Cold (< 5 heat) - No hot paths nearby or low room heat
        /// 1: Warm (5-20 heat) - Some hot paths adjacent or moderate room heat
        /// 2: Hot (>20 heat) - Very hot path adjacent or high room heat
        /// </summary>
        private int GetHeatNearbyBucket()
        {
            if (!roomTracker || !TransitionHeatGraph.Instance)
                return 0; // Default to cold
            
            string currentRoom = roomTracker.GetCurrentRoom();
            if (string.IsNullOrEmpty(currentRoom))
                return 0; // Not in any room
            
            // Check both: adjacent edge heat (transitions) AND node heat (dwelling in room)
            float maxAdjacentHeat = TransitionHeatGraph.Instance.GetMaxAdjacentEdgeHeat(currentRoom);
            
            // ✅ ALSO check current room's node heat (player is dwelling here)
            float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(currentRoom);
            
            // Take maximum of both
            float totalHeat = Mathf.Max(maxAdjacentHeat, nodeHeat);
            
            if (totalHeat < heatWarmThreshold) return 0;  // Cold
            if (totalHeat < heatHotThreshold) return 1;   // Warm
            return 2;                                      // Hot
        }

        /// <summary>
        /// Get state space size (for debugging)
        /// </summary>
        public int GetStateSpaceSize()
        {
            // 2 × 3 × 3 × 3 × 3 × 3 = 486 states
            return 2 * 3 * 3 * 3 * 3 * 3;
        }

        /// <summary>
        /// Reset state (for testing)
        /// </summary>
        public void ResetState()
        {
            lastActionValue = 0;
            Debug.Log("<color=yellow>[SimpleStateExtractor] State reset</color>");
        }
    }

    /// <summary>
    /// Simple RL State Key - 4 features (54 states total)
    /// </summary>
    [System.Serializable]
    public struct SimpleRLStateKey
    {
        public byte playerVisible;         // 0/1 (2 values)
        public byte distanceBucket;        // 0/1/2 (3 values)
        public byte lastSeenSector;        // 0/1/2 (3 values)
        public byte timeSinceContactBucket; // 0/1/2 (3 values)
        public byte heatHereBucket;        // 0/1/2 (3 values) - NEW
        public byte heatNearbyBucket;      // 0/1/2 (3 values) - NEW

        /// <summary>
        /// Generate hash key for Q-table lookup
        /// Maps 6 features to unique integer (0-485)
        /// </summary>
        public int GetHashKey()
        {
            // Formula: key = v*243 + d*81 + s*27 + t*9 + hh*3 + hn
            // Ranges:
            //   playerVisible: [0,1] → multiply by 243
            //   distanceBucket: [0,2] → multiply by 81
            //   lastSeenSector: [0,2] → multiply by 27
            //   timeSinceContactBucket: [0,2] → multiply by 9
            //   heatHereBucket: [0,2] → multiply by 3
            //   heatNearbyBucket: [0,2] → multiply by 1
            
            int key = playerVisible * 243 +
                      distanceBucket * 81 +
                      lastSeenSector * 27 +
                      timeSinceContactBucket * 9 +
                      heatHereBucket * 3 +
                      heatNearbyBucket;
            
            return key;
        }

        public override string ToString()
        {
            return $"[Simple State: visible={playerVisible}, dist={distanceBucket}, " +
                   $"sector={lastSeenSector}, time={timeSinceContactBucket}, " +
                   $"heatHere={heatHereBucket}, heatNearby={heatNearbyBucket}, key={GetHashKey()}]";
        }
    }
}
