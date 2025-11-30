using UnityEngine;
using UnityEngine.UI;
using AITest.Enemy;
using AITest.Learning;

namespace AITest.UI
{
    /// <summary>
    /// Canlý metrikler ekran HUD + Player Behavior Tracking
    /// </summary>
    public class LearningHUD : MonoBehaviour
    {
        [Header("References")]
        public EnemyBrain enemyBrain;
        public QLearner qLearner;
        
        [Header("UI Elements")]
        public Text statusText;
        
        [Header("Update")]
        public float updateInterval = 0.5f;
        
        [Header("Player Stats Display")]
        public bool showPlayerStats = true; // ? YENÝ!
        
        private float nextUpdateTime;
        private float wrongSectorPerMinute;
        private float backtrackPercent;
        
        private int lastWrongSectorCount;
        private int lastBacktrackCount;
        private float trackingStartTime;
        
        private void Start()
        {
            // Auto-find
            if (!enemyBrain)
            {
                var enemy = GameObject.FindGameObjectWithTag("Enemy");
                if (enemy) enemyBrain = enemy.GetComponent<EnemyBrain>();
            }
            
            if (!qLearner && enemyBrain)
                qLearner = enemyBrain.GetComponent<QLearner>();
            
            trackingStartTime = Time.time;
        }
        
        private void Update()
        {
            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + updateInterval;
                UpdateMetrics();
                UpdateUI();
            }
        }
        
        private void UpdateMetrics()
        {
            if (!enemyBrain) return;
            
            float elapsed = Time.time - trackingStartTime;
            if (elapsed < 1f) return;
            
            // ? OLD METRICS (removed from EnemyBrain)
            // Wrong sector/min
            // int wrongDelta = enemyBrain.WrongSectorCount - lastWrongSectorCount;
            // wrongSectorPerMinute = (wrongDelta / elapsed) * 60f;
            // lastWrongSectorCount = enemyBrain.WrongSectorCount;
            
            // Backtrack %
            // int backtrackDelta = enemyBrain.BacktrackCount - lastBacktrackCount;
            // int totalDecisions = Mathf.Max(1, wrongDelta + backtrackDelta);
            // backtrackPercent = (backtrackDelta / (float)totalDecisions) * 100f;
            // lastBacktrackCount = enemyBrain.BacktrackCount;
            
            wrongSectorPerMinute = 0f;
            backtrackPercent = 0f;
            
            // Reset tracking window
            trackingStartTime = Time.time;
        }
        
        private void UpdateUI()
        {
            if (!statusText) return;
            
            string text = "";
            
            // ? YENÝ: PLAYER STATS SECTION (TOP)
            if (showPlayerStats)
            {
                text += "<color=yellow>??? PLAYER ???</color>\n";
                
                var playerMonitor = AITest.Player.PlayerStatsCache.GetMonitor();
                if (playerMonitor != null)
                {
                    int bucket = playerMonitor.PlayerStyleBucket;
                    string bucketName = GetPlayerBucketName(bucket);
                    Color bucketColor = GetPlayerBucketColor(bucket);
                    string colorHex = ColorUtility.ToHtmlStringRGB(bucketColor);
                    
                    text += $"<b>Style:</b> <color=#{colorHex}>{bucketName}</color>\n";
                    text += $"<b>Speed:</b> {playerMonitor.CurrentSpeed:F1} m/s\n";
                    text += $"<b>Noise:</b> {playerMonitor.CurrentNoiseCount}/5s\n";
                    text += $"<b>Light:</b> {playerMonitor.CurrentLightExposure:P0}\n";
                }
                else
                {
                    text += "<color=red>Monitor: NOT FOUND</color>\n";
                }
                
                text += "\n";
            }
            
            // Enemy Learning Section
            text += "<color=cyan>?? ENEMY ??</color>\n";
            
            // Learning status
            text += $"<b>Learning:</b> {(enemyBrain && enemyBrain.learningEnabled ? "<color=lime>ON</color>" : "<color=red>OFF</color>")}\n";
            
            // ? NEW: Behavior State
            if (enemyBrain)
            {
                text += $"<b>Behavior:</b> <color=cyan>{enemyBrain.currentBehavior}</color>\n";
                text += $"<b>Decisions:</b> {enemyBrain.TotalDecisions}\n";
                text += $"<b>Captures:</b> {enemyBrain.SuccessfulCaptures}\n";
            }
            
            // ? Threat Assessment (Perceptron)
            if (enemyBrain && enemyBrain.threatPerceptron && enemyBrain.useThreatAssessment)
            {
                float threat = enemyBrain.threatPerceptron.LastThreatScore;
                string category = enemyBrain.threatPerceptron.GetThreatCategory();
                
                Color color = threat >= 0.8f ? new Color(1f, 0f, 0f) :
                              threat >= 0.6f ? new Color(1f, 0.5f, 0f) :
                              threat >= 0.3f ? new Color(1f, 1f, 0f) :
                              new Color(0f, 1f, 0f);
                
                string colorHex = ColorUtility.ToHtmlStringRGB(color);
                text += $"<b>Threat:</b> <color=#{colorHex}>{threat:F2} ({category})</color> (T key)\n";
                
                int trainCount = enemyBrain.threatPerceptron.TrainingCount;
                text += $"<b>NN Training:</b> {trainCount} samples\n";
            }
            
            // Capture time
            if (enemyBrain)
                text += $"<b>CaptureTime:</b> {enemyBrain.CaptureTime:F1}s\n";
            
            // ? REMOVED: WrongSector/Backtrack metrics
            // text += $"<b>WrongSector/min:</b> {wrongSectorPerMinute:F2}\n";
            // text += $"<b>Backtrack%:</b> {backtrackPercent:F1}%\n";
            
            // Q-Learning params
            if (qLearner)
            {
                text += $"<b>?:</b> {qLearner.CurrentEpsilon:F3}\n";
                text += $"<b>?Q(last):</b> {qLearner.LastDeltaQ:F3}\n";
                text += $"<b>TD-Error:</b> {qLearner.LastTDError:F3}\n";
                text += $"<b>Q-States:</b> {qLearner.GetQTableSize()}\n"; // ? YENÝ!
            }
            
            // Sectors
            if (enemyBrain && enemyBrain.perception)
            {
                var perception = enemyBrain.perception;
                text += $"<b>EnemySector:</b> {enemyBrain.sectorizer?.GetIdByPosition(enemyBrain.transform.position) ?? "?"}\n";
                text += $"<b>LastSeen:</b> {perception.LastSeenSectorId}\n";
                text += $"<b>LastHeard:</b> {perception.LastHeardSectorId}\n";
            }
            
            // ? YENÝ: Hiding Spot Probabilities (current sector)
            if (enemyBrain && enemyBrain.sectorizer)
            {
                var currentSector = enemyBrain.sectorizer.GetByPosition(enemyBrain.transform.position);
                if (currentSector != null && currentSector.hidingStats != null && currentSector.hidingStats.Length > 0)
                {
                    text += $"<b>Hiding:</b> ";
                    for (int i = 0; i < currentSector.hidingStats.Length; i++)
                    {
                        float prob = currentSector.hidingStats[i].GetProbability();
                        
                        // Color based on probability
                        Color probColor = prob >= 0.7f ? new Color(1f, 0f, 0f) :
                                          prob >= 0.5f ? new Color(1f, 0.5f, 0f) :
                                          new Color(0f, 1f, 0f);
                        
                        string probHex = ColorUtility.ToHtmlStringRGB(probColor);
                        text += $"<color=#{probHex}>S{i}:{prob:P0}</color> ";
                    }
                    text += "\n";
                }
            }
            
            statusText.text = text;
        }
        
        /// <summary>
        /// ? YENÝ: Player bucket name helper
        /// </summary>
        private string GetPlayerBucketName(int bucket)
        {
            return bucket switch
            {
                0 => "Aggressive",
                1 => "Silent",
                2 => "Light-Seeker",
                3 => "Hider",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        /// ? YENÝ: Player bucket color helper
        /// </summary>
        private Color GetPlayerBucketColor(int bucket)
        {
            return bucket switch
            {
                0 => new Color(1f, 0f, 0f),      // Red (Aggressive)
                1 => new Color(0f, 1f, 0f),      // Green (Silent)
                2 => new Color(1f, 1f, 0f),      // Yellow (Light-Seeker)
                3 => new Color(0.5f, 0.5f, 1f),  // Blue (Hider)
                _ => Color.white
            };
        }
    }
}
