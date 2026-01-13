using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System;
using AITest.Enemy;

namespace AITest.Metrics
{
    /// <summary>
    /// Metrics Collector - Comprehensive training metrics for Q-Learning evaluation
    /// 
    /// PURPOSE: Academic-grade data collection for learning evidence
    /// - Episode-level aggregation (reward, duration, success, steps)
    /// - Action distribution tracking (8 actions)
    /// - TD error tracking (convergence proxy)
    /// - State visitation frequency
    /// - Rolling window statistics
    /// - CSV export for offline plotting
    /// 
    /// INTEGRATION: Minimal intrusion singleton pattern
    /// - Subscribes to existing events (ChaseExecutor.OnPlayerCaptured)
    /// - Called from EpisodeManager.EndEpisode() via MetricsCollector.Instance
    /// - Called from EnemyBrain.StartNewOption() for action tracking
    /// </summary>
    public class MetricsCollector : MonoBehaviour
    {
        #region Singleton
        public static MetricsCollector Instance { get; private set; }
        #endregion

        #region Configuration
        [Header("Collection Settings")]
        [Tooltip("Enable metrics collection (disable for production)")]
        public bool enableMetricsCollection = true;

        [Tooltip("Enable CSV auto-export")]
        public bool enableCSVExport = true;

        [Tooltip("Auto-export every N episodes (0 = manual only)")]
        public int csvExportIntervalEpisodes = 50;

        [Tooltip("Enable detailed debug logs")]
        public bool enableDetailedLogs = false;

        [Header("Rolling Window")]
        [Tooltip("Window size for rolling averages")]
        [Range(10, 200)]
        public int rollingWindowSize = 100;

        [Header("Output Paths")]
        [Tooltip("CSV output path (relative to project root)")]
        public string csvOutputPath = "TrainingData/metrics.csv";

        [Tooltip("Benchmark output path")]
        public string benchmarkOutputPath = "TrainingData/benchmarks.csv";

        [Header("References")]
        [Tooltip("Enemy Brain (for Q-learning access)")]
        public AITest.Enemy.EnemyBrain enemyBrain;

        [Tooltip("Episode Manager (for episode tracking)")]
        public AITest.Learning.EpisodeManager episodeManager;
        #endregion

        #region Data Structures
        /// <summary>
        /// Single episode record
        /// </summary>
        [System.Serializable]
        public class EpisodeRecord
        {
            public int episodeNumber;
            public string timestamp;
            public float duration;
            public float totalReward;
            public int steps;
            public bool success;  // Enemy captured player
            public bool captured; // Same as success (for clarity)
            public float timeToFirstContact; // Time to first see/hear player
            public float epsilon; // Exploration rate at this episode
            public float meanTDError; // Average TD error this episode
            public int[] actionCounts; // Counts for each of 8 actions
            public int uniqueStatesVisited; // Number of unique states this episode
            public float captureTime; // Time to capture (if successful, else -1)

            public EpisodeRecord()
            {
                actionCounts = new int[8]; // 8 actions in EnemyMode
            }
        }

        /// <summary>
        /// Rolling window statistics
        /// </summary>
        [System.Serializable]
        public class RollingStats
        {
            public float avgReward;
            public float avgDuration;
            public float successRate;
            public float avgTimeToCapture; // Only successful episodes
            public float avgTDError;
            public int[] totalActionCounts;

            public RollingStats()
            {
                totalActionCounts = new int[8];
            }
        }
        #endregion

        #region State
        // Episode history (all episodes)
        private List<EpisodeRecord> episodeHistory = new List<EpisodeRecord>();

        // Current episode tracking
        private EpisodeRecord currentEpisode;
        private List<float> currentTDErrors = new List<float>();
        private HashSet<int> currentStatesVisited = new HashSet<int>();
        private float episodeStartTime;
        private float firstContactTime = -1f;
        private bool capturedThisEpisode = false;

        // State visitation tracking (across all episodes)
        private Dictionary<int, int> globalStateVisits = new Dictionary<int, int>();

        // Rolling window cache
        private RollingStats cachedRollingStats = new RollingStats();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-find references
            if (!enemyBrain) enemyBrain = FindFirstObjectByType<AITest.Enemy.EnemyBrain>();
            if (!episodeManager) episodeManager = FindFirstObjectByType<AITest.Learning.EpisodeManager>();

            // Subscribe to capture event
            var chaseExecutor = FindFirstObjectByType<AITest.Enemy.ChaseExecutor>();
            if (chaseExecutor)
            {
                chaseExecutor.OnPlayerCaptured += OnPlayerCaptured;
            }

            Debug.Log("[MetricsCollector] Initialized - Ready to collect training data");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            var chaseExecutor = FindFirstObjectByType<AITest.Enemy.ChaseExecutor>();
            if (chaseExecutor)
            {
                chaseExecutor.OnPlayerCaptured -= OnPlayerCaptured;
            }
        }
        #endregion

        #region Public API - Episode Tracking
        /// <summary>
        /// Called at START of episode (from EpisodeManager.StartEpisode)
        /// </summary>
        public void RecordEpisodeStart(int episodeNumber)
        {
            if (!enableMetricsCollection) return;

            currentEpisode = new EpisodeRecord
            {
                episodeNumber = episodeNumber,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                epsilon = enemyBrain?.qLearningPolicy?.epsilon ?? 1f
            };

            currentTDErrors.Clear();
            currentStatesVisited.Clear();
            episodeStartTime = Time.time;
            firstContactTime = -1f;
            capturedThisEpisode = false;

            if (enableDetailedLogs)
            {
                Debug.Log($"[Metrics] Episode {episodeNumber} started | ε={currentEpisode.epsilon:F3}");
            }
        }

        /// <summary>
        /// Called at END of episode (from EpisodeManager.EndEpisode)
        /// </summary>
        public void RecordEpisodeComplete(int episodeNumber, float totalReward, float duration, int steps, bool success)
        {
            if (!enableMetricsCollection || currentEpisode == null) return;

            // Finalize current episode record
            currentEpisode.duration = duration;
            currentEpisode.totalReward = totalReward;
            currentEpisode.steps = steps;
            currentEpisode.success = success;
            currentEpisode.captured = success;
            currentEpisode.uniqueStatesVisited = currentStatesVisited.Count;

            // Calculate mean TD error
            if (currentTDErrors.Count > 0)
            {
                currentEpisode.meanTDError = currentTDErrors.Average();
            }

            // Time to first contact
            if (firstContactTime > 0)
            {
                currentEpisode.timeToFirstContact = firstContactTime - episodeStartTime;
            }
            else
            {
                currentEpisode.timeToFirstContact = -1f; // Never detected
            }

            // Capture time
            if (success && capturedThisEpisode)
            {
                currentEpisode.captureTime = duration; // Captured at end of episode
            }
            else
            {
                currentEpisode.captureTime = -1f;
            }

            // Add to history
            episodeHistory.Add(currentEpisode);

            // Update rolling stats
            UpdateRollingStats();

            // Auto-export CSV
            if (enableCSVExport && csvExportIntervalEpisodes > 0)
            {
                if (episodeNumber % csvExportIntervalEpisodes == 0)
                {
                    ExportToCSV();
                }
            }

            // Log summary
            if (enableDetailedLogs || episodeNumber % 10 == 0)
            {
                Debug.Log($"[Metrics] Episode {episodeNumber} complete: " +
                    $"Reward={totalReward:F1}, Duration={duration:F1}s, Success={success}, " +
                    $"TD_error={currentEpisode.meanTDError:F3}, States={currentStatesVisited.Count}");
            }
        }

        /// <summary>
        /// Called when action is chosen (from EnemyBrain.StartNewOption)
        /// </summary>
        public void RecordActionChosen(EnemyMode action, int stateKey)
        {
            if (!enableMetricsCollection || currentEpisode == null) return;

            // Track action count
            int actionIndex = (int)action;
            if (actionIndex >= 0 && actionIndex < 8)
            {
                currentEpisode.actionCounts[actionIndex]++;
            }

            // Track state visitation
            currentStatesVisited.Add(stateKey);

            // Track global state visits
            if (!globalStateVisits.ContainsKey(stateKey))
            {
                globalStateVisits[stateKey] = 0;
            }
            globalStateVisits[stateKey]++;
        }

        /// <summary>
        /// Called when Q-update occurs (from QLearningPolicy.UpdateQ)
        /// </summary>
        public void RecordTDError(float tdError)
        {
            if (!enableMetricsCollection || currentEpisode == null) return;

            currentTDErrors.Add(Mathf.Abs(tdError));
        }

        /// <summary>
        /// Called when first contact made (see/hear player)
        /// </summary>
        public void RecordFirstContact()
        {
            if (!enableMetricsCollection || firstContactTime > 0) return;

            firstContactTime = Time.time;

            if (enableDetailedLogs)
            {
                Debug.Log($"[Metrics] First contact at {firstContactTime - episodeStartTime:F1}s");
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Called when player captured (from ChaseExecutor.OnPlayerCaptured)
        /// </summary>
        private void OnPlayerCaptured(float captureTime, Vector2 capturePos)
        {
            if (!enableMetricsCollection) return;

            capturedThisEpisode = true;

            if (enableDetailedLogs)
            {
                Debug.Log($"[Metrics] Player captured at {captureTime:F1}s");
            }
        }
        #endregion

        #region Rolling Statistics
        /// <summary>
        /// Update rolling window statistics (last N episodes)
        /// </summary>
        private void UpdateRollingStats()
        {
            int windowStart = Mathf.Max(0, episodeHistory.Count - rollingWindowSize);
            var windowRecords = episodeHistory.Skip(windowStart).ToList();

            if (windowRecords.Count == 0)
            {
                cachedRollingStats = new RollingStats();
                return;
            }

            // Average reward
            cachedRollingStats.avgReward = windowRecords.Average(r => r.totalReward);

            // Average duration
            cachedRollingStats.avgDuration = windowRecords.Average(r => r.duration);

            // Success rate
            int successCount = windowRecords.Count(r => r.success);
            cachedRollingStats.successRate = (float)successCount / windowRecords.Count;

            // Average time to capture (successful episodes only)
            var successfulRecords = windowRecords.Where(r => r.success && r.captureTime > 0).ToList();
            if (successfulRecords.Count > 0)
            {
                cachedRollingStats.avgTimeToCapture = successfulRecords.Average(r => r.captureTime);
            }
            else
            {
                cachedRollingStats.avgTimeToCapture = -1f;
            }

            // Average TD error
            cachedRollingStats.avgTDError = windowRecords.Average(r => r.meanTDError);

            // Total action counts
            cachedRollingStats.totalActionCounts = new int[8];
            foreach (var record in windowRecords)
            {
                for (int i = 0; i < 8; i++)
                {
                    cachedRollingStats.totalActionCounts[i] += record.actionCounts[i];
                }
            }
        }

        /// <summary>
        /// Get rolling statistics (last N episodes)
        /// </summary>
        public RollingStats GetRollingStats()
        {
            return cachedRollingStats;
        }
        #endregion

        #region CSV Export
        /// <summary>
        /// Export all episode data to CSV
        /// </summary>
        public void ExportToCSV()
        {
            if (episodeHistory.Count == 0)
            {
                Debug.LogWarning("[MetricsCollector] No episode data to export");
                return;
            }

            try
            {
                // Ensure directory exists
                string fullPath = Path.Combine(Application.dataPath, "..", csvOutputPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Build CSV
                StringBuilder csv = new StringBuilder();

                // Header
                csv.AppendLine("episode,timestamp,duration,total_reward,steps,success,captured,time_to_first_contact,epsilon,mean_td_error," +
                    "action_patrol,action_investigate,action_heat_search,action_sweep,action_hide_spot,action_ambush,action_heat_sweep,action_ambush_choke," +
                    "unique_states,capture_time");

                // Data rows
                foreach (var record in episodeHistory)
                {
                    csv.AppendLine(
                        $"{record.episodeNumber}," +
                        $"{record.timestamp}," +
                        $"{record.duration:F2}," +
                        $"{record.totalReward:F2}," +
                        $"{record.steps}," +
                        $"{(record.success ? 1 : 0)}," +
                        $"{(record.captured ? 1 : 0)}," +
                        $"{record.timeToFirstContact:F2}," +
                        $"{record.epsilon:F4}," +
                        $"{record.meanTDError:F4}," +
                        $"{record.actionCounts[0]}," +
                        $"{record.actionCounts[1]}," +
                        $"{record.actionCounts[2]}," +
                        $"{record.actionCounts[3]}," +
                        $"{record.actionCounts[4]}," +
                        $"{record.actionCounts[5]}," +
                        $"{record.actionCounts[6]}," +
                        $"{record.actionCounts[7]}," +
                        $"{record.uniqueStatesVisited}," +
                        $"{record.captureTime:F2}"
                    );
                }

                // Write to file
                File.WriteAllText(fullPath, csv.ToString());

                Debug.Log($"[MetricsCollector] Exported {episodeHistory.Count} episodes to {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MetricsCollector] CSV export failed: {e.Message}");
            }
        }

        /// <summary>
        /// Export state visitation frequency to CSV
        /// </summary>
        public void ExportStateVisitation(int topN = 20)
        {
            if (globalStateVisits.Count == 0)
            {
                Debug.LogWarning("[MetricsCollector] No state visitation data to export");
                return;
            }

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", "TrainingData/state_visitation.csv");
                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                StringBuilder csv = new StringBuilder();
                csv.AppendLine("state_key,visit_count,frequency");

                // Get top N most visited states
                var topStates = globalStateVisits
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(topN)
                    .ToList();

                int totalVisits = globalStateVisits.Values.Sum();

                foreach (var kvp in topStates)
                {
                    float frequency = (float)kvp.Value / totalVisits;
                    csv.AppendLine($"{kvp.Key},{kvp.Value},{frequency:F4}");
                }

                File.WriteAllText(fullPath, csv.ToString());

                Debug.Log($"[MetricsCollector] Exported top {topN} states to {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MetricsCollector] State visitation export failed: {e.Message}");
            }
        }
        #endregion

        #region Manual Triggers
        /// <summary>
        /// Manual CSV export (can be called from Unity editor button)
        /// </summary>
        [ContextMenu("Export CSV Now")]
        public void ManualExportCSV()
        {
            ExportToCSV();
            ExportStateVisitation(20);
        }

        /// <summary>
        /// Print current rolling stats to console
        /// </summary>
        [ContextMenu("Print Rolling Stats")]
        public void PrintRollingStats()
        {
            var stats = GetRollingStats();
            Debug.Log($"=== ROLLING STATS (last {rollingWindowSize} episodes) ===");
            Debug.Log($"Avg Reward: {stats.avgReward:F2}");
            Debug.Log($"Avg Duration: {stats.avgDuration:F2}s");
            Debug.Log($"Success Rate: {stats.successRate:F2} ({stats.successRate * 100:F1}%)");
            Debug.Log($"Avg Time to Capture: {(stats.avgTimeToCapture > 0 ? stats.avgTimeToCapture.ToString("F2") + "s" : "N/A")}");
            Debug.Log($"Avg TD Error: {stats.avgTDError:F4}");

            Debug.Log("Action Distribution:");
            string[] actionNames = { "Patrol", "Investigate", "HeatSearch", "Sweep", "HideSpot", "Ambush", "HeatSweep", "AmbushChoke" };
            int totalActions = stats.totalActionCounts.Sum();
            for (int i = 0; i < 8; i++)
            {
                float pct = totalActions > 0 ? (float)stats.totalActionCounts[i] / totalActions * 100 : 0;
                Debug.Log($"  {actionNames[i]}: {stats.totalActionCounts[i]} ({pct:F1}%)");
            }
        }

        /// <summary>
        /// Reset all metrics (for new training run)
        /// </summary>
        [ContextMenu("Reset All Metrics")]
        public void ResetAllMetrics()
        {
            episodeHistory.Clear();
            globalStateVisits.Clear();
            cachedRollingStats = new RollingStats();
            currentEpisode = null;
            currentTDErrors.Clear();
            currentStatesVisited.Clear();

            Debug.Log("[MetricsCollector] All metrics reset");
        }
        #endregion

        #region Getters
        /// <summary>
        /// Get total episodes recorded
        /// </summary>
        public int GetTotalEpisodes()
        {
            return episodeHistory.Count;
        }

        /// <summary>
        /// Get specific episode record
        /// </summary>
        public EpisodeRecord GetEpisode(int index)
        {
            if (index >= 0 && index < episodeHistory.Count)
            {
                return episodeHistory[index];
            }
            return null;
        }

        /// <summary>
        /// Get all episode records
        /// </summary>
        public List<EpisodeRecord> GetAllEpisodes()
        {
            return new List<EpisodeRecord>(episodeHistory);
        }

        /// <summary>
        /// Get global state visitation counts
        /// </summary>
        public Dictionary<int, int> GetStateVisitation()
        {
            return new Dictionary<int, int>(globalStateVisits);
        }
        #endregion
    }
}
