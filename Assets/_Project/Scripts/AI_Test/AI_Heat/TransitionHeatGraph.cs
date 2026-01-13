using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AITest.Heat
{
    /// <summary>
    /// Room/Corridor Transition Heat Graph - Tracks player movement patterns between rooms
    /// 
    /// CRITICAL: NOT OMNISCIENT - Only updates on observable room transitions, NOT direct player position
    /// 
    /// Features:
    /// - Edge heat between room transitions (undirected graph)
    /// - Temporal decay to forget old patterns
    /// - Choke point detection (rooms with high transition traffic)
    /// - Hot route pathfinding (follow hottest edges)
    /// </summary>
    public class TransitionHeatGraph : MonoBehaviour
    {
        [Header("Heat Configuration")]
        [Tooltip("Heat decay per second (linear)")]
        [Range(0f, 1f)]
        public float decayRate = 0.1f;
        
        [Tooltip("Minimum heat threshold (below this, heat = 0)")]
        [Range(0f, 1f)]
        public float minHeatThreshold = 0.01f;
        
        [Tooltip("Maximum heat cap per edge")]
        [Range(10f, 1000f)]
        public float maxHeatCap = 100f;
        
        [Header("Weight Multipliers")]
        [Tooltip("Base weight for normal walk transitions")]
        public float walkWeight = 1.0f;
        
        [Tooltip("Weight multiplier for sprint transitions")]
        public float sprintWeightMultiplier = 2.0f;
        
        [Tooltip("Node heat boost when player interacts in room")]
        public float interactionNodeBoost = 0.5f;
        
        [Header("Choke Point Detection")]
        [Tooltip("Percentile threshold for choke points (0-1)")]
        [Range(0.5f, 0.95f)]
        public float chokePercentile = 0.75f;
        
        [Header("Optional Cell Heat Overlay")]
        [Tooltip("Enable fine-grained cell heat tracking (OPTIONAL - can disable)")]
        public bool enableCellHeatOverlay = false;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        public bool showGizmos = true;
        public float debugLogInterval = 5f;
        
        // Singleton
        public static TransitionHeatGraph Instance { get; private set; }
        
        // Graph structure
        private HashSet<string> nodes = new HashSet<string>();
        private Dictionary<string, HashSet<string>> adjacency = new Dictionary<string, HashSet<string>>();
        
        // Heat data
        private Dictionary<EdgeKey, float> edgeHeat = new Dictionary<EdgeKey, float>();
        private Dictionary<string, float> nodeHeat = new Dictionary<string, float>();
        
        // Runtime tracking
        private float lastDecayTime;
        private float lastDebugLogTime;
        
        // Edge key struct (undirected)
        private struct EdgeKey : System.IEquatable<EdgeKey>
        {
            public readonly string a;
            public readonly string b;
            
            public EdgeKey(string roomA, string roomB)
            {
                // Ensure consistent ordering for undirected edges
                if (string.Compare(roomA, roomB) < 0)
                {
                    a = roomA;
                    b = roomB;
                }
                else
                {
                    a = roomB;
                    b = roomA;
                }
            }
            
            public bool Equals(EdgeKey other)
            {
                return a == other.a && b == other.b;
            }
            
            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }
            
            public override int GetHashCode()
            {
                return (a?.GetHashCode() ?? 0) * 31 + (b?.GetHashCode() ?? 0);
            }
        }
        
        private void Awake()
        {
            // Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            lastDecayTime = Time.unscaledTime;
            lastDebugLogTime = Time.unscaledTime;
            
            if (showDebugLogs)
                Debug.Log("<color=lime>[TransitionHeatGraph] Initialized</color>");
        }
        
        private void Update()
        {
            // Periodic decay
            float dt = Time.unscaledTime - lastDecayTime;
            if (dt >= 1f) // Decay every second
            {
                DecayTick(dt);
                lastDecayTime = Time.unscaledTime;
            }
            
            // Debug logging
            if (showDebugLogs && Time.unscaledTime - lastDebugLogTime >= debugLogInterval)
            {
                LogTopChokePoints();
                lastDebugLogTime = Time.unscaledTime;
            }
        }
        
        #region Public API
        
        /// <summary>
        /// Register a room node
        /// </summary>
        public void RegisterNode(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            
            if (!nodes.Contains(roomId))
            {
                nodes.Add(roomId);
                if (!adjacency.ContainsKey(roomId))
                    adjacency[roomId] = new HashSet<string>();
                
                if (showDebugLogs)
                    Debug.Log($"[TransitionHeatGraph] Node registered: {roomId}");
            }
        }
        
        /// <summary>
        /// Register an edge between two rooms (bidirectional)
        /// </summary>
        public void RegisterEdge(string roomA, string roomB)
        {
            if (string.IsNullOrEmpty(roomA) || string.IsNullOrEmpty(roomB)) return;
            if (roomA == roomB) return; // No self-loops
            
            RegisterNode(roomA);
            RegisterNode(roomB);
            
            adjacency[roomA].Add(roomB);
            adjacency[roomB].Add(roomA);
            
            EdgeKey key = new EdgeKey(roomA, roomB);
            if (!edgeHeat.ContainsKey(key))
                edgeHeat[key] = 0f;
        }
        
        /// <summary>
        /// Add heat to a room transition (player moved from prevRoom to newRoom)
        /// </summary>
        public void AddTransitionHeat(string prevRoom, string newRoom, float weight = 1.0f)
        {
            if (string.IsNullOrEmpty(prevRoom) || string.IsNullOrEmpty(newRoom)) return;
            if (prevRoom == newRoom) return;
            
            // Auto-register edge if not exists
            RegisterEdge(prevRoom, newRoom);
            
            EdgeKey key = new EdgeKey(prevRoom, newRoom);
            float currentHeat = edgeHeat.ContainsKey(key) ? edgeHeat[key] : 0f;
            edgeHeat[key] = Mathf.Min(currentHeat + weight, maxHeatCap);
            
            if (showDebugLogs)
                Debug.Log($"<color=yellow>[TransitionHeatGraph] Heat +{weight:F1}: {prevRoom} ↔ {newRoom} (total: {edgeHeat[key]:F1})</color>");
        }
        
        /// <summary>
        /// Add heat to a room node (player dwelling/interacting)
        /// </summary>
        public void AddNodeHeat(string roomId, float weight = 1.0f)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            
            RegisterNode(roomId);
            
            float currentHeat = nodeHeat.ContainsKey(roomId) ? nodeHeat[roomId] : 0f;
            nodeHeat[roomId] = Mathf.Min(currentHeat + weight, maxHeatCap);
        }
        
        /// <summary>
        /// Decay all heat values over time
        /// </summary>
        public void DecayTick(float dt)
        {
            float decay = decayRate * dt;
            
            // Decay edge heat
            List<EdgeKey> edgeKeys = new List<EdgeKey>(edgeHeat.Keys);
            foreach (var key in edgeKeys)
            {
                edgeHeat[key] = Mathf.Max(0f, edgeHeat[key] - decay);
                
                // Remove if below threshold
                if (edgeHeat[key] < minHeatThreshold)
                    edgeHeat[key] = 0f;
            }
            
            // Decay node heat
            List<string> nodeKeys = new List<string>(nodeHeat.Keys);
            foreach (var key in nodeKeys)
            {
                nodeHeat[key] = Mathf.Max(0f, nodeHeat[key] - decay);
                
                if (nodeHeat[key] < minHeatThreshold)
                    nodeHeat[key] = 0f;
            }
        }
        
        /// <summary>
        /// Get heat value for a specific edge
        /// </summary>
        public float GetEdgeHeat(string roomA, string roomB)
        {
            if (string.IsNullOrEmpty(roomA) || string.IsNullOrEmpty(roomB)) return 0f;
            
            EdgeKey key = new EdgeKey(roomA, roomB);
            return edgeHeat.ContainsKey(key) ? edgeHeat[key] : 0f;
        }
        
        /// <summary>
        /// Get heat value for a room node
        /// </summary>
        public float GetNodeHeat(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return 0f;
            return nodeHeat.ContainsKey(roomId) ? nodeHeat[roomId] : 0f;
        }

        /// <summary>
        /// Multiply a room's node heat by a multiplier (cool down after search)
        /// </summary>
        public void MultiplyNodeHeat(string roomId, float multiplier)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            if (!nodeHeat.ContainsKey(roomId)) return;
            nodeHeat[roomId] = Mathf.Max(0f, nodeHeat[roomId] * multiplier);
        }
        
        /// <summary>
        /// Calculate choke score for a room (sum of adjacent edge heats)
        /// </summary>
        public float GetChokeScore(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return 0f;
            if (!adjacency.ContainsKey(roomId)) return 0f;
            
            float score = 0f;
            foreach (var neighbor in adjacency[roomId])
            {
                score += GetEdgeHeat(roomId, neighbor);
            }
            
            return score;
        }
        
        /// <summary>
        /// ✅ Get adjacent rooms for a given room (for neighbor propagation)
        /// </summary>
        public HashSet<string> GetAdjacentRooms(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return new HashSet<string>();
            if (!adjacency.ContainsKey(roomId)) return new HashSet<string>();
            
            return new HashSet<string>(adjacency[roomId]); // Return copy
        }
        
        /// <summary>
        /// Get maximum adjacent edge heat for a room (hottest path leading away)
        /// </summary>
        public float GetMaxAdjacentEdgeHeat(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return 0f;
            if (!adjacency.ContainsKey(roomId)) return 0f;
            
            float maxHeat = 0f;
            foreach (var neighbor in adjacency[roomId])
            {
                float heat = GetEdgeHeat(roomId, neighbor);
                if (heat > maxHeat)
                    maxHeat = heat;
            }
            
            return maxHeat;
        }

        /// <summary>
        /// Get room id with highest node heat (or "None" if none)
        /// </summary>
        public string GetPeakRoom()
        {
            if (nodeHeat.Count == 0) return "None";
            string best = null;
            float bestVal = -1f;
            foreach (var kvp in nodeHeat)
            {
                if (kvp.Value > bestVal)
                {
                    bestVal = kvp.Value;
                    best = kvp.Key;
                }
            }
            return best ?? "None";
        }

        /// <summary>
        /// Get highest node heat value (0 if none)
        /// </summary>
        public float GetPeakValue()
        {
            if (nodeHeat.Count == 0) return 0f;
            float bestVal = 0f;
            foreach (var kvp in nodeHeat)
            {
                if (kvp.Value > bestVal)
                    bestVal = kvp.Value;
            }
            return bestVal;
        }
        
        /// <summary>
        /// Get top K choke points (rooms with highest traffic)
        /// </summary>
        public List<string> GetTopChokePoints(int k = 3)
        {
            var scores = new Dictionary<string, float>();
            
            foreach (var roomId in nodes)
            {
                scores[roomId] = GetChokeScore(roomId);
            }
            
            return scores
                .OrderByDescending(kvp => kvp.Value)
                .Take(k)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        /// <summary>
        /// Get hot route from a starting room (greedy follow hottest edges)
        /// </summary>
        public List<string> GetHotChainFrom(string startRoom, int maxSteps = 3)
        {
            List<string> chain = new List<string>();
            HashSet<string> visited = new HashSet<string>();
            
            if (string.IsNullOrEmpty(startRoom) || !nodes.Contains(startRoom))
                return chain;
            
            string current = startRoom;
            chain.Add(current);
            visited.Add(current);
            
            for (int step = 0; step < maxSteps; step++)
            {
                if (!adjacency.ContainsKey(current))
                    break;
                
                // Find hottest unvisited neighbor
                string hottest = null;
                float hottestHeat = 0f;
                
                foreach (var neighbor in adjacency[current])
                {
                    if (visited.Contains(neighbor))
                        continue;
                    
                    float heat = GetEdgeHeat(current, neighbor);
                    if (heat > hottestHeat)
                    {
                        hottestHeat = heat;
                        hottest = neighbor;
                    }
                }
                
                if (hottest == null)
                    break; // No unvisited neighbors
                
                chain.Add(hottest);
                visited.Add(hottest);
                current = hottest;
            }
            
            return chain;
        }
        
        // ❌ REMOVED: Duplicate GetAdjacentRooms - using HashSet<string> version at line 284
        
        /// <summary>
        /// Calculate heat bucket for a given heat value (for state extraction)
        /// </summary>
        public int GetHeatBucket(float heat, float lowThreshold = 5f, float highThreshold = 20f)
        {
            if (heat < lowThreshold)
                return 0; // Cold
            else if (heat < highThreshold)
                return 1; // Warm
            else
                return 2; // Hot
        }
        
        #endregion
        
        #region Debug & Visualization
        
        private void LogTopChokePoints()
        {
            var topChokes = GetTopChokePoints(5);
            if (topChokes.Count == 0)
            {
                Debug.Log("[TransitionHeatGraph] No choke points detected yet");
                return;
            }
            
            string log = "<color=cyan>[TransitionHeatGraph] Top Choke Points:</color>\n";
            for (int i = 0; i < topChokes.Count; i++)
            {
                float score = GetChokeScore(topChokes[i]);
                log += $"  {i + 1}. {topChokes[i]}: {score:F1}\n";
            }
            Debug.Log(log);
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || !Application.isPlaying)
                return;
            
            // Draw edges with heat intensity
            foreach (var kvp in edgeHeat)
            {
                if (kvp.Value < 0.1f)
                    continue;
                
                // Get room positions (try to find RoomZone components)
                Vector3? posA = GetRoomPosition(kvp.Key.a);
                Vector3? posB = GetRoomPosition(kvp.Key.b);
                
                if (!posA.HasValue || !posB.HasValue)
                    continue;
                
                // Color based on heat intensity
                float intensity = Mathf.Clamp01(kvp.Value / 50f);
                Gizmos.color = Color.Lerp(Color.blue, Color.red, intensity);
                
                Gizmos.DrawLine(posA.Value, posB.Value);
            }
        }
        
        private Vector3? GetRoomPosition(string roomId)
        {
            var roomZone = AITest.World.WorldRegistry.Instance?.GetRoom(roomId);
            if (roomZone != null)
                return roomZone.Center;
            
            return null;
        }
        
        #endregion
    }
}
