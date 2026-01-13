using UnityEngine;
using AITest.Enemy;
using System.Linq;
using AITest.Heat;

namespace AITest.UI
{
    /// <summary>
    /// Debug Visualizer - Gizmos for learning visualization
    /// 
    /// PROMPT 11: Scene view debug drawings
    /// - Last seen/heard positions
    /// - Current destination
    /// - Heat values per room
    /// - Pathfinding routes
    /// - Option targets
    /// </summary>
    public class DebugVisualizer : MonoBehaviour
    {
        [Header("References")]
        public EnemyContext context;
        public AIAgentMover mover;
        
        [Header("Visualization Toggles")]
        [Tooltip("Show last seen position")]
        public bool showLastSeen = true;
        
        [Tooltip("Show last heard position")]
        public bool showLastHeard = true;
        
        [Tooltip("Show current destination")]
        public bool showDestination = true;
        
        [Tooltip("Show room heat values")]
        public bool showRoomHeat = true;
        
        [Tooltip("Show hide spots")]
        public bool showHideSpots = true;
        
        [Header("Visual Settings")]
        public Color lastSeenColor = Color.red;
        public Color lastHeardColor = Color.yellow;
        public Color destinationColor = Color.cyan;
        public Color heatColor = Color.magenta;
        
        [Range(0.5f, 3f)] public float markerSize = 1f;
        [Range(0.1f, 1f)] public float heatAlpha = 0.5f;

        [Header("Academic Visualization (OnGUI)")]
        public bool showAcademicOverlay = true;
        public EnemyBrain enemyBrain;
        public AITest.Learning.EpisodeManager episodeManager;
        public float guiScale = 1.0f;

        private void Awake()
        {
            // Auto-find
            if (!context) context = GetComponent<EnemyContext>();
            if (!mover) mover = GetComponent<AIAgentMover>();
            
            // Auto-find Brain and Manager if not set
            if (!enemyBrain) enemyBrain = GetComponent<EnemyBrain>();
            if (!episodeManager) episodeManager = FindFirstObjectByType<AITest.Learning.EpisodeManager>();
        }

        private void OnDrawGizmos()
        {
            if (!context)
                return;
            
            // Last seen
            if (showLastSeen && context.worldModel && context.worldModel.HasSeenRecently)
            {
                DrawMarker(context.worldModel.LastSeenPos, lastSeenColor, "LAST SEEN");
            }
            
            // Last heard
            if (showLastHeard && context.worldModel && context.worldModel.HasHeardRecently)
            {
                DrawMarker(context.worldModel.LastHeardPos, lastHeardColor, "LAST HEARD");
            }
            
            // Destination
            if (showDestination && mover && mover.Destination.HasValue)
            {
                DrawMarker(mover.Destination.Value, destinationColor, "DESTINATION");
                
                // Line from enemy to destination
                Gizmos.color = new Color(destinationColor.r, destinationColor.g, destinationColor.b, 0.3f);
                Gizmos.DrawLine(transform.position, mover.Destination.Value);
            }
            
            // Hide spots
            if (showHideSpots && context.Registry)
            {
                DrawHideSpots();
            }
        }

        private void OnGUI()
        {
            if (!showAcademicOverlay || !enemyBrain || !enemyBrain.qLearningPolicy.QTableSize.Equals(0) && enemyBrain.qLearningPolicy == null)
                return;

            // Scale GUI for high-DPI screens if needed
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1));

            DrawStateInfo();
            DrawQValues();
            DrawTrainingStats();

            GUI.matrix = oldMatrix;
        }

        private void DrawStateInfo()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 230), "Current State", GUI.skin.window);
            
            if (enemyBrain.useSimpleStateExtractor && enemyBrain.simpleStateExtractor)
            {
                var state = enemyBrain.simpleStateExtractor.ExtractState();
                
                // 1. Visible
                GUILayout.Label($"Player Visible: {(state.playerVisible == 1 ? "YES" : "NO")}");
                
                // 2. Distance
                string distStr = state.distanceBucket == 0 ? "CLOSE" : (state.distanceBucket == 1 ? "MEDIUM" : "FAR");
                GUILayout.Label($"Distance: {distStr}");
                
                // 3. Last Seen Sector
                string sectorStr = state.lastSeenSector == 0 ? "SAME" : (state.lastSeenSector == 1 ? "DIFFERENT" : "UNKNOWN");
                GUILayout.Label($"Last Seen Sector: {sectorStr}");
                
                // 4. Time Since Contact
                string timeStr = state.timeSinceContactBucket == 0 ? "RECENT (<2s)" : (state.timeSinceContactBucket == 1 ? "OLD (3-6s)" : "VERY OLD");
                GUILayout.Label($"Time Since Contact: {timeStr}");

                // 5. Heat Here
                string heatHere = state.heatHereBucket == 2 ? "HOT" : (state.heatHereBucket == 1 ? "WARM" : "COLD");
                GUILayout.Label($"Heat Here: {heatHere}");
                
                // 6. Heat Nearby
                string heatNear = state.heatNearbyBucket == 2 ? "HOT" : (state.heatNearbyBucket == 1 ? "WARM" : "COLD");
                GUILayout.Label($"Heat Nearby: {heatNear}");
            }
            else
            {
                GUILayout.Label("Using Full State Extractor (Complex)");
            }

            GUILayout.Space(10);
            GUILayout.Label($"Current Mode: {enemyBrain.currentMode}");
            GUILayout.EndArea();
        }

        private void DrawQValues()
        {
            if (enemyBrain.qLearningPolicy == null) return;

            int stateKey = enemyBrain.lastStateKey;
            float[] qValues = enemyBrain.qLearningPolicy.GetQValues(stateKey);

            if (qValues == null) return;

            float maxQ = -9999f;
            for (int i=0; i<qValues.Length; i++) if(qValues[i] > maxQ) maxQ = qValues[i];
            
            // Prevent div by zero
            if (Mathf.Abs(maxQ) < 0.01f) maxQ = 1f;

            GUILayout.BeginArea(new Rect(10, 220, 300, 250), "Q-Values (Decision Weights)", GUI.skin.window);
            
            for (int i = 0; i < qValues.Length; i++)
            {
                EnemyMode mode = (EnemyMode)i;
                float val = qValues[i];
                
                // Visualize bar
                GUILayout.BeginHorizontal();
                GUILayout.Label(mode.ToString(), GUILayout.Width(120));
                
                // Draw bar logic
                float width = 130 * (Mathf.Clamp(val, 0, maxQ) / maxQ);
                if (val < 0) width = 10; // minimal for negative
                
                GUILayout.Space(5);
                GUI.color = (val == maxQ) ? Color.green : Color.white;
                GUILayout.Box("", GUILayout.Width(width), GUILayout.Height(15));
                GUI.color = Color.white;
                
                GUILayout.Label(val.ToString("F2"));
                GUILayout.EndHorizontal();
            }

            GUILayout.Label($"Epsilon (Exploration): {enemyBrain.qLearningPolicy.epsilon:P0}");
            GUILayout.EndArea();
        }

        private void DrawTrainingStats()
        {
            if (!episodeManager || !episodeManager.metrics) return;

            var m = episodeManager.metrics;

            // Increase window height to accommodate breakdown
            GUILayout.BeginArea(new Rect(Screen.width / guiScale - 310, 10, 300, 350), "Training Metrics", GUI.skin.window);
            GUILayout.Label($"Episodes: {m.EpisodesCompleted}");
            GUILayout.Label($"Success Rate: {m.GetSuccessRate():F1}%");
            GUILayout.Label($"Avg Reward: {m.AverageReward:F2}");
            GUILayout.Label($"Rolling Avg (100): {m.GetRollingAverageReward():F2}");
            GUILayout.Label($"Total Steps: {episodeManager.episodeSteps}");
            
            GUILayout.Space(10);
            if (GUILayout.Button("Export CSV"))
            {
                m.ExportToCSV();
            }

            GUILayout.Space(10);
            GUILayout.Label("Last Reward Breakdown:");
            
            if (enemyBrain.rewardCalculator != null)
            {
                var breakdown = enemyBrain.rewardCalculator.LastRewardBreakdown;
                if (breakdown.Count > 0)
                {
                    foreach (var item in breakdown)
                    {
                        Color c = item.value >= 0 ? Color.green : Color.red;
                        string sign = item.value >= 0 ? "+" : "";
                        
                        GUILayout.BeginHorizontal();
                        GUI.color = c;
                        GUILayout.Label($"{item.reason}");
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"{sign}{item.value:F2}");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("(Waiting for next update...)");
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Draw position marker
        /// </summary>
        private void DrawMarker(Vector2 position, Color color, string label)
        {
            Gizmos.color = color;
            
            // Cross marker
            float size = markerSize;
            Gizmos.DrawLine(position + Vector2.up * size, position + Vector2.down * size);
            Gizmos.DrawLine(position + Vector2.left * size, position + Vector2.right * size);
            
            // Circle
            Gizmos.DrawWireSphere(position, size * 0.7f);
            
            #if UNITY_EDITOR
            // Label
            GUIStyle style = new GUIStyle();
            style.normal.textColor = color;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            
            Vector3 labelPos = position + Vector2.up * (size + 0.5f);
            UnityEditor.Handles.Label(labelPos, label, style);
            #endif
        }

        /// <summary>
        /// Draw room heat values
        /// </summary>
        private void DrawRoomHeat()
        {
           // (Kept as is - removed for brevity in replacement but should be kept if not replacing entire file)
           // Actually, I am replacing lines 49-225 which covers OnDrawGizmos and Helpers.
           // I need to be careful not to delete DrawRoomHeat if it is outside range, OR include it.
           // Looking at file content:
           // OnDrawGizmos ends at line 90.
           // DrawMarker is 95-117.
           // DrawRoomHeat is 122-174.
           // DrawHideSpots is 179-222.
           // I will include ALL helper methods to be safe as I am replacing a large chunk.
           
           if (context == null || context.Registry == null || TransitionHeatGraph.Instance == null)
                return;
                
            var allRooms = context.Registry.GetAllRooms();
            
            if (allRooms == null)
                return;
            
            foreach (var room in allRooms)
            {
                if (room == null)
                    continue;
                
                try
                {
                    float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(room.roomId);
                    float heat01 = Mathf.Clamp01(nodeHeat / TransitionHeatGraph.Instance.maxHeatCap);
                    if (heat01 < 0.05f)
                        continue; // Skip very low heat
                    
                    // Color intensity based on heat
                    Color color = Color.Lerp(Color.blue, Color.red, heat01);
                    color.a = heatAlpha;
                    
                    Gizmos.color = color;
                    
                    // Draw filled sphere at room center
                    Gizmos.DrawSphere(room.Center, markerSize * heat01);
                    
                    // Draw wireframe of room bounds
                    Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
                    Gizmos.DrawWireCube(room.Bounds.center, room.Bounds.size);
                    
                    #if UNITY_EDITOR
                    // Heat value label
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = Color.white;
                    style.fontSize = 14;
                    style.fontStyle = FontStyle.Bold;
                    
                    Vector3 labelPos = room.Center + Vector2.up * 2f;
                    UnityEditor.Handles.Label(labelPos, $"{room.roomId}\n{heat01:P0}", style);
                    #endif
                }
                catch (System.Exception)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Draw hide spots
        /// </summary>
        private void DrawHideSpots()
        {
            if (context == null || context.Registry == null)
                return;
                
            var allRooms = context.Registry.GetAllRooms();
            
            if (allRooms == null)
                return;
            
            foreach (var room in allRooms)
            {
                if (room == null)
                    continue;
                    
                var hideSpots = context.GetHideSpotsInRoom(room.roomId);
                
                if (hideSpots == null)
                    continue;
                
                foreach (var spot in hideSpots)
                {
                    if (!spot)
                        continue;
                    
                    // Color based on Bayesian probability
                    Color color = Color.Lerp(Color.green, Color.red, spot.Probability);
                    color.a = 0.6f;
                    
                    Gizmos.color = color;
                    Gizmos.DrawWireSphere(spot.Position, 0.8f);
                    
                    #if UNITY_EDITOR
                    // Probability label
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = color;
                    style.fontSize = 10;
                    
                    Vector3 labelPos = spot.Position + Vector2.up * 1.2f;
                    UnityEditor.Handles.Label(labelPos, $"{spot.Probability:P0}", style);
                    #endif
                }
            }
        }
    }
}
