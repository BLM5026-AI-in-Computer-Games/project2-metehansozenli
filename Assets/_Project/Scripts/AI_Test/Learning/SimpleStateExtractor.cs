using UnityEngine;
using AITest.WorldModel;
using AITest.World;
using AITest.Heat;
using AITest.Quest;

namespace AITest.Learning
{
    /// <summary>
    /// Simple State Extractor - OPTIMIZED MDP for Tabular Q-Learning
    /// 
    /// RATIONALE: Previous 486-state space had overlapping features and distance hallucinations.
    /// This new 162-state architecture relies on topological (Room) context and Heatmaps.
    /// 
    /// State Features (5 features):
    /// 1. PlayerPresence (3): 0=Visible, 1=HeardRecent, 2=Lost/Old
    /// 2. RoomContext (3): 0=SameRoom, 1=AdjacentRoom, 2=Far/Unknown
    /// 3. HeatHere (3): 0=Cold, 1=Warm, 2=Hot
    /// 4. HeatNearby (3): 0=Cold, 1=Warm, 2=Hot
    /// 5. StrategicPhase (2): 0=Early/Mid, 1=EndGame (Panic)
    /// 
    /// Total States: 3 * 3 * 3 * 3 * 2 = 162 states.
    /// This ensures extremely fast convergence (<200 episodes).
    /// </summary>
    public class SimpleStateExtractor : MonoBehaviour
    {
        [Header("References")]
        public EnemyWorldModel worldModel;
        public Transform enemyTransform;
        public EnemyRoomTracker roomTracker;
        public TransitionHeatGraph heatGraph;
        
        [Header("Thresholds")]
        [Tooltip("Contact considered 'Recent' if less than this time (seconds)")]
        public float recentContactThreshold = 5f;
        
        [Tooltip("Heat threshold for 'warm' bucket")]
        public float heatWarmThreshold = 0.5f; // Lowered from 5f
        
        [Tooltip("Heat threshold for 'hot' bucket")]
        public float heatHotThreshold = 2.0f; // Lowered from 20f
        
        [Tooltip("Game phase switches to EndGame if Quest Progress > this %")]
        public float panicPhaseThreshold = 0.6f;

        [Header("Debug")]
        public bool showDebugLogs = false;
        
        [Tooltip("If true, uses actual player position for state calculation (Cheat/Oracle Mode)")]
        public bool useRealPlayerPosition = false;

        private void Awake()
        {
            // Auto-find components
            if (!worldModel) worldModel = GetComponent<EnemyWorldModel>();
            if (!enemyTransform) enemyTransform = transform;
            if (!roomTracker) roomTracker = GetComponent<EnemyRoomTracker>();
            if (!heatGraph) heatGraph = TransitionHeatGraph.Instance;
        }

        /// <summary>
        /// Extract Optimized State (162 states)
        /// </summary>
        public SimpleRLStateKey ExtractState()
        {
            SimpleRLStateKey state = new SimpleRLStateKey();
            
            if (!worldModel) return state;

            // 1. PLAYER PRESENCE (0=Vis, 1=Heard, 2=Lost)
            state.playerPresence = (byte)GetPlayerPresence();

            // 2. ROOM CONTEXT (0=Same, 1=Adj, 2=Far)
            state.roomContext = (byte)GetRoomContext();

            // 3. HEAT HERE (0=Cold, 1=Warm, 2=Hot)
            state.heatHere = (byte)GetHeatHere();

            // 4. HEAT NEARBY (0=Cold, 1=Warm, 2=Hot)
            state.heatNearby = (byte)GetHeatNearby();

            // 5. STRATEGIC PHASE (0=Early, 1=EndGame)
            state.strategicPhase = (byte)GetStrategicPhase();

            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[State] {state}</color>");
            }
            
            return state;
        }
        
        // Unused in optimized version but kept for interface compatibility
        public void SetLastAction(AITest.Enemy.EnemyMode action) { }

        // --- FEATURE EXTRACTORS ---

        private int GetPlayerPresence()
        {
            // Priority 1: Vision
            if (worldModel.SeePlayerNow) 
                return 0; // Visible (Highest Priority)

            // Priority 2: Recent Hearing (or very recent sight memory)
            float lowestTime = Mathf.Min(worldModel.TimeSinceSeen, worldModel.TimeSinceHeard);
            if (lowestTime < recentContactThreshold)
                return 1; // Heard/Fresh

            // Priority 3: Lost
            return 2; // Lost/Old
        }

        private int GetRoomContext()
        {
            // If we don't know where we are, we are lost/far
            string myRoom = roomTracker ? roomTracker.GetCurrentRoom() : "";
            if (string.IsNullOrEmpty(myRoom)) return 2; // Unknown

            // Get last known player pos
            Vector2 targetPos = GetBestTargetPos();
            if (targetPos == Vector2.zero) return 2; // Unknown target

            // Topological Check: Which room is the player in?
            string playerRoom = "";
            bool foundRoom = WorldRegistry.Instance && WorldRegistry.Instance.TryGetRoomAtPosition(targetPos, out playerRoom);

            if (foundRoom)
            {
                if (playerRoom == myRoom) return 0; // Same Room

                // Check Adjacent
                if (heatGraph)
                {
                    var adjacent = heatGraph.GetAdjacentRooms(myRoom);
                    if (adjacent != null && adjacent.Contains(playerRoom))
                        return 1; // Adjacent Room
                }
            }
            else
            {
                // ? FALLBACK: If player is Visible but not in a registered room (e.g. doorway), use Euclidean distance
                if (worldModel.SeePlayerNow)
                {
                    float dist = Vector2.Distance(enemyTransform.position, targetPos);
                    if (dist < 8.0f) return 0; // Close enough to be "Same Room" context
                    if (dist < 15.0f) return 1; // Likely adjacent
                }
            }

            return 2; // Far/Unknown
        }

        private int GetHeatHere()
        {
            if (!roomTracker || !heatGraph) return 0;
            string myRoom = roomTracker.GetCurrentRoom();
            if (string.IsNullOrEmpty(myRoom)) return 0;

            // ? FIX: Read Node Heat (Perception), not Choke Score (Movement edges)
            float score = heatGraph.GetNodeHeat(myRoom);
            return BucketizeHeat(score);
        }

        private int GetHeatNearby()
        {
            if (!roomTracker || !heatGraph) return 0;
            string myRoom = roomTracker.GetCurrentRoom();
            if (string.IsNullOrEmpty(myRoom)) return 0;

            // ? FIX: Check Neighbor Node Heat (Room contents), not Edge Heat (Path usage)
            var neighbors = heatGraph.GetAdjacentRooms(myRoom);
            if (neighbors == null || neighbors.Count == 0) return 0;

            float maxHeat = 0f;
            foreach (var neighbor in neighbors)
            {
                float h = heatGraph.GetNodeHeat(neighbor);
                if (h > maxHeat) maxHeat = h;
            }
            
            return BucketizeHeat(maxHeat);
        }

        private int BucketizeHeat(float val)
        {
            if (val < heatWarmThreshold) return 0; // Cold
            if (val < heatHotThreshold) return 1;  // Warm
            return 2;                              // Hot
        }

        private int GetStrategicPhase()
        {
            // Hook into QuestManager
            var qm = QuestManager.Instance;
            if (qm)
            {
                float progress = qm.GetProgress();
                if (progress >= panicPhaseThreshold) return 1; // EndGame/Panic
            }
            
            return 0; // Normal Phase
        }

        // --- DEBUG GUI ---
        private void OnGUI()
        {
            if (!showDebugLogs) return;

            GUILayout.BeginArea(new Rect(10, 200, 300, 220), GUI.skin.box);
            GUILayout.Label("<b>--- STATE DEBUGGER ---</b>");
            
            string myRoom = roomTracker ? roomTracker.GetCurrentRoom() : "null";
            GUILayout.Label($"Enemy Room: {myRoom}");

            string targetSrc = "None";
            Vector2 targetPos = GetBestTargetPos(out targetSrc);
            string targetRoom = "Unknown";
            if (WorldRegistry.Instance)
                WorldRegistry.Instance.TryGetRoomAtPosition(targetPos, out targetRoom);
            
            GUILayout.Label($"Target Pos: {targetPos}");
            GUILayout.Label($"Target Room (Belief): {targetRoom}");
            
            // ? DEBUG HEATMAP status
            if (heatGraph)
            {
                string hotRoom = heatGraph.GetHottestRoom();
                float hotVal = heatGraph.GetNodeHeat(hotRoom);
                GUILayout.Label($"Hottest Room: {hotRoom} ({hotVal:F2})");
            }
            else
            {
                GUILayout.Label("HeatGraph: <color=red>NULL</color>");
            }

            // ? DEBUG: Show REAL player room for comparison
            string realPlayerRoom = "Unknown";
            if (worldModel.perception && worldModel.perception.player)
            {
                Vector2 realPos = worldModel.perception.player.position;
                if (WorldRegistry.Instance)
                    WorldRegistry.Instance.TryGetRoomAtPosition(realPos, out realPlayerRoom);
            }
            GUILayout.Label($"Real Player Room: <color=lime>{realPlayerRoom}</color>");

            GUILayout.Label($"Source: <color=yellow>{targetSrc}</color>");
            
            var state = ExtractState();
            GUILayout.Label($"<b>State ID: {state.GetHashKey()}</b>");
            GUILayout.Label($"Presence: {state.playerPresence} (0=Vis, 1=Hrd, 2=Lst)");
            GUILayout.Label($"Context: {state.roomContext} (0=Same, 1=Adj, 2=Far)");
            GUILayout.Label($"Heat: {state.heatHere} / {state.heatNearby}");
            
            GUILayout.EndArea();
        }
        
        // Overload for internal use
        private Vector2 GetBestTargetPos()
        {
            return GetBestTargetPos(out _);
        }

        private Vector2 GetBestTargetPos(out string source)
        {
            source = "None";
            
            // ? CHEAT MODE: Use Real Player Position (Oracle)
            // This makes the state space deterministically correct based on ground truth,
            // speeding up training but making the agent "Psychic".
            if (useRealPlayerPosition && worldModel.perception && worldModel.perception.player)
            {
                source = "Oracle(Cheat)";
                return worldModel.perception.player.position;
            }
            
            // 1. VISION (Real-time)
            if (worldModel.SeePlayerNow)
            {
                source = "Visible";
                // ? FIX: Use REAL position if visible
                if (worldModel.perception && worldModel.perception.player)
                    return worldModel.perception.player.position;
                
                return worldModel.LastSeenPos; 
            }
            
            string myRoom = roomTracker ? roomTracker.GetCurrentRoom() : "";
            
            // 2. VISION MEMORY (Last Seen)
            // Only use if we haven't checked/cleared it yet
            // If we are in the LastSeenRoom and don't see player, assume it's stale!
            bool seenValid = worldModel.HasSeenRecently;
            if (seenValid && !string.IsNullOrEmpty(myRoom) && myRoom == worldModel.LastSeenRoom)
            {
                 // We are in the room where we last saw the player, but SeePlayerNow is false.
                 // So the player is GONE. Ignore this memory.
                 seenValid = false;
            }

            // 3. HEARING MEMORY
            bool heardValid = worldModel.HasHeardRecently;
            if (heardValid && !string.IsNullOrEmpty(myRoom) && myRoom == worldModel.LastHeardRoom && worldModel.TimeSinceHeard > 5.0f)
            {
                // We are in the room where we heard something >5s ago, and nobody is here.
                // Ignore.
                heardValid = false;
            }
            
            // Decision: Seen vs Heard vs Heat
            
            if (seenValid)
            {
                if (!heardValid || worldModel.TimeSinceSeen <= worldModel.TimeSinceHeard)
                {
                    source = "Memory(Seen)";
                    return worldModel.LastSeenPos;
                }
            }
            
            if (heardValid)
            {
                source = "Memory(Heard)";
                return worldModel.LastHeardPos;
            }

            // 4. HEAT GRAPH (Strategic)
            if (heatGraph)
            {
                string hotRoom = heatGraph.GetHottestRoom();
                
                // If hot room is MY room, and I see nothing, look for next hottest?
                // For now just stick to hottest.
                if (!string.IsNullOrEmpty(hotRoom) && WorldRegistry.Instance)
                {
                    var r = WorldRegistry.Instance.GetRoom(hotRoom);
                    if (r != null)
                    {
                        source = $"Heat({hotRoom})";
                        return r.Center;
                    }
                }
            }

            source = "None";
            return Vector2.zero;
        }

        // --- DEBUG ---
        public int GetStateSpaceSize()
        {
            return 3 * 3 * 3 * 3 * 2; // 162
        }
    }

    /// <summary>
    /// OPTIMIZED State Key (162 states)
    /// </summary>
    [System.Serializable]
    public struct SimpleRLStateKey
    {
        public byte playerPresence; // 0-2 (3)
        public byte roomContext;    // 0-2 (3)
        public byte heatHere;       // 0-2 (3)
        public byte heatNearby;     // 0-2 (3)
        public byte strategicPhase; // 0-1 (2)

        /// <summary>
        /// Generate unique hash key [0..161]
        /// </summary>
        public int GetHashKey()
        {
            // Formula: Mixed radix encoding
            // Key = P*54 + R*18 + HH*6 + HN*2 + SP
            // Max = 2*54 + 2*18 + 2*6 + 2*2 + 1 = 108 + 36 + 12 + 4 + 1 = 161
            
            int key = playerPresence * 54 +
                      roomContext * 18 +
                      heatHere * 6 +
                      heatNearby * 2 +
                      strategicPhase;
            return key;
        }

        public override string ToString()
        {
            return $"[State: Pres={playerPresence}, Room={roomContext}, Heat={heatHere}/{heatNearby}, Phase={strategicPhase} | Key={GetHashKey()}]";
        }
    }
}
