using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;

namespace Metrics
{
    /// <summary>
    /// Benchmark Runner - Fixed scenario evaluation for fair comparison
    /// Runs predefined scenarios with epsilon=0 (pure exploitation)
    /// </summary>
    public class BenchmarkRunner : MonoBehaviour
    {
        [Header("Benchmark Settings")]
        [Tooltip("Number of trials per scenario")]
        public int trialsPerScenario = 10;
        
        [Tooltip("Max episode duration (seconds)")]
        public float maxEpisodeDuration = 120f;
        
        [Tooltip("Snapshot labels (e.g., Ep50, Ep200, Ep500)")]
        public string currentSnapshotLabel = "Ep500";
        
        [Tooltip("Export path for benchmark CSV")]
        public string benchmarkCSVPath = "Metrics/benchmark_results.csv";
        
        [Header("Scenario Definitions")]
        [Tooltip("Enable built-in scenarios")]
        public bool useBuiltInScenarios = true;
        
        private List<BenchmarkScenario> scenarios = new List<BenchmarkScenario>();
        private bool isRunning = false;
        private int currentScenarioIndex = 0;
        private int currentTrial = 0;
        private List<BenchmarkResult> results = new List<BenchmarkResult>();
        
        [System.Serializable]
        public class BenchmarkScenario
        {
            public string scenarioId;
            public int seed;
            public Vector2 playerSpawnPos;
            public Vector2 enemySpawnPos;
            public string description;
            
            public BenchmarkScenario(string id, int seed, Vector2 playerPos, Vector2 enemyPos, string desc)
            {
                scenarioId = id;
                this.seed = seed;
                playerSpawnPos = playerPos;
                enemySpawnPos = enemyPos;
                description = desc;
            }
        }
        
        [System.Serializable]
        public class BenchmarkResult
        {
            public string scenarioId;
            public string snapshotLabel;
            public int trial;
            public bool success;
            public float timeToCapture;
            public float totalReward;
            public int uniqueStates;
            public Dictionary<int, int> actionCounts;
            
            public BenchmarkResult()
            {
                actionCounts = new Dictionary<int, int>();
            }
        }
        
        private void Awake()
        {
            if (useBuiltInScenarios)
            {
                InitializeBuiltInScenarios();
            }
        }
        
        private void InitializeBuiltInScenarios()
        {
            // Scenario 1: Open space - player far from enemy
            scenarios.Add(new BenchmarkScenario(
                "S1_OpenFar",
                12345,
                new Vector2(10f, 10f),
                new Vector2(-10f, -10f),
                "Open space, player far from enemy, tests patrol→search transition"
            ));
            
            // Scenario 2: Corridor - player close
            scenarios.Add(new BenchmarkScenario(
                "S2_CorridorClose",
                23456,
                new Vector2(5f, 0f),
                new Vector2(-5f, 0f),
                "Narrow corridor, player close, tests direct chase effectiveness"
            ));
            
            // Scenario 3: Multi-room - player hidden
            scenarios.Add(new BenchmarkScenario(
                "S3_MultiRoomHidden",
                34567,
                new Vector2(15f, 8f),
                new Vector2(-8f, -8f),
                "Multi-room layout, player hidden, tests search strategies"
            ));
            
            // Scenario 4: Chokepoint - player beyond door
            scenarios.Add(new BenchmarkScenario(
                "S4_Chokepoint",
                45678,
                new Vector2(12f, 5f),
                new Vector2(8f, 5f),
                "Chokepoint scenario, tests ambush/chokepoint actions"
            ));
            
            // Scenario 5: Heat trail - player moving
            scenarios.Add(new BenchmarkScenario(
                "S5_HeatTrail",
                56789,
                new Vector2(0f, 12f),
                new Vector2(0f, -12f),
                "Heat trail scenario, tests heat-based search effectiveness"
            ));
        }
        
        /// <summary>
        /// Start benchmark evaluation
        /// </summary>
        public void RunBenchmark()
        {
            if (isRunning)
            {
                Debug.LogWarning("[BenchmarkRunner] Benchmark already running!");
                return;
            }
            
            if (scenarios.Count == 0)
            {
                Debug.LogError("[BenchmarkRunner] No scenarios defined!");
                return;
            }
            
            Debug.Log($"[BenchmarkRunner] Starting benchmark with {scenarios.Count} scenarios, {trialsPerScenario} trials each");
            isRunning = true;
            currentScenarioIndex = 0;
            currentTrial = 0;
            results.Clear();
            
            StartNextTrial();
        }
        
        private void StartNextTrial()
        {
            if (currentScenarioIndex >= scenarios.Count)
            {
                // All scenarios complete
                FinalizeBenchmark();
                return;
            }
            
            var scenario = scenarios[currentScenarioIndex];
            
            if (currentTrial >= trialsPerScenario)
            {
                // Move to next scenario
                currentScenarioIndex++;
                currentTrial = 0;
                
                if (currentScenarioIndex >= scenarios.Count)
                {
                    FinalizeBenchmark();
                    return;
                }
                
                scenario = scenarios[currentScenarioIndex];
            }
            
            Debug.Log($"[BenchmarkRunner] Running {scenario.scenarioId} - Trial {currentTrial + 1}/{trialsPerScenario}");
            
            // Setup scenario
            SetupScenario(scenario);
            
            // Start episode with epsilon=0 (pure exploitation)
            MetricsHooks.EpisodeStart(scenario.seed, scenario.scenarioId, EpisodeMode.EVAL, 0f);
            
            // Subscribe to episode end
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.OnEpisodeComplete += OnTrialComplete;
            }
            
            // Start episode timer
            Invoke(nameof(ForceEpisodeEnd), maxEpisodeDuration);
        }
        
        private void SetupScenario(BenchmarkScenario scenario)
        {
            // Set random seed
            Random.InitState(scenario.seed);
            
            // TODO: Move player and enemy to spawn positions
            // This requires references to player and enemy game objects
            // For now, log the setup
            Debug.Log($"[BenchmarkRunner] Setup: Player={scenario.playerSpawnPos}, Enemy={scenario.enemySpawnPos}, Seed={scenario.seed}");
            
            // You'll need to implement actual spawn logic based on your scene setup
            // Example:
            // GameObject player = GameObject.FindGameObjectWithTag("Player");
            // if (player) player.transform.position = scenario.playerSpawnPos;
            // 
            // GameObject enemy = GameObject.FindGameObjectWithTag("Enemy");
            // if (enemy) enemy.transform.position = scenario.enemySpawnPos;
        }
        
        private void OnTrialComplete()
        {
            // Unsubscribe
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.OnEpisodeComplete -= OnTrialComplete;
            }
            
            // Cancel force end timer
            CancelInvoke(nameof(ForceEpisodeEnd));
            
            // Record result
            RecordTrialResult();
            
            // Next trial
            currentTrial++;
            
            // Small delay before next trial
            Invoke(nameof(StartNextTrial), 0.5f);
        }
        
        private void ForceEpisodeEnd()
        {
            Debug.LogWarning("[BenchmarkRunner] Episode timeout - forcing end");
            MetricsHooks.EpisodeEnd(TerminationReason.TIMEOUT);
        }
        
        private void RecordTrialResult()
        {
            if (MetricsManager.Instance == null) return;
            
            var currentEpisode = MetricsManager.Instance.GetCurrentEpisode();
            if (currentEpisode == null) return;
            
            var result = new BenchmarkResult
            {
                scenarioId = currentEpisode.scenarioId,
                snapshotLabel = currentSnapshotLabel,
                trial = currentTrial,
                success = currentEpisode.success,
                timeToCapture = currentEpisode.timeToCapture,
                totalReward = currentEpisode.totalReward,
                uniqueStates = currentEpisode.uniqueStatesVisited,
                actionCounts = ConvertActionCountsToDict(currentEpisode.actionCounts)
            };
            
            results.Add(result);
            
            Debug.Log($"[BenchmarkRunner] Trial result: Success={result.success}, Time={result.timeToCapture:F2}s, Reward={result.totalReward:F2}");
        }
        
        private void FinalizeBenchmark()
        {
            isRunning = false;
            
            Debug.Log($"[BenchmarkRunner] Benchmark complete! Total trials: {results.Count}");
            
            // Export results
            ExportBenchmarkCSV();
            
            // Print summary
            PrintBenchmarkSummary();
        }
        
        private void ExportBenchmarkCSV()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", benchmarkCSVPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            
            using (StreamWriter writer = new StreamWriter(fullPath, false))
            {
                // Header
                writer.WriteLine("scenarioId,snapshotLabel,trial,success,timeToCapture,totalReward,uniqueStates,action0,action1,action2,action3,action4,action5,action6,action7");
                
                // Data rows
                foreach (var result in results)
                {
                    writer.Write($"{result.scenarioId},");
                    writer.Write($"{result.snapshotLabel},");
                    writer.Write($"{result.trial},");
                    writer.Write($"{(result.success ? 1 : 0)},");
                    writer.Write($"{result.timeToCapture.ToString("F3", CultureInfo.InvariantCulture)},");
                    writer.Write($"{result.totalReward.ToString("F3", CultureInfo.InvariantCulture)},");
                    writer.Write($"{result.uniqueStates},");
                    
                    // Action counts (0-7)
                    for (int i = 0; i < 8; i++)
                    {
                        int count = result.actionCounts.ContainsKey(i) ? result.actionCounts[i] : 0;
                        writer.Write(count);
                        if (i < 7) writer.Write(",");
                    }
                    
                    writer.WriteLine();
                }
            }
            
            Debug.Log($"[BenchmarkRunner] Exported benchmark results to {fullPath}");
        }
        
        private void PrintBenchmarkSummary()
        {
            // Group by scenario
            var scenarioGroups = results.GroupBy(r => r.scenarioId);
            
            Debug.Log("=== BENCHMARK SUMMARY ===");
            Debug.Log($"Snapshot: {currentSnapshotLabel}");
            Debug.Log($"Trials per scenario: {trialsPerScenario}");
            Debug.Log("");
            
            foreach (var group in scenarioGroups)
            {
                var scenarioResults = group.ToList();
                int successCount = scenarioResults.Count(r => r.success);
                float successRate = (float)successCount / scenarioResults.Count;
                
                var successfulTrials = scenarioResults.Where(r => r.success).ToList();
                float avgTime = successfulTrials.Count > 0 ? successfulTrials.Average(r => r.timeToCapture) : 0f;
                float stdTime = successfulTrials.Count > 1 ? CalculateStdDev(successfulTrials.Select(r => r.timeToCapture).ToList()) : 0f;
                
                float avgReward = scenarioResults.Average(r => r.totalReward);
                float stdReward = scenarioResults.Count > 1 ? CalculateStdDev(scenarioResults.Select(r => r.totalReward).ToList()) : 0f;
                
                Debug.Log($"{group.Key}:");
                Debug.Log($"  Success Rate: {successRate:P1} ({successCount}/{scenarioResults.Count})");
                Debug.Log($"  Time to Capture: {avgTime:F2} ± {stdTime:F2}s (successful only)");
                Debug.Log($"  Total Reward: {avgReward:F2} ± {stdReward:F2}");
                Debug.Log("");
            }
        }
        
        private float CalculateStdDev(List<float> values)
        {
            if (values.Count < 2) return 0f;
            
            float avg = values.Average();
            float sumSquaredDiff = values.Sum(v => (v - avg) * (v - avg));
            return Mathf.Sqrt(sumSquaredDiff / (values.Count - 1));
        }
        
        /// <summary>
        /// Add custom scenario
        /// </summary>
        public void AddScenario(string id, int seed, Vector2 playerPos, Vector2 enemyPos, string description)
        {
            scenarios.Add(new BenchmarkScenario(id, seed, playerPos, enemyPos, description));
        }
        
        /// <summary>
        /// Clear all scenarios
        /// </summary>
        public void ClearScenarios()
        {
            scenarios.Clear();
        }
        
        /// <summary>
        /// Convert action counts array to dictionary
        /// </summary>
        private Dictionary<int, int> ConvertActionCountsToDict(int[] actionCounts)
        {
            Dictionary<int, int> dict = new Dictionary<int, int>();
            for (int i = 0; i < actionCounts.Length; i++)
            {
                if (actionCounts[i] > 0)
                {
                    dict[i] = actionCounts[i];
                }
            }
            return dict;
        }
        
        /// <summary>
        /// Get all benchmark results
        /// </summary>
        public List<BenchmarkResult> GetResults()
        {
            return results;
        }
    }
}
