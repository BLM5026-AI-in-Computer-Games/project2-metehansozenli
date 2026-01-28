using UnityEngine;
using System.Collections.Generic;
using AITest.World;
using AITest.Perception;
using AITest.Heat; // ✅ For TransitionHeatGraph

namespace AITest.WorldModel
{
    /// <summary>
    /// Enemy World Model - Unified belief state manager
    /// 
    /// PROMPT 4: Stores enemy's belief about player location
    /// - Vision memory (see events)
    /// - Audio memory (hear events)
    /// - Clue memory (clue events)
    /// - Room-based heatmap via TransitionHeatGraph (probability distribution)
    /// 
    /// UPDATES:
    /// - See event: Strong spike in room
    /// - Hear event: Medium spike in room + neighbors
    /// - Clue event: Low/medium spike
    /// - Decay over time (handled by TransitionHeatGraph)
    /// 
    /// ✅ TransitionHeatGraph is the ONLY heat system (HeatmapModel removed)
    /// </summary>
    public class EnemyWorldModel : MonoBehaviour
    {
        [Header("References")]
        public Perception.Perception perception;
        
        [Header("Event Weights")]
        [Tooltip("Heat spike on see event")]
        [Range(0f, 10f)] public float seeEventWeight = 5.0f;
        
        [Tooltip("Heat spike on hear event")]
        [Range(0f, 10f)] public float hearEventWeight = 3.0f;
        
        [Tooltip("Heat spike on clue event")]
        [Range(0f, 10f)] public float clueEventWeight = 1.5f;
        
        [Header("Neighbor Propagation")]
        [Tooltip("Propagate heat to neighbor rooms (hear/clue events)")]
        public bool propagateToNeighbors = true;
        
        [Tooltip("Neighbor heat multiplier")]
        [Range(0f, 1f)] public float neighborMultiplier = 0.6f;
        
        [Header("Memory Timeouts")]
        [Tooltip("See memory timeout (seconds)")]
        [Range(5f, 60f)] public float seeMemoryTimeout = 20f;
        
        [Tooltip("Hear memory timeout (seconds)")]
        [Range(5f, 60f)] public float hearMemoryTimeout = 30f;
        
        [Tooltip("Clue memory timeout (seconds)")]
        [Range(5f, 60f)] public float clueMemoryTimeout = 40f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // ? PROMPT 4: Belief state
        public bool SeePlayerNow => perception ? perception.PlayerVisible : false;
        
        public Vector2 LastSeenPos => perception ? perception.LastSeenPos : Vector2.zero;
        public float LastSeenTime { get; private set; } = -999f;
        public string LastSeenRoom { get; private set; } = "None";
        
        public Vector2 LastHeardPos => perception ? perception.LastHeardPos : Vector2.zero;
        public float LastHeardTime { get; private set; } = -999f;
        public string LastHeardRoom { get; private set; } = "None";
        
        public Vector2 LastCluePos { get; private set; } = Vector2.zero;
        public float LastClueTime { get; private set; } = -999f;
        public string LastClueRoom { get; private set; } = "None";
        
        // Computed properties
        public float TimeSinceSeen => Time.time - LastSeenTime;
        public float TimeSinceHeard => Time.time - LastHeardTime;
        public float TimeSinceClue => Time.time - LastClueTime;
        
        public bool HasSeenRecently => TimeSinceSeen < seeMemoryTimeout;
        public bool HasHeardRecently => TimeSinceHeard < hearMemoryTimeout;
        public bool HasClueRecently => TimeSinceClue < clueMemoryTimeout;

        private void Awake()
        {
            // Auto-find components
            if (!perception)
                perception = GetComponent<Perception.Perception>();
        }

        private void OnEnable()
        {
            // Subscribe to perception events (via ClueEventBus)
            // Note: Perception doesn't use UnityEvents, uses direct callbacks
            // TODO: Migrate to event-based system if needed
        }

        private void Start()
        {
            // ? FORCE PARAMETER OVERRIDE (To fix configuration issues)
            seeEventWeight = 6.0f;  // HIGH
            hearEventWeight = 3.5f; // MEDIUM-HIGH
            clueEventWeight = 2.0f; // MEDIUM
            
            if (showDebugLogs) 
                Debug.Log($"[EnemyWorldModel] Forced Heat Weights: See={seeEventWeight}, Hear={hearEventWeight}");
        }

        private void OnDisable()
        {
            // Unsubscribe
            // TODO: Implement unsubscribe logic
        }

        private void Update()
        {
            // ✅ Direct check instead of events
            if (perception && perception.PlayerVisible)
            {
                // See player NOW
                Vector2 playerPos = perception.player ? (Vector2)perception.player.position : Vector2.zero;
                
                // Update only if position changed significantly
                if (Vector2.Distance(LastSeenPos, playerPos) > 0.5f || Time.time - LastSeenTime > 1f)
                {
                    OnSeePlayer(playerPos, 1f);
                }
            }
            
            // Memory timeout cleanup
            CheckMemoryTimeouts();
        }

        /// <summary>
        /// ? PROMPT 4: See event handler
        /// </summary>
        private void OnSeePlayer(Vector2 position, float confidence)
        {
            LastSeenTime = Time.time;
            
            // Get room ID
            if (WorldRegistry.Instance)
            {
                if (WorldRegistry.Instance.TryGetRoomAtPosition(position, out string roomId))
                {
                    LastSeenRoom = roomId;
                    
                    // ✅ Only use TransitionHeatGraph
                    if (TransitionHeatGraph.Instance)
                    {
                        TransitionHeatGraph.Instance.AddNodeHeat(roomId, seeEventWeight * confidence);
                    }
                    
                    if (showDebugLogs)
                        Debug.Log($"<color=orange>[WorldModel] SEE: Room={roomId}, Weight={seeEventWeight * confidence:F2}</color>");
                }
            }
        }

        /// <summary>
        /// Player lost event (optional - just for logging)
        /// </summary>
        private void OnLosePlayer(Vector2 lastKnownPos)
        {
            if (showDebugLogs)
                Debug.Log($"<color=yellow>[WorldModel] ??? LOST: {LastSeenRoom}</color>");
        }

        /// <summary>
        /// ? PROMPT 4: Hear event handler
        /// </summary>
        private void OnHearNoise(Vector2 position, float confidence)
        {
            LastHeardTime = Time.time;
            
            // Get room ID
            if (WorldRegistry.Instance)
            {
                if (WorldRegistry.Instance.TryGetRoomAtPosition(position, out string roomId))
                {
                    LastHeardRoom = roomId;
                    
                    // ✅ Only use TransitionHeatGraph
                    if (TransitionHeatGraph.Instance)
                    {
                        TransitionHeatGraph.Instance.AddNodeHeat(roomId, hearEventWeight * confidence);
                        
                        // Propagate to neighbors
                        if (propagateToNeighbors)
                        {
                            var neighbors = TransitionHeatGraph.Instance.GetAdjacentRooms(roomId);
                            foreach (string neighborRoom in neighbors)
                            {
                                TransitionHeatGraph.Instance.AddNodeHeat(neighborRoom, hearEventWeight * confidence * neighborMultiplier);
                            }
                        }
                    }
                    
                    if (showDebugLogs)
                        Debug.Log($"<color=yellow>[WorldModel] HEAR: Room={roomId}, Weight={hearEventWeight * confidence:F2}</color>");
                }
            }
        }

        /// <summary>
        /// ? PROMPT 4: Clue event handler
        /// </summary>
        private void OnClueFound(Vector2 position, float strength)
        {
            LastCluePos = position;
            LastClueTime = Time.time;
            
            // Get room ID
            if (WorldRegistry.Instance)
            {
                if (WorldRegistry.Instance.TryGetRoomAtPosition(position, out string roomId))
                {
                    LastClueRoom = roomId;
                    
                    // ✅ Only use TransitionHeatGraph
                    if (TransitionHeatGraph.Instance)
                    {
                        TransitionHeatGraph.Instance.AddNodeHeat(roomId, clueEventWeight * strength);
                        
                        // Light propagation to neighbors
                        if (propagateToNeighbors)
                        {
                            var neighbors = TransitionHeatGraph.Instance.GetAdjacentRooms(roomId);
                            foreach (string neighborRoom in neighbors)
                            {
                                TransitionHeatGraph.Instance.AddNodeHeat(neighborRoom, clueEventWeight * strength * neighborMultiplier * 0.5f);
                            }
                        }
                    }
                    
                    if (showDebugLogs)
                        Debug.Log($"<color=cyan>[WorldModel] CLUE: Room={roomId}, Position={position}</color>");
                }
            }
        }

        /// <summary>
        /// ? PROMPT 4: Mark room as searched (cool down)
        /// </summary>
        public void MarkRoomSearched(string roomId)
        {
            // Reduce heat significantly (enemy checked this room)
            if (TransitionHeatGraph.Instance)
            {
                TransitionHeatGraph.Instance.MultiplyNodeHeat(roomId, 0.8f); // 20% reduction only (was 90%)
            }
            
            if (showDebugLogs)
                Debug.Log($"<color=cyan>[WorldModel] ?? COOL DOWN: {roomId} (searched)</color>");
        }

        /// <summary>
        /// Check memory timeouts and clear expired data
        /// </summary>
        private void CheckMemoryTimeouts()
        {
            // See timeout
            if (!HasSeenRecently && LastSeenRoom != "None")
            {
                if (showDebugLogs)
                    Debug.Log($"<color=orange>[WorldModel] ? See memory expired ({LastSeenRoom})</color>");
                
                LastSeenRoom = "None";
                // Note: LastSeenPos comes from Perception, resets with Perception.ResetMemory()
            }
            
            // Hear timeout
            if (!HasHeardRecently && LastHeardRoom != "None")
            {
                if (showDebugLogs)
                    Debug.Log($"<color=orange>[WorldModel] ? Hear memory expired ({LastHeardRoom})</color>");
                
                LastHeardRoom = "None";
                // Note: LastHeardPos comes from Perception, resets with Perception.ResetMemory()
            }
            
            // Clue timeout
            if (!HasClueRecently && LastClueRoom != "None")
            {
                if (showDebugLogs)
                    Debug.Log($"<color=orange>[WorldModel] ? Clue memory expired ({LastClueRoom})</color>");
                
                LastClueRoom = "None";
                LastCluePos = Vector2.zero;
            }
        }

        /// <summary>
        /// Get most likely player location (see > heat peak > hear > clue)
        /// </summary>
        public Vector2 GetBestGuessPosition()
        {
            // Priority order:
            // 1. See (most reliable)
            if (HasSeenRecently)
                return LastSeenPos;
            
            // 2. Hear (less reliable)
            if (HasHeardRecently)
                return LastHeardPos;
            
            // 3. Clue (least reliable)
            if (HasClueRecently)
                return LastCluePos;
            
            // 4. No idea
            return Vector2.zero;
        }

        /// <summary>
        /// Get most likely room (see > heat peak > hear > clue)
        /// </summary>
        public string GetBestGuessRoom()
        {
            // Priority order:
            if (HasSeenRecently && LastSeenRoom != "None")
                return LastSeenRoom;
            
            if (HasHeardRecently && LastHeardRoom != "None")
                return LastHeardRoom;
            
            if (HasClueRecently && LastClueRoom != "None")
                return LastClueRoom;
            
            return "None";
        }

        /// <summary>
        /// Reset all belief state (for testing/episode reset)
        /// </summary>
        public void ResetBelief()
        {
            // Note: LastSeenPos is read from Perception, no need to reset here
            LastSeenTime = -999f;
            LastSeenRoom = "None";
            
            // Note: LastHeardPos is read from Perception, no need to reset here
            LastHeardTime = -999f;
            LastHeardRoom = "None";
            
            LastCluePos = Vector2.zero;
            LastClueTime = -999f;
            LastClueRoom = "None";
            
            // ✅ TransitionHeatGraph handles decay and reset automatically
            if (showDebugLogs)
                Debug.Log("<color=yellow>[WorldModel] ?? Belief state reset</color>");
        }
    }
}
