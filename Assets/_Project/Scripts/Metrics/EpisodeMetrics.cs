using System;
using System.Text;

namespace Metrics
{
    /// <summary>
    /// Episode Metrics - Data model for single episode tracking
    /// Serializable for CSV export
    /// </summary>
    [Serializable]
    public class EpisodeMetrics
    {
        // Episode identification
        public int episodeId;
        public int seed;
        public string scenarioId;
        public EpisodeMode mode; // TRAIN or EVAL
        
        // Timing
        public float startTime;
        public float endTime;
        public float duration;
        
        // Termination
        public TerminationReason terminationReason;
        public bool success; // 1 if enemy captured player, else 0
        public float timeToCapture; // seconds; -1 if none
        
        // Rewards and learning
        public float totalReward;
        public float epsilonUsed;
        
        // Actions
        public int[] actionCounts; // [NumActions]
        public float[] actionTimeSpent; // [NumActions] optional
        
        // States
        public int uniqueStatesVisited;
        
        // Contact
        public float firstContactTime; // -1 if none
        
        // Convergence
        public float tdErrorMean; // -1 if not tracked
        
        // Configuration flags
        public bool useLearning;
        public bool useHeat;
        
        public EpisodeMetrics(int numActions)
        {
            actionCounts = new int[numActions];
            actionTimeSpent = new float[numActions];
            timeToCapture = -1f;
            firstContactTime = -1f;
            tdErrorMean = -1f;
            success = false;
            scenarioId = "default";
            mode = EpisodeMode.TRAIN;
        }
        
        /// <summary>
        /// Get CSV header (matches ToCSVRow order)
        /// </summary>
        public static string GetCSVHeader(int numActions)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("episodeId,seed,scenarioId,mode,");
            sb.Append("startTime,endTime,duration,");
            sb.Append("terminationReason,success,timeToCapture,");
            sb.Append("totalReward,epsilonUsed,");
            
            // Action counts
            for (int i = 0; i < numActions; i++)
            {
                sb.Append($"action{i}_count,");
            }
            
            // Action time spent
            for (int i = 0; i < numActions; i++)
            {
                sb.Append($"action{i}_time,");
            }
            
            sb.Append("uniqueStatesVisited,firstContactTime,tdErrorMean,");
            sb.Append("useLearning,useHeat");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Convert to CSV row (matches header order)
        /// </summary>
        public string ToCSVRow()
        {
            StringBuilder sb = new StringBuilder();
            
            // Use invariant culture to avoid locale decimal separators
            sb.Append($"{episodeId},");
            sb.Append($"{seed},");
            sb.Append($"{scenarioId},");
            sb.Append($"{mode},");
            sb.Append($"{startTime.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{endTime.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{duration.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{terminationReason},");
            sb.Append($"{(success ? 1 : 0)},");
            sb.Append($"{timeToCapture.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{totalReward.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{epsilonUsed.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            
            // Action counts
            for (int i = 0; i < actionCounts.Length; i++)
            {
                sb.Append($"{actionCounts[i]},");
            }
            
            // Action time spent
            for (int i = 0; i < actionTimeSpent.Length; i++)
            {
                sb.Append($"{actionTimeSpent[i].ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            }
            
            sb.Append($"{uniqueStatesVisited},");
            sb.Append($"{firstContactTime.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{tdErrorMean.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"{(useLearning ? 1 : 0)},");
            sb.Append($"{(useHeat ? 1 : 0)}");
            
            return sb.ToString();
        }
    }
    
    public enum EpisodeMode
    {
        TRAIN,
        EVAL
    }
    
    public enum TerminationReason
    {
        CAPTURE,
        TIMEOUT,
        QUEST_COMPLETE,
        ABORT
    }
}
