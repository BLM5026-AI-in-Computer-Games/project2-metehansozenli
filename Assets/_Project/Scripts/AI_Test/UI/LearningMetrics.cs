using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AITest.UI
{
    /// <summary>
    /// Learning Metrics - Performance tracking for Q-Learning
    /// 
    /// PROMPT 11 + PROFESSOR FEEDBACK: Enhanced metrics for academic evaluation
    /// - Learning curve (episode ? reward)
    /// - Success rate tracking
    /// - State visit frequency
    /// - Q-value convergence
    /// - Action distribution
    /// - CSV export for plotting
    /// </summary>
    public class LearningMetrics : MonoBehaviour
    {
        [Header("Time Metrics")]
        public float TimeToFirstDetection = 0f;
        public float CaptureTime = 0f;
        public float SessionTime = 0f;
        
        [Header("Search Metrics")]
        public int CorrectRoomSearches = 0;
        public int WrongRoomSearches = 0;
        public int TotalRoomSearches => CorrectRoomSearches + WrongRoomSearches;
        
        [Header("Episode Metrics")]
        public int EpisodesCompleted = 0;
        public float TotalReward = 0f;
        public float AverageReward => EpisodesCompleted > 0 ? TotalReward / EpisodesCompleted : 0f;
        
        [Header("Capture State")]
        public bool PlayerCaptured = false;
        public bool FirstDetection = false;
        
        [Header("Baseline Comparison")]
        public float BaselineAverageReward = 0f;
        public float LearningAverageReward = 0f;
        public float ImprovementPercent => BaselineAverageReward > 0 
            ? ((LearningAverageReward - BaselineAverageReward) / BaselineAverageReward) * 100f 
            : 0f;
        
        [Header("Enhanced Tracking (Professor Feedback)")]
        [Tooltip("Track last N episodes for rolling average")]
        public int rollingWindowSize = 25;
        
        [Tooltip("CSV export path (relative to project)")]
        public string csvExportPath = "TrainingData/learning_metrics.csv";
        
        [Tooltip("Auto-export CSV every N episodes")]
        public int autoExportInterval = 50;
        
        [Tooltip("Enable detailed logging")]
        public bool enableDetailedLogs = false;
        
        // Episode history
        private List<EpisodeData> episodeHistory = new List<EpisodeData>();
        
        // State visit tracking
        private Dictionary<int, int> stateVisitCounts = new Dictionary<int, int>();
        
        // Action distribution tracking
        private Dictionary<int, int> actionCounts = new Dictionary<int, int>();
        
        // Q-value tracking (for convergence analysis)
        private List<float> qValueChanges = new List<float>();
        
        // Internal
        private float sessionStartTime;
        private float lastQValueSum = 0f;

        private void Start()
        {
            ResetSession();
        }

        private void Update()
        {
            // Update session time
            SessionTime = Time.time - sessionStartTime;
        }

        /// <summary>
        /// Mark first detection
        /// </summary>
        public void MarkFirstDetection()
        {
            if (FirstDetection)
                return;
            
            FirstDetection = true;
            TimeToFirstDetection = Time.time - sessionStartTime;
            
            Debug.Log($"<color=lime>[Metrics] First detection at {TimeToFirstDetection:F1}s</color>");
        }

        /// <summary>
        /// Mark capture
        /// </summary>
        public void MarkCapture()
        {
            if (PlayerCaptured)
                return;
            
            PlayerCaptured = true;
            CaptureTime = Time.time - sessionStartTime;
            
            Debug.Log($"<color=lime>[Metrics] Player captured at {CaptureTime:F1}s!</color>");
        }

        /// <summary>
        /// Mark room search (correct/wrong)
        /// </summary>
        public void MarkRoomSearch(bool correct)
        {
            if (correct)
            {
                CorrectRoomSearches++;
                Debug.Log($"<color=green>[Metrics] Correct room search! ({CorrectRoomSearches}/{TotalRoomSearches})</color>");
            }
            else
            {
                WrongRoomSearches++;
                Debug.Log($"<color=yellow>[Metrics] Wrong room search ({WrongRoomSearches}/{TotalRoomSearches})</color>");
            }
        }

        /// <summary>
        /// ? NEW: Record episode data
        /// </summary>
        public void RecordEpisode(int episodeNum, float reward, float duration, int steps, bool success)
        {
            EpisodeData data = new EpisodeData
            {
                episodeNumber = episodeNum,
                totalReward = reward,
                duration = duration,
                steps = steps,
                success = success,
                timestamp = Time.time
            };
            
            episodeHistory.Add(data);
            
            EpisodesCompleted = episodeNum;
            TotalReward += reward;
            
            if (enableDetailedLogs)
            {
                Debug.Log($"<color=cyan>[Metrics] Episode {episodeNum}: Reward={reward:F2}, Duration={duration:F2}s, Steps={steps}, Success={success}</color>");
            }
            
            // Auto-export
            if (autoExportInterval > 0 && episodeNum % autoExportInterval == 0)
            {
                ExportToCSV();
            }
        }

        /// <summary>
        /// ? NEW: Track state visit
        /// </summary>
        public void TrackStateVisit(int stateKey)
        {
            if (!stateVisitCounts.ContainsKey(stateKey))
            {
                stateVisitCounts[stateKey] = 0;
            }
            
            stateVisitCounts[stateKey]++;
        }

        /// <summary>
        /// ? NEW: Track action selection
        /// </summary>
        public void TrackActionSelection(int actionIndex)
        {
            if (!actionCounts.ContainsKey(actionIndex))
            {
                actionCounts[actionIndex] = 0;
            }
            
            actionCounts[actionIndex]++;
        }

        /// <summary>
        /// ? NEW: Track Q-value change (for convergence)
        /// </summary>
        public void TrackQValueChange(float currentQSum)
        {
            float change = Mathf.Abs(currentQSum - lastQValueSum);
            qValueChanges.Add(change);
            lastQValueSum = currentQSum;
        }

        /// <summary>
        /// ? NEW: Get rolling average reward
        /// </summary>
        public float GetRollingAverageReward(int windowSize = -1)
        {
            if (windowSize == -1)
                windowSize = rollingWindowSize;
            
            if (episodeHistory.Count == 0)
                return 0f;
            
            int startIndex = Mathf.Max(0, episodeHistory.Count - windowSize);
            float sum = 0f;
            
            for (int i = startIndex; i < episodeHistory.Count; i++)
            {
                sum += episodeHistory[i].totalReward;
            }
            
            return sum / (episodeHistory.Count - startIndex);
        }

        /// <summary>
        /// ? NEW: Get success rate
        /// </summary>
        public float GetSuccessRate(int windowSize = -1)
        {
            if (windowSize == -1)
                windowSize = rollingWindowSize;
            
            if (episodeHistory.Count == 0)
                return 0f;
            
            int startIndex = Mathf.Max(0, episodeHistory.Count - windowSize);
            int successes = 0;
            
            for (int i = startIndex; i < episodeHistory.Count; i++)
            {
                if (episodeHistory[i].success)
                    successes++;
            }
            
            return (float)successes / (episodeHistory.Count - startIndex) * 100f;
        }

        /// <summary>
        /// ? NEW: Get most visited states (top N)
        /// </summary>
        public List<KeyValuePair<int, int>> GetMostVisitedStates(int topN = 20)
        {
            var sortedStates = new List<KeyValuePair<int, int>>(stateVisitCounts);
            sortedStates.Sort((a, b) => b.Value.CompareTo(a.Value));
            
            return sortedStates.GetRange(0, Mathf.Min(topN, sortedStates.Count));
        }

        /// <summary>
        /// ? NEW: Get action distribution
        /// </summary>
        public Dictionary<int, float> GetActionDistribution()
        {
            Dictionary<int, float> distribution = new Dictionary<int, float>();
            int totalActions = 0;
            
            foreach (var count in actionCounts.Values)
            {
                totalActions += count;
            }
            
            if (totalActions == 0)
                return distribution;
            
            foreach (var kvp in actionCounts)
            {
                distribution[kvp.Key] = (float)kvp.Value / totalActions * 100f;
            }
            
            return distribution;
        }

        /// <summary>
        /// ? NEW: Export data to CSV (for plotting in Python/Excel)
        /// </summary>
        [ContextMenu("Export to CSV")]
        public void ExportToCSV()
        {
            if (episodeHistory.Count == 0)
            {
                Debug.LogWarning("[Metrics] No episode data to export!");
                return;
            }
            
            // Create directory if needed
            string fullPath = Path.Combine(Application.dataPath, "..", csvExportPath);
            string directory = Path.GetDirectoryName(fullPath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Build CSV
            StringBuilder csv = new StringBuilder();
            
            // Header
            csv.AppendLine("Episode,Reward,Duration,Steps,Success,RollingAvgReward,SuccessRate");
            
            // Data rows
            for (int i = 0; i < episodeHistory.Count; i++)
            {
                var ep = episodeHistory[i];
                
                // Calculate rolling metrics
                float rollingAvg = GetRollingAverageRewardAt(i);
                float successRate = GetSuccessRateAt(i);
                
                csv.AppendLine($"{ep.episodeNumber},{ep.totalReward:F2},{ep.duration:F2},{ep.steps},{(ep.success ? 1 : 0)},{rollingAvg:F2},{successRate:F2}");
            }
            
            // Write file
            File.WriteAllText(fullPath, csv.ToString());
            
            Debug.Log($"<color=lime>[Metrics] CSV exported to: {fullPath} ({episodeHistory.Count} episodes)</color>");
        }

        /// <summary>
        /// Get rolling average at specific episode index
        /// </summary>
        private float GetRollingAverageRewardAt(int episodeIndex)
        {
            int startIndex = Mathf.Max(0, episodeIndex - rollingWindowSize + 1);
            float sum = 0f;
            
            for (int i = startIndex; i <= episodeIndex; i++)
            {
                sum += episodeHistory[i].totalReward;
            }
            
            return sum / (episodeIndex - startIndex + 1);
        }

        /// <summary>
        /// Get success rate at specific episode index
        /// </summary>
        private float GetSuccessRateAt(int episodeIndex)
        {
            int startIndex = Mathf.Max(0, episodeIndex - rollingWindowSize + 1);
            int successes = 0;
            
            for (int i = startIndex; i <= episodeIndex; i++)
            {
                if (episodeHistory[i].success)
                    successes++;
            }
            
            return (float)successes / (episodeIndex - startIndex + 1) * 100f;
        }

        /// <summary>
        /// End episode (record reward)
        /// </summary>
        public void EndEpisode(float episodeReward, bool isLearning)
        {
            EpisodesCompleted++;
            TotalReward += episodeReward;
            
            // Update baseline/learning averages
            if (isLearning)
            {
                LearningAverageReward = (LearningAverageReward * (EpisodesCompleted - 1) + episodeReward) / EpisodesCompleted;
            }
            else
            {
                BaselineAverageReward = (BaselineAverageReward * (EpisodesCompleted - 1) + episodeReward) / EpisodesCompleted;
            }
            
            Debug.Log($"<color=cyan>[Metrics] Episode {EpisodesCompleted} complete! Reward: {episodeReward:F2}, Avg: {AverageReward:F2}</color>");
        }

        /// <summary>
        /// Reset session
        /// </summary>
        public void ResetSession()
        {
            sessionStartTime = Time.time;
            SessionTime = 0f;
            TimeToFirstDetection = 0f;
            CaptureTime = 0f;
            PlayerCaptured = false;
            FirstDetection = false;
            CorrectRoomSearches = 0;
            WrongRoomSearches = 0;
            
            Debug.Log("<color=yellow>[Metrics] Session reset</color>");
        }

        /// <summary>
        /// Reset all metrics (for new experiment)
        /// </summary>
        [ContextMenu("Reset All Metrics")]
        public void ResetAll()
        {
            ResetSession();
            EpisodesCompleted = 0;
            TotalReward = 0f;
            BaselineAverageReward = 0f;
            LearningAverageReward = 0f;
            
            episodeHistory.Clear();
            stateVisitCounts.Clear();
            actionCounts.Clear();
            qValueChanges.Clear();
            
            Debug.Log("<color=red>[Metrics] All metrics reset!</color>");
        }

        /// <summary>
        /// Print report
        /// </summary>
        [ContextMenu("Print Report")]
        public void PrintReport()
        {
            Debug.Log("<color=cyan>===== LEARNING METRICS REPORT =====</color>");
            Debug.Log($"Session Time: {SessionTime:F1}s");
            Debug.Log($"Time to First Detection: {TimeToFirstDetection:F1}s");
            Debug.Log($"Capture Time: {(PlayerCaptured ? CaptureTime.ToString("F1") + "s" : "Not captured")}");
            Debug.Log($"Room Searches: Correct={CorrectRoomSearches}, Wrong={WrongRoomSearches}, Total={TotalRoomSearches}");
            Debug.Log($"Search Accuracy: {(TotalRoomSearches > 0 ? (CorrectRoomSearches * 100f / TotalRoomSearches).ToString("F1") : "0")}%");
            Debug.Log($"Episodes: {EpisodesCompleted}");
            Debug.Log($"Total Reward: {TotalReward:F2}");
            Debug.Log($"Average Reward: {AverageReward:F2}");
            Debug.Log($"Rolling Avg ({rollingWindowSize} episodes): {GetRollingAverageReward():F2}");
            Debug.Log($"Success Rate: {GetSuccessRate():F1}%");
            Debug.Log($"Baseline Avg: {BaselineAverageReward:F2}");
            Debug.Log($"Learning Avg: {LearningAverageReward:F2}");
            Debug.Log($"Improvement: {ImprovementPercent:F1}%");
            Debug.Log($"Unique States Visited: {stateVisitCounts.Count}");
            Debug.Log($"Total Actions Taken: {GetTotalActionCount()}");
            
            // Print action distribution
            Debug.Log("<color=yellow>Action Distribution:</color>");
            var distribution = GetActionDistribution();
            foreach (var kvp in distribution)
            {
                Debug.Log($"  Action {kvp.Key}: {kvp.Value:F1}%");
            }
        }

        /// <summary>
        /// Get total action count
        /// </summary>
        private int GetTotalActionCount()
        {
            int total = 0;
            foreach (var count in actionCounts.Values)
            {
                total += count;
            }
            return total;
        }

        /// <summary>
        /// Episode data structure
        /// </summary>
        [System.Serializable]
        private struct EpisodeData
        {
            public int episodeNumber;
            public float totalReward;
            public float duration;
            public int steps;
            public bool success;
            public float timestamp;
        }
    }
}
