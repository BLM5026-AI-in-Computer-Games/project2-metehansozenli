using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metrics
{
    /// <summary>
    /// Metrics Manager - Singleton for episode tracking and CSV export
    /// API for recording episode data during training and evaluation
    /// </summary>
    public class MetricsManager : MonoBehaviour
    {
        #region Singleton
        public static MetricsManager Instance { get; private set; }
        #endregion
        
        #region Configuration
        [Header("Configuration")]
        [Tooltip("Number of actions in the MDP")]
        public int numActions = 8;
        
        [Tooltip("Auto-export CSV every N episodes (0 = manual only)")]
        public int autoExportInterval = 50;
        
        [Tooltip("Rolling window size for statistics")]
        public int rollingWindowSize = 100;
        
        [Tooltip("CSV output path (relative to project)")]
        public string csvPath = "TrainingData/metrics.csv";
        
        [Tooltip("Benchmark CSV path")]
        public string benchmarkCsvPath = "TrainingData/benchmarks.csv";
        
        [Header("Flags")]
        [Tooltip("Use learning (Q-learning ON)")]
        public bool useLearning = true;
        
        [Tooltip("Use heat system")]
        public bool useHeat = true;
        
        [Header("Debug")]
        public bool enableDebugLogs = false;
        #endregion
        
        #region Events
        public event System.Action OnEpisodeComplete;
        #endregion
        
        #region State
        private EpisodeMetrics currentEpisode;
        private List<EpisodeMetrics> episodeHistory = new List<EpisodeMetrics>();
        private HashSet<int> currentStatesVisited = new HashSet<int>();
        private List<float> currentTDErrors = new List<float>();
        private float currentActionStartTime = 0f;
        private int lastActionId = -1;
        private bool episodeRunning = false;
        private bool firstContactRecorded = false;
        #endregion
        
        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[MetricsManager] Initialized");
        }
        #endregion
        
        #region Episode Lifecycle
        /// <summary>
        /// Begin new episode
        /// </summary>
        public void BeginEpisode(int seed, string scenarioId, EpisodeMode mode, float epsilon)
        {
            if (episodeRunning)
            {
                Debug.LogWarning("[MetricsManager] Episode already running - ending previous episode");
                EndEpisode(TerminationReason.ABORT);
            }
            
            currentEpisode = new EpisodeMetrics(numActions);
            currentEpisode.episodeId = episodeHistory.Count + 1;
            currentEpisode.seed = seed;
            currentEpisode.scenarioId = scenarioId;
            currentEpisode.mode = mode;
            currentEpisode.startTime = Time.time;
            currentEpisode.epsilonUsed = epsilon;
            currentEpisode.useLearning = useLearning;
            currentEpisode.useHeat = useHeat;
            
            currentStatesVisited.Clear();
            currentTDErrors.Clear();
            episodeRunning = true;
            firstContactRecorded = false;
            lastActionId = -1;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MetricsManager] Episode {currentEpisode.episodeId} started | Mode={mode} | ε={epsilon:F3}");
            }
        }
        
        /// <summary>
        /// End current episode
        /// </summary>
        public void EndEpisode(TerminationReason reason)
        {
            if (!episodeRunning || currentEpisode == null)
            {
                Debug.LogWarning("[MetricsManager] No episode running to end");
                return;
            }
            
            currentEpisode.endTime = Time.time;
            currentEpisode.duration = currentEpisode.endTime - currentEpisode.startTime;
            currentEpisode.terminationReason = reason;
            currentEpisode.uniqueStatesVisited = currentStatesVisited.Count;
            
            // Calculate mean TD error
            if (currentTDErrors.Count > 0)
            {
                currentEpisode.tdErrorMean = currentTDErrors.Average();
            }
            
            // Add to history
            episodeHistory.Add(currentEpisode);
            episodeRunning = false;
            
            // Auto-export
            if (autoExportInterval > 0 && currentEpisode.episodeId % autoExportInterval == 0)
            {
                ExportCSV(csvPath);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MetricsManager] Episode {currentEpisode.episodeId} ended | " +
                    $"Reason={reason} | Success={currentEpisode.success} | " +
                    $"Reward={currentEpisode.totalReward:F1} | Duration={currentEpisode.duration:F1}s");
            }
            
            // Invoke event
            OnEpisodeComplete?.Invoke();
        }
        #endregion
        
        #region Recording Methods
        /// <summary>
        /// Record action selection
        /// </summary>
        public void RecordAction(int actionId, string actionName = null)
        {
            if (!episodeRunning || currentEpisode == null) return;
            
            // Track time spent on previous action
            if (lastActionId >= 0 && lastActionId < numActions)
            {
                float timeSpent = Time.time - currentActionStartTime;
                currentEpisode.actionTimeSpent[lastActionId] += timeSpent;
            }
            
            // Record new action
            if (actionId >= 0 && actionId < numActions)
            {
                currentEpisode.actionCounts[actionId]++;
                lastActionId = actionId;
                currentActionStartTime = Time.time;
            }
        }
        
        /// <summary>
        /// Record reward increment
        /// </summary>
        public void RecordReward(float deltaReward)
        {
            if (!episodeRunning || currentEpisode == null) return;
            
            currentEpisode.totalReward += deltaReward;
        }
        
        /// <summary>
        /// Record state visitation
        /// </summary>
        public void RecordState(int stateId)
        {
            if (!episodeRunning || currentEpisode == null) return;
            
            currentStatesVisited.Add(stateId);
        }
        
        /// <summary>
        /// Record TD error
        /// </summary>
        public void RecordTDError(float tdError)
        {
            if (!episodeRunning || currentEpisode == null) return;
            
            currentTDErrors.Add(Mathf.Abs(tdError));
        }
        
        /// <summary>
        /// Record first contact (see/hear player)
        /// </summary>
        public void RecordFirstContact(string contactType, float time)
        {
            if (!episodeRunning || currentEpisode == null || firstContactRecorded) return;
            
            currentEpisode.firstContactTime = time - currentEpisode.startTime;
            firstContactRecorded = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MetricsManager] First contact ({contactType}) at {currentEpisode.firstContactTime:F1}s");
            }
        }
        
        /// <summary>
        /// Record capture event
        /// </summary>
        public void RecordCapture(float time)
        {
            if (!episodeRunning || currentEpisode == null) return;
            
            currentEpisode.success = true;
            currentEpisode.timeToCapture = time - currentEpisode.startTime;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MetricsManager] Capture at {currentEpisode.timeToCapture:F1}s");
            }
        }
        #endregion
        
        #region CSV Export
        /// <summary>
        /// Export all episodes to CSV
        /// </summary>
        public void ExportCSV(string path)
        {
            if (episodeHistory.Count == 0)
            {
                Debug.LogWarning("[MetricsManager] No episodes to export");
                return;
            }
            
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                string directory = Path.GetDirectoryName(fullPath);
                
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                using (StreamWriter writer = new StreamWriter(fullPath, false))
                {
                    // Header
                    writer.WriteLine(EpisodeMetrics.GetCSVHeader(numActions));
                    
                    // Data rows
                    foreach (var episode in episodeHistory)
                    {
                        writer.WriteLine(episode.ToCSVRow());
                    }
                }
                
                Debug.Log($"[MetricsManager] Exported {episodeHistory.Count} episodes to {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MetricsManager] CSV export failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Append current episode to CSV (for incremental export)
        /// </summary>
        public void AppendToCSV(string path)
        {
            if (currentEpisode == null) return;
            
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                string directory = Path.GetDirectoryName(fullPath);
                
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                bool fileExists = File.Exists(fullPath);
                
                using (StreamWriter writer = new StreamWriter(fullPath, true))
                {
                    // Write header if file is new
                    if (!fileExists)
                    {
                        writer.WriteLine(EpisodeMetrics.GetCSVHeader(numActions));
                    }
                    
                    // Append episode
                    writer.WriteLine(currentEpisode.ToCSVRow());
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MetricsManager] CSV append failed: {e.Message}");
            }
        }
        #endregion
        
        #region Rolling Statistics
        /// <summary>
        /// Get rolling success rate (last N episodes)
        /// </summary>
        public float GetRollingSuccessRate()
        {
            int startIdx = Mathf.Max(0, episodeHistory.Count - rollingWindowSize);
            var window = episodeHistory.Skip(startIdx).ToList();
            
            if (window.Count == 0) return 0f;
            
            return (float)window.Count(e => e.success) / window.Count;
        }
        
        /// <summary>
        /// Get rolling average time to capture (successful episodes only)
        /// </summary>
        public float GetRollingAvgTimeToCapture()
        {
            int startIdx = Mathf.Max(0, episodeHistory.Count - rollingWindowSize);
            var window = episodeHistory.Skip(startIdx).Where(e => e.success && e.timeToCapture > 0).ToList();
            
            if (window.Count == 0) return -1f;
            
            return window.Average(e => e.timeToCapture);
        }
        
        /// <summary>
        /// Get rolling average reward
        /// </summary>
        public float GetRollingAvgReward()
        {
            int startIdx = Mathf.Max(0, episodeHistory.Count - rollingWindowSize);
            var window = episodeHistory.Skip(startIdx).ToList();
            
            if (window.Count == 0) return 0f;
            
            return window.Average(e => e.totalReward);
        }
        
        /// <summary>
        /// Get rolling action counts (last N episodes)
        /// </summary>
        public int[] GetRollingActionCounts()
        {
            int[] counts = new int[numActions];
            
            int startIdx = Mathf.Max(0, episodeHistory.Count - rollingWindowSize);
            var window = episodeHistory.Skip(startIdx).ToList();
            
            foreach (var episode in window)
            {
                for (int i = 0; i < numActions && i < episode.actionCounts.Length; i++)
                {
                    counts[i] += episode.actionCounts[i];
                }
            }
            
            return counts;
        }
        #endregion
        
        #region Getters
        public int GetTotalEpisodes() => episodeHistory.Count;
        public EpisodeMetrics GetCurrentEpisode() => currentEpisode;
        public List<EpisodeMetrics> GetAllEpisodes() => new List<EpisodeMetrics>(episodeHistory);
        public bool IsEpisodeRunning() => episodeRunning;
        #endregion
        
        #region Context Menu Commands
        [ContextMenu("Export CSV Now")]
        public void ManualExportCSV()
        {
            ExportCSV(csvPath);
        }
        
        [ContextMenu("Print Rolling Stats")]
        public void PrintRollingStats()
        {
            Debug.Log("=== ROLLING STATISTICS ===");
            Debug.Log($"Success Rate: {GetRollingSuccessRate() * 100:F1}%");
            Debug.Log($"Avg Time to Capture: {GetRollingAvgTimeToCapture():F1}s");
            Debug.Log($"Avg Reward: {GetRollingAvgReward():F1}");
            
            int[] actionCounts = GetRollingActionCounts();
            int totalActions = actionCounts.Sum();
            Debug.Log($"Action Distribution (total={totalActions}):");
            for (int i = 0; i < actionCounts.Length; i++)
            {
                float pct = totalActions > 0 ? (float)actionCounts[i] / totalActions * 100 : 0;
                Debug.Log($"  Action {i}: {actionCounts[i]} ({pct:F1}%)");
            }
        }
        
        [ContextMenu("Reset All Data")]
        public void ResetAllData()
        {
            episodeHistory.Clear();
            currentEpisode = null;
            episodeRunning = false;
            Debug.Log("[MetricsManager] All data reset");
        }
        #endregion
    }
}
