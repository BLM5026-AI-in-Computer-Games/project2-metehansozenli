using UnityEngine;
using AITest.WorldModel;
using AITest.Perception;
using AITest.World;
using AITest.Learning;
using AITest.Pathfinding; // ? FIXED: Correct namespace
using AITest.Heat;

namespace AITest.Enemy
{
    /// <summary>
    /// Enemy Context - Shared data for all options
    /// 
    /// PROMPT 6: Central access point for option execution
    /// - WorldModel: Belief state (heatmap, clues, memories)
    /// - Perception: Vision/hearing sensors
    /// - Pathfinder: A* navigation
    /// - Registry: Room/hide spot database
    /// - Perceptron: Target scoring
    /// - Timers: Elapsed time tracking
    /// </summary>
    public class EnemyContext : MonoBehaviour
    {
        [Header("References")]
        public EnemyWorldModel worldModel;
        public Perception.Perception perception;
        public Pathfinder pathfinder; // ? FIXED: Correct type
        public AICharacterController mover;
        public AIAgentMover agentMover; // ✅ Added for high-level movement info
        public ThreatPerceptron perceptron;
        public Transform enemyTransform;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Runtime data
        private float optionStartTime;
        private EnemyMode currentMode;
        
        // Public properties
        public float ElapsedTime => Time.time - optionStartTime;
        public EnemyMode CurrentMode => currentMode;
        public Vector2 Position => enemyTransform ? (Vector2)enemyTransform.position : Vector2.zero;
        
        // WorldRegistry shortcut
        public WorldRegistry Registry => WorldRegistry.Instance;

        private void Awake()
        {
            // Auto-find components
            if (!worldModel) worldModel = GetComponent<EnemyWorldModel>();
            if (!perception) perception = GetComponent<Perception.Perception>();
            if (!pathfinder) pathfinder = GetComponent<Pathfinder>();
            if (!mover) mover = GetComponent<AICharacterController>();
            if (!agentMover) agentMover = GetComponent<AIAgentMover>(); // ✅ Auto-find
            if (!perceptron) perceptron = GetComponent<ThreatPerceptron>();
            if (!enemyTransform) enemyTransform = transform;
        }

        /// <summary>
        /// Start option timer
        /// </summary>
        public void StartOptionTimer(EnemyMode mode)
        {
            optionStartTime = Time.time;
            currentMode = mode;
            
            if (showDebugLogs)
                Debug.Log($"<color=cyan>[EnemyContext] Option started: {mode}</color>");
        }

        /// <summary>
        /// Reset timer
        /// </summary>
        public void ResetTimer()
        {
            optionStartTime = Time.time;
        }

        /// <summary>
        /// Get current room ID
        /// </summary>
        public string GetCurrentRoom()
        {
            if (!Registry) return "None";
            
            if (Registry.TryGetRoomAtPosition(Position, out string roomId))
                return roomId;
            
            return "None";
        }

        /// <summary>
        /// Get room by ID
        /// </summary>
        public RoomZone GetRoom(string roomId)
        {
            return Registry ? Registry.GetRoom(roomId) : null;
        }

        /// <summary>
        /// Get hide spots in room
        /// </summary>
        public System.Collections.Generic.List<AITest.World.HideSpot> GetHideSpotsInRoom(string roomId) // ? FIXED: Full namespace
        {
            return Registry ? Registry.GetHideSpotsInRoom(roomId) : new System.Collections.Generic.List<AITest.World.HideSpot>();
        }

        /// <summary>
        /// Check if player is visible now
        /// </summary>
        public bool CanSeePlayer()
        {
            return perception && perception.PlayerVisible;
        }

        /// <summary>
        /// Get player position (if visible)
        /// </summary>
        public Vector2 GetPlayerPosition()
        {
            return perception && perception.player ? (Vector2)perception.player.position : Vector2.zero;
        }

        /// <summary>
        /// Get best guess position (from worldModel)
        /// </summary>
        public Vector2 GetBestGuessPosition()
        {
            return worldModel ? worldModel.GetBestGuessPosition() : Vector2.zero;
        }

        /// <summary>
        /// Get best guess room (from worldModel)
        /// </summary>
        public string GetBestGuessRoom()
        {
            return worldModel ? worldModel.GetBestGuessRoom() : "None";
        }

        /// <summary>
        /// Score a target position (using perceptron)
        /// </summary>
        public float ScoreTarget(Vector2 targetPos)
        {
            if (!perceptron) return 0.5f;
            
            float distance = Vector2.Distance(Position, targetPos);
            bool los = false; // Skip expensive LOS check
            float lightLevel = 0.5f; // Placeholder
            float heatmapValue = 0f;
            if (TransitionHeatGraph.Instance)
            {
                string roomId = GetCurrentRoom();
                float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(roomId);
                heatmapValue = Mathf.Clamp01(nodeHeat / TransitionHeatGraph.Instance.maxHeatCap);
            }
            float timeSince = worldModel ? worldModel.TimeSinceSeen : 999f;
            bool recentHear = worldModel ? worldModel.HasHeardRecently : false;
            float visScore = perception ? (perception.PlayerVisible ? 1f : 0f) : 0f;
            
            return perceptron.ComputeThreatScore(distance, los, lightLevel, heatmapValue, timeSince, recentHear, visScore);
        }

        /// <summary>
        /// Log message (if debug enabled)
        /// </summary>
        public void Log(string message, string color = "cyan")
        {
            if (showDebugLogs)
                Debug.Log($"<color={color}>[EnemyContext] {message}</color>");
        }
    }
}
