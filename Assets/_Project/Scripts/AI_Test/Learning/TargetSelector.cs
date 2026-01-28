using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AITest.Enemy;
using AITest.World;

namespace AITest.Learning
{
    /// <summary>
    /// Target Selector - Option-specific target selection
    /// 
    /// PROMPT 8: Use perceptron to score and rank targets
    /// - Patrol: Select next room
    /// - HideSpotCheck: Pick top-K hide spots
    /// - ❌ CutoffAmbush: REMOVED (no IntersectionPoints)
    /// </summary>
    public class TargetSelector : MonoBehaviour
    {
        [Header("References")]
        public PerceptronScorer roomScorer;
        public PerceptronScorer hideSpotScorer;
        // public PerceptronScorer intersectionScorer; // ❌ REMOVED: No IntersectionPoints
        
        [Header("Extractors")]
        public FeatureExtractor featureExtractor;
        
        [Header("Exploration")]
        [Tooltip("Enable exploration (randomly pick targets sometimes)")]
        public bool explore = true;
        [Tooltip("Chance to pick random target instead of best")]
        [Range(0f, 1f)] public float epsilon = 0.1f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;

        public string LastPerceptronLog { get; private set; } = "";

        private void Awake()
        {
            // Keep serialized weights if present; only init if missing or wrong dimension
            if (roomScorer == null || roomScorer.FeatureCount != TargetFeatures.FeatureCount)
            {
                roomScorer = new PerceptronScorer(TargetFeatures.FeatureCount, randomize: false); // equal weights baseline
            }
            if (hideSpotScorer == null || hideSpotScorer.FeatureCount != TargetFeatures.FeatureCount)
            {
                hideSpotScorer = new PerceptronScorer(TargetFeatures.FeatureCount, randomize: false);
            }

            if (featureExtractor == null)
                featureExtractor = new FeatureExtractor();
        }

        /// <summary>
        /// ? PROMPT 8: Select next patrol room
        /// </summary>
        public RoomTarget SelectPatrolRoom(EnemyContext ctx, List<string> excludeRooms = null)
        {
            if (ctx == null || ctx.Registry == null)
            {
                Debug.LogError("[TargetSelector] ctx or ctx.Registry is null!");
                return null;
            }
            
            if (featureExtractor == null || roomScorer == null)
            {
                Debug.LogError("[TargetSelector] featureExtractor or roomScorer is null!");
                return null;
            }
            
            // Get all rooms (FILTER OUT JUNCTIONS - only real RoomZone objects)
            var allRooms = ctx.Registry.GetAllRooms();
            
            if (allRooms == null || allRooms.Count == 0)
            {
                Debug.LogWarning("[TargetSelector] No rooms available for patrol selection");
                return null;
            }
            
            // Create room targets
            List<RoomTarget> candidates = new List<RoomTarget>();
            
            foreach (var room in allRooms)
            {
                if (room == null)
                    continue;
                
                // ✅ SKIP junction rooms (use RoomZone.isJunction property)
                if (room.isJunction)
                {
                    if (showDebugLogs)
                        Debug.Log($"<color=gray>[TargetSelector] Skipping junction room: {room.roomId}</color>");
                    continue;
                }
                    
                // Exclude current room or blacklist
                if (excludeRooms != null && excludeRooms.Contains(room.roomId))
                    continue;
                
                try
                {
                    RoomTarget target = new RoomTarget(room.roomId, room.Center, room);
                    
                    // Extract features
                    target.features = featureExtractor.ExtractRoomFeatures(target, ctx);
                    
                    // Score with perceptron
                    target.score = roomScorer.Score(target.features.ToArray());
                    
                    candidates.Add(target);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TargetSelector] Failed to process room {room.roomId}: {ex.Message}");
                    continue;
                }
            }
            
            if (candidates.Count == 0)
                return null;
            
            // Sort by score (descending)
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            
            RoomTarget best = candidates[0];
            
            // ✅ OPTIONAL EXPLORATION (ε-greedy)
            if (explore && Random.value < epsilon && candidates.Count > 1)
            {
                int randomIndex = Random.Range(0, candidates.Count);
                best = candidates[randomIndex];
                if (showDebugLogs) Debug.Log($"<color=yellow>[TargetSelector] Exploring: Picked random room {best.roomId}</color>");
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[TargetSelector] Patrol Room: {best.roomId} (score={best.score:F3})</color>");
                Debug.Log($"  Features: {best.features}");
            }
            
            // LOGGING
            LogTacticalChoice("Patrol", candidates);

            return best;
        }

        /// <summary>
        /// ? PROMPT 8: Select top-K hide spots in room
        /// </summary>
        public List<HideSpotTarget> SelectHideSpots(EnemyContext ctx, string roomId, int topK = 3)
        {
            if (!ctx.Registry)
                return new List<HideSpotTarget>();
            
            // Get hide spots in room
            var hideSpots = ctx.GetHideSpotsInRoom(roomId);
            
            if (hideSpots.Count == 0)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[TargetSelector] No hide spots in room {roomId}");
                
                return new List<HideSpotTarget>();
            }
            
            // Create hide spot targets
            List<HideSpotTarget> candidates = new List<HideSpotTarget>();
            
            foreach (var spot in hideSpots)
            {
                if (!spot) continue;
                
                HideSpotTarget target = new HideSpotTarget(spot);
                
                // Extract features
                target.features = featureExtractor.ExtractHideSpotFeatures(target, ctx);
                
                // Score with perceptron
                target.score = hideSpotScorer.Score(target.features.ToArray());
                
                candidates.Add(target);
            }
            
            if (candidates.Count == 0)
                return new List<HideSpotTarget>();
            
            // Sort by score (descending)
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            
            // Take top-K
            int count = Mathf.Min(topK, candidates.Count);
            
            // ✅ OPTIONAL EXPLORATION: Shuffle the top candidates if exploring
            // This ensures we don't always check spots in the exact same score order
            List<HideSpotTarget> topSpots = candidates.GetRange(0, count);
            
            if (explore && Random.value < epsilon && count > 1)
            {
                // Simple shuffle of the top K spots
                for (int i = 0; i < count; i++)
                {
                    HideSpotTarget temp = topSpots[i];
                    int randomIndex = Random.Range(i, count);
                    topSpots[i] = topSpots[randomIndex];
                    topSpots[randomIndex] = temp;
                }
                 if (showDebugLogs) Debug.Log($"<color=yellow>[TargetSelector] Exploring: Shuffled top {count} hide spots</color>");
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[TargetSelector] Top {count} Hide Spots in {roomId}:</color>");
                foreach (var spot in topSpots)
                {
                    Debug.Log($"  {spot}");
                }
            }
            
            return topSpots;
        }

        /// <summary>
        /// Get top-N rooms (for patrol route)
        /// </summary>
        public List<RoomTarget> SelectPatrolRoute(EnemyContext ctx, int routeLength = 3)
        {
            if (ctx == null || ctx.Registry == null)
            {
                Debug.LogError("[TargetSelector] ctx or ctx.Registry is null!");
                return new List<RoomTarget>();
            }
            
            if (featureExtractor == null || roomScorer == null)
            {
                Debug.LogError("[TargetSelector] featureExtractor or roomScorer is null!");
                return new List<RoomTarget>();
            }
            
            var allRooms = ctx.Registry.GetAllRooms();
            
            if (allRooms == null || allRooms.Count == 0)
            {
                Debug.LogWarning("[TargetSelector] No rooms available for patrol route");
                return new List<RoomTarget>();
            }
            
            // Score all rooms
            List<RoomTarget> candidates = new List<RoomTarget>();
            
            foreach (var room in allRooms)
            {
                if (room == null)
                    continue;
                
                // ✅ SKIP junction rooms (use RoomZone.isJunction property)
                if (room.isJunction)
                {
                    if (showDebugLogs)
                        Debug.Log($"<color=gray>[TargetSelector] Skipping junction room: {room.roomId}</color>");
                    continue;
                }

                // ✅ SKIP current room (don't route to where we are)
                if (room.roomId == ctx.GetCurrentRoom())
                    continue;
                
                try
                {
                    RoomTarget target = new RoomTarget(room.roomId, room.Center, room);
                    target.features = featureExtractor.ExtractRoomFeatures(target, ctx);
                    
                    target.score = roomScorer.Score(target.features.ToArray());
                    candidates.Add(target);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TargetSelector] Failed to process room {room.roomId}: {ex.Message}");
                    continue;
                }
            }
            
            // Sort by score
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            
            // Build log string for top candidates
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Scores: ");
            int logCount = Mathf.Min(5, candidates.Count);
            for(int i=0; i<logCount; i++)
            {
                sb.Append($"{candidates[i].roomId}({candidates[i].score:F1})");
                if (i < logCount - 1) sb.Append(", ");
            }
            LastPerceptronLog = sb.ToString();

            // Take top-N
            int count = Mathf.Min(routeLength, candidates.Count);
            List<RoomTarget> route = candidates.GetRange(0, count);
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[TargetSelector] Patrol Route ({count} rooms):</color>");
                foreach (var room in route)
                {
                    Debug.Log($"  {room}");
                }
            }
            
            return route;
        }

        /// <summary>
        /// Train perceptron with feedback (optional)
        /// </summary>
        public void TrainRoomScorer(RoomTarget room, float targetScore)
        {
            roomScorer.Update(room.features.ToArray(), targetScore);
            
            if (showDebugLogs)
                Debug.Log($"<color=lime>[TargetSelector] Room scorer trained: {room.roomId} ? {targetScore:F2}</color>");
        }

        public void TrainHideSpotScorer(HideSpotTarget hideSpot, float targetScore)
        {
            hideSpotScorer.Update(hideSpot.features.ToArray(), targetScore);
        }

        /// <summary>
        /// Reset all perceptrons
        /// </summary>
        [ContextMenu("Reset Perceptrons")]
        public void ResetPerceptrons()
        {
            roomScorer.RandomizeWeights();
            hideSpotScorer.RandomizeWeights();
            
            Debug.Log("<color=yellow>[TargetSelector] Perceptrons reset</color>");
        }

        /// <summary>
        /// Print perceptron weights
        /// </summary>
        [ContextMenu("Print Weights")]
        public void PrintWeights()
        {
            Debug.Log("<color=cyan>===== ROOM SCORER =====</color>");
            Debug.Log(roomScorer.GetWeightSummary());
            
            Debug.Log("<color=cyan>===== HIDESPOT SCORER =====</color>");
            Debug.Log(hideSpotScorer.GetWeightSummary());

        }
        // --- LOGGING & METRICS ---
        private int searchEfficiencyCounter = 0;
        private Dictionary<AITest.Enemy.EnemyMode, int> actionDistribution = new Dictionary<AITest.Enemy.EnemyMode, int>();

        /// <summary>
        /// 1. Log Perceptron Weights to CSV
        /// </summary>
        public void LogPerceptronWeights()
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "../TrainingData");
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            roomScorer.LogWeightsToCSV(System.IO.Path.Combine(dir, "perceptron_room_weights.csv"));
            hideSpotScorer.LogWeightsToCSV(System.IO.Path.Combine(dir, "perceptron_hidespot_weights.csv"));
        }

        /// <summary>
        /// 2. Log Tactical Choice (Candidates & Scores)
        /// </summary>
        private void LogTacticalChoice(string decisionType, List<RoomTarget> candidates)
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "../TrainingData");
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "tactical_choices.csv");

            try
            {
                if (!System.IO.File.Exists(path))
                    System.IO.File.WriteAllText(path, "Time,DecisionType,Winner,WinnerScore,CandidateCount,Top5Candidates\n");

                var best = candidates[0];
                string top5 = "";
                int count = Mathf.Min(5, candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    top5 += $"{candidates[i].roomId}({candidates[i].score:F2})|";
                }

                string line = $"{Time.time:F1},{decisionType},{best.roomId},{best.score:F3},{candidates.Count},{top5}\n";
                System.IO.File.AppendAllText(path, line);
            }
            catch { }
        }

        /// <summary>
        /// 3. Track Search Efficiency (Called when entering a room or checking a spot)
        /// </summary>
        public void IncrementSearchEfficiency()
        {
            searchEfficiencyCounter++;
        }

        public void ResetSearchEfficiency()
        {
            searchEfficiencyCounter = 0;
        }

        public void LogCaptureEfficiency(float timeToCapture)
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "../TrainingData");
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "search_efficiency.csv");

            try
            {
                if (!System.IO.File.Exists(path))
                    System.IO.File.WriteAllText(path, "Time,RoomsChecked,TimeToCapture\n");

                string line = $"{Time.time:F1},{searchEfficiencyCounter},{timeToCapture:F2}\n";
                System.IO.File.AppendAllText(path, line);
            }
            catch { }
        }

        /// <summary>
        /// 4. Track Macro Action (Called by EnemyBrain)
        /// </summary>
        public void TrackAction(AITest.Enemy.EnemyMode mode)
        {
            if (!actionDistribution.ContainsKey(mode))
                actionDistribution[mode] = 0;
            
            actionDistribution[mode]++;
        }

        public void LogActionDistribution(int episode)
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "../TrainingData");
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "action_distribution.csv");

            try
            {
                if (!System.IO.File.Exists(path))
                    System.IO.File.WriteAllText(path, "Episode,Non,Patrol,Investigate,SearchPeak,Sweep,HideCheck,HeatSweep,Ambush\n");

                // Map enum to columns manually or iterating
                int[] counts = new int[8]; // Assuming 8 modes define in EnemyMode
                foreach(var kvp in actionDistribution)
                {
                    if ((int)kvp.Key < 8) counts[(int)kvp.Key] = kvp.Value;
                }

                string line = $"{episode}";
                for(int i=0; i<8; i++) line += $",{counts[i]}";
                line += "\n";

                System.IO.File.AppendAllText(path, line);
            }
            catch { }
        }
    }
}
