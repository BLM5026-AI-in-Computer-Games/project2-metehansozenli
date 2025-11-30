using UnityEngine;
using System.IO;
using System.Text;
using AITest.Enemy;
using AITest.Learning;

namespace AITest.Utils
{
    /// <summary>
    /// CSV/JSON logger - Her karar için log satýrý
    /// </summary>
    public class Logger : MonoBehaviour
    {
        public enum LogFormat { CSV, JSON }
        
        [Header("Settings")]
        [Tooltip("Log formatý")]
        public LogFormat format = LogFormat.CSV;
        
        [Tooltip("Log dosya adý")]
        public string fileName = "ai_test_log";
        
        [Header("References")]
        public EnemyBrain enemyBrain;
        public QLearner qLearner;
        
        [Header("Debug")]
        public bool enableLogging = true;
        
        private StreamWriter writer;
        private string filePath;
        private StringBuilder logBuffer = new StringBuilder();
        
        private void Start()
        {
            if (!enableLogging) return;
            
            // Auto-find
            if (!enemyBrain)
            {
                var enemy = GameObject.FindGameObjectWithTag("Enemy");
                if (enemy) enemyBrain = enemy.GetComponent<EnemyBrain>();
            }
            
            if (!qLearner && enemyBrain)
                qLearner = enemyBrain.GetComponent<QLearner>();
            
            // File setup
            string ext = format == LogFormat.CSV ? "csv" : "json";
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = Path.Combine(Application.persistentDataPath, $"{fileName}_{timestamp}.{ext}");
            
            writer = new StreamWriter(filePath, false);
            
            // CSV header
            if (format == LogFormat.CSV)
            {
                writer.WriteLine("time,state_key,action,reward,Q_before,Q_after,TD_error,enemySector,lastClue,clueType,timeBucket");
            }
            else
            {
                writer.WriteLine("["); // JSON array start
            }
            
            Debug.Log($"<color=lime>[Logger] Logging started: {filePath}</color>");
        }
        
        private void OnDestroy()
        {
            if (writer != null)
            {
                if (format == LogFormat.JSON)
                {
                    writer.WriteLine("]"); // JSON array end
                }
                
                writer.Close();
                Debug.Log($"<color=lime>[Logger] Log saved: {filePath}</color>");
            }
        }
        
        /// <summary>
        /// Karar log'u ekle (EnemyBrain'den çaðrýlacak)
        /// </summary>
        public void LogDecision(RLState state, RLAction action, float reward, float qBefore, float qAfter, float tdError)
        {
            if (!enableLogging || writer == null) return;
            
            if (format == LogFormat.CSV)
            {
                LogCSV(state, action, reward, qBefore, qAfter, tdError);
            }
            else
            {
                LogJSON(state, action, reward, qBefore, qAfter, tdError);
            }
        }
        
        private void LogCSV(RLState state, RLAction action, float reward, float qBefore, float qAfter, float tdError)
        {
            logBuffer.Clear();
            
            // Determine last clue sector (prefer seen over heard)
            string lastClue = state.lastSeenSectorId != "None" ? state.lastSeenSectorId : state.lastHeardSectorId;
            bool isSeenClue = state.lastSeenSectorId != "None";
            
            logBuffer.Append($"{Time.time:F3},");
            logBuffer.Append($"{state.ToKey()},");
            logBuffer.Append($"{action},");
            logBuffer.Append($"{reward:F4},");
            logBuffer.Append($"{qBefore:F4},");
            logBuffer.Append($"{qAfter:F4},");
            logBuffer.Append($"{tdError:F4},");
            logBuffer.Append($"{state.enemySectorId},");
            logBuffer.Append($"{lastClue},");
            logBuffer.Append($"{(isSeenClue ? "S" : "H")},");
            logBuffer.Append($"{state.timeSinceContactBucket}");
            
            writer.WriteLine(logBuffer.ToString());
            writer.Flush();
        }
        
        private void LogJSON(RLState state, RLAction action, float reward, float qBefore, float qAfter, float tdError)
        {
            logBuffer.Clear();
            
            // Determine last clue sector (prefer seen over heard)
            string lastClue = state.lastSeenSectorId != "None" ? state.lastSeenSectorId : state.lastHeardSectorId;
            bool isSeenClue = state.lastSeenSectorId != "None";
            
            logBuffer.Append("  {");
            logBuffer.Append($"\"time\":{Time.time:F3},");
            logBuffer.Append($"\"state\":\"{state.ToKey()}\",");
            logBuffer.Append($"\"action\":\"{action}\",");
            logBuffer.Append($"\"reward\":{reward:F4},");
            logBuffer.Append($"\"Q_before\":{qBefore:F4},");
            logBuffer.Append($"\"Q_after\":{qAfter:F4},");
            logBuffer.Append($"\"TD_error\":{tdError:F4},");
            logBuffer.Append($"\"enemySector\":\"{state.enemySectorId}\",");
            logBuffer.Append($"\"lastClue\":\"{lastClue}\",");
            logBuffer.Append($"\"clueType\":\"{(isSeenClue ? "Seen" : "Heard")}\",");
            logBuffer.Append($"\"timeBucket\":{state.timeSinceContactBucket}");
            logBuffer.Append("},");
            
            writer.WriteLine(logBuffer.ToString());
            writer.Flush();
        }
    }
}
