using UnityEngine;
using System.Collections.Generic;
using AITest.Enemy;
using Metrics; // ✅ NEW: Metrics system

namespace AITest.Learning
{
    /// <summary>
    /// Q-Learning Policy - Tabular SMDP Q-Learning
    /// 
    /// PROMPT 10: Option-based reinforcement learning
    /// - Q-table: Dictionary<stateKey ? action values) (6 actions/options)
    /// - SMDP update: ?^duration (temporal discounting)
    /// - ?-greedy exploration
    /// </summary>
    [System.Serializable]
    public class QLearningPolicy
    {
        [Header("Q-Table")]
        [Tooltip("Q-table storage (stateKey ? action values)")]
        private Dictionary<int, float[]> qTable = new Dictionary<int, float[]>();
        
        [Header("Hyperparameters")]
        [Tooltip("Learning rate (?) - Professor recommendation: 0.1-0.3")]
        [Range(0f, 1f)] public float alpha = 0.3f;  // ✅ Increased for faster learning
        
        [Tooltip("Discount factor (?) - Professor recommendation: 0.90-0.99")]
        [Range(0f, 1f)] public float gamma = 0.95f;  // ? Keep 0.95
        
        [Tooltip("Exploration rate (?) - START LOWER for faster learning")]
        [Range(0f, 1f)] public float epsilon = 0.3f;  // ✅ Start at 30% exploration
        
        [Tooltip("Epsilon decay per episode - SLOWER decay")]
        [Range(0.9f, 1f)] public float epsilonDecay = 0.995f;  // ✅ Slightly faster decay
        
        [Tooltip("Minimum epsilon - Professor recommendation: 0.05")]
        [Range(0f, 0.2f)] public float epsilonMin = 0.05f;  // ? Keep 0.05
        
        [Header("Statistics")]
        public int statesVisited = 0;
        public int updatesPerformed = 0;
        public int episodesCompleted = 0;
        
        // ✅ NEW: TD error tracking for convergence analysis
        public float lastTDError { get; private set; }
        
        // Public properties for debugging
        public int QTableSize => qTable.Count;
        public int TotalUpdates => updatesPerformed;
        
        // Constants
        private const int ActionCount = 7; // 7 active enum values (CutoffAmbush removed)

        /// <summary>
        /// ? PROMPT 10: Choose action (?-greedy)
        /// </summary>
        public EnemyMode ChooseAction(int stateKey, bool explore = true)
        {
            // Ensure Q-values exist for this state
            if (!qTable.ContainsKey(stateKey))
            {
                InitializeState(stateKey);
            }
            
            // ?-greedy exploration
            if (explore && Random.value < epsilon)
            {
                // Random action (exploration)
                int randomAction = Random.Range(0, ActionCount);
                return (EnemyMode)randomAction;
            }
            else
            {
                // Greedy action (exploitation)
                float[] qValues = qTable[stateKey];
                int bestAction = GetBestAction(qValues);
                return (EnemyMode)bestAction;
            }
        }

        /// <summary>
        /// ? PROMPT 10 + 16: Choose action (?-greedy with action masking)
        /// </summary>
        public EnemyMode ChooseAction(int stateKey, bool explore = true, List<EnemyMode> validActions = null)
        {
            // Ensure Q-values exist for this state
            if (!qTable.ContainsKey(stateKey))
            {
                InitializeState(stateKey);
            }
            
            // ? PROMPT 16: If no valid actions provided, allow all (7 total)
            if (validActions == null || validActions.Count == 0)
            {
                validActions = new List<EnemyMode>
                {
                    EnemyMode.Patrol,
                    EnemyMode.InvestigateLastHeard,
                    EnemyMode.HeatSearchPeak,
                    EnemyMode.SweepArea,
                    EnemyMode.HideSpotCheck,
                    EnemyMode.HeatSweep,
                    EnemyMode.AmbushHotChoke
                };
            }
            
            // ?-greedy exploration
            if (explore && Random.value < epsilon)
            {
                // ? Random action from VALID actions only
                int randomIndex = Random.Range(0, validActions.Count);
                return validActions[randomIndex];
            }
            else
            {
                // ? Greedy action from VALID actions only
                float[] qValues = qTable[stateKey];
                
                EnemyMode bestAction = validActions[0];
                float maxQ = qValues[(int)bestAction];
                
                foreach (var action in validActions)
                {
                    float q = qValues[(int)action];
                    if (q > maxQ)
                    {
                        maxQ = q;
                        bestAction = action;
                    }
                }
                
                return bestAction;
            }
        }

        /// <summary>
        /// ? PROMPT 10: SMDP Q-update
        /// Q(s,a) += ? [r + ?^duration * max Q(s',a') - Q(s,a)]
        /// </summary>
        public void UpdateQ(int stateKey, EnemyMode action, float reward, int nextStateKey, float duration)
        {
            // Ensure states exist
            if (!qTable.ContainsKey(stateKey))
                InitializeState(stateKey);
            
            if (!qTable.ContainsKey(nextStateKey))
                InitializeState(nextStateKey);
            
            // Get Q-values
            float[] qValues = qTable[stateKey];
            float[] nextQValues = qTable[nextStateKey];
            
            int actionIndex = (int)action;
            
            // ? SMDP temporal discounting: ?^duration
            float gammaPow = Mathf.Pow(gamma, duration);
            
            // Q-learning update
            float currentQ = qValues[actionIndex];
            float maxNextQ = GetMaxQValue(nextQValues);
            float tdTarget = reward + gammaPow * maxNextQ;
            float tdError = tdTarget - currentQ;
            
            qValues[actionIndex] += alpha * tdError;
            
            float newQ = qValues[actionIndex];
            float delta = newQ - currentQ; // currentQ holds the old value

            // ✅ Track TD error for metrics
            lastTDError = Mathf.Abs(tdError);
            MetricsHooks.TDError(Mathf.Abs(tdError));
            
            // ✅ Log to CSV for analysis
            LogQChange(episodesCompleted, Time.time, stateKey, action, reward, currentQ, newQ, delta);

            // Update table
            qTable[stateKey] = qValues;
            
            updatesPerformed++;
        }

        // --- Logging ---
        private string logFilePath;
        private void LogQChange(int episode, float time, int state, EnemyMode action, float reward, float oldQ, float newQ, float delta)
        {
            try
            {
                if (string.IsNullOrEmpty(logFilePath))
                {
                    string dir = System.IO.Path.Combine(Application.dataPath, "../TrainingData");
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                    logFilePath = System.IO.Path.Combine(dir, "q_learning_history.csv");

                    // Overwrite if it's the very first update of the session, or append? 
                    // Let's append, but if file doesn't exist, write header.
                    if (!System.IO.File.Exists(logFilePath))
                        System.IO.File.WriteAllText(logFilePath, "Episode,Time,StateHash,Action,Reward,OldQ,NewQ,Delta\n");
                }

                string line = $"{episode},{time:F1},{state},{action},{reward:F2},{oldQ:F3},{newQ:F3},{delta:F4}\n";
                System.IO.File.AppendAllText(logFilePath, line);
            }
            catch { /* Ignore IO errors in loop */ }
        }

        /// <summary>
        /// Initialize Q-values for new state
        /// </summary>
        private void InitializeState(int stateKey)
        {
            float[] qValues = new float[ActionCount];
            
            // Initialize with small random values (optimistic initialization)
            for (int i = 0; i < ActionCount; i++)
            {
                qValues[i] = Random.Range(0f, 0.1f);
            }
            
            qTable[stateKey] = qValues;
            statesVisited++;
        }

        /// <summary>
        /// Get best action (argmax Q)
        /// </summary>
        private int GetBestAction(float[] qValues)
        {
            int bestAction = 0;
            float maxQ = qValues[0];
            
            for (int i = 1; i < qValues.Length; i++)
            {
                if (qValues[i] > maxQ)
                {
                    maxQ = qValues[i];
                    bestAction = i;
                }
            }
            
            return bestAction;
        }

        /// <summary>
        /// Get max Q-value
        /// </summary>
        private float GetMaxQValue(float[] qValues)
        {
            float maxQ = qValues[0];
            
            for (int i = 1; i < qValues.Length; i++)
            {
                if (qValues[i] > maxQ)
                {
                    maxQ = qValues[i];
                }
            }
            
            return maxQ;
        }

        /// <summary>
        /// Decay epsilon (after episode)
        /// </summary>
        public void DecayEpsilon()
        {
            epsilon = Mathf.Max(epsilonMin, epsilon * epsilonDecay);
            episodesCompleted++;
        }

        /// <summary>
        /// Get Q-values for state
        /// </summary>
        public float[] GetQValues(int stateKey)
        {
            if (!qTable.ContainsKey(stateKey))
            {
                InitializeState(stateKey);
            }
            
            return qTable[stateKey];
        }

        /// <summary>
        /// Get single Q-value (for logging)
        /// </summary>
        public float GetQValue(int stateKey, int actionIndex)
        {
            if (!qTable.ContainsKey(stateKey))
            {
                InitializeState(stateKey);
            }
            
            if (actionIndex < 0 || actionIndex >= ActionCount)
                return 0f;
                
            return qTable[stateKey][actionIndex];
        }

        /// <summary>
        /// Save Q-table to PlayerPrefs (simple persistence)
        /// </summary>
        public void SaveQTable(string filename = "qtable")
        {
            string json = SerializeQTable();
            PlayerPrefs.SetString(filename, json);
            PlayerPrefs.Save();
            
            Debug.Log($"<color=lime>[QLearning] Q-table saved ({qTable.Count} states)</color>");
        }

        /// <summary>
        /// Load Q-table from PlayerPrefs
        /// </summary>
        public void LoadQTable(string filename = "qtable")
        {
            if (PlayerPrefs.HasKey(filename))
            {
                string json = PlayerPrefs.GetString(filename);
                DeserializeQTable(json);
                
                Debug.Log($"<color=lime>[QLearning] Q-table loaded ({qTable.Count} states)</color>");
            }
            else
            {
                Debug.LogWarning("[QLearning] No saved Q-table found");
            }
        }

        /// <summary>
        /// Serialize Q-table to JSON
        /// </summary>
        private string SerializeQTable()
        {
            QTableData data = new QTableData();
            data.states = new List<int>();
            data.qValues = new List<float[]>();
            
            foreach (var kvp in qTable)
            {
                data.states.Add(kvp.Key);
                data.qValues.Add(kvp.Value);
            }
            
            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// Deserialize Q-table from JSON
        /// </summary>
        private void DeserializeQTable(string json)
        {
            // ? NULL CHECK: Prevent crash on corrupt/empty JSON
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[QLearning] JSON is null or empty! Skipping load.");
                return;
            }
            
            try
            {
                QTableData data = JsonUtility.FromJson<QTableData>(json);
                
                // ? NULL CHECK: Prevent crash on null data
                if (data == null)
                {
                    Debug.LogWarning("[QLearning] Failed to parse JSON! Starting with empty Q-table.");
                    return;
                }
                
                // ? NULL CHECK: Prevent crash on null lists
                if (data.states == null || data.qValues == null)
                {
                    Debug.LogWarning("[QLearning] Q-table data is corrupted! Starting with empty Q-table.");
                    return;
                }
                
                // ? LENGTH CHECK: Ensure matching lengths
                if (data.states.Count != data.qValues.Count)
                {
                    Debug.LogWarning($"[QLearning] State/QValue count mismatch ({data.states.Count} vs {data.qValues.Count})! Skipping load.");
                    return;
                }
                
                qTable.Clear();
                
                for (int i = 0; i < data.states.Count; i++)
                {
                    // ? NULL CHECK: Skip null Q-values
                    if (data.qValues[i] != null)
                    {
                        qTable[data.states[i]] = data.qValues[i];
                    }
                }
                
                Debug.Log($"<color=lime>[QLearning] Successfully loaded {qTable.Count} states from JSON</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QLearning] Exception while loading Q-table: {e.Message}");
                Debug.LogWarning("[QLearning] Starting with empty Q-table.");
            }
        }

        /// <summary>
        /// Get statistics summary
        /// </summary>
        public string GetStatsSummary()
        {
            return $"States: {qTable.Count}/{statesVisited} | Updates: {updatesPerformed} | " +
                   $"Episodes: {episodesCompleted} | ?: {epsilon:F3}";
        }

        /// <summary>
        /// Reset Q-table
        /// </summary>
        public void ResetQTable()
        {
            qTable.Clear();
            statesVisited = 0;
            updatesPerformed = 0;
            epsilon = 0.3f; // Reset epsilon
            
            Debug.Log("<color=yellow>[QLearning] Q-table reset</color>");
        }

        [System.Serializable]
        private class QTableData
        {
            public List<int> states;
            public List<float[]> qValues;
        }
    }
}
