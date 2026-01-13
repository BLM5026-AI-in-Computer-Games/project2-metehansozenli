using UnityEngine;

namespace Metrics
{
    /// <summary>
    /// Metrics Hooks - Static API for lightweight integration
    /// Fails gracefully if MetricsManager is not present in scene
    /// </summary>
    public static class MetricsHooks
    {
        /// <summary>
        /// Record action selection
        /// </summary>
        public static void ActionSelected(int actionId, string actionName = null)
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.RecordAction(actionId, actionName);
            }
        }
        
        /// <summary>
        /// Add reward increment
        /// </summary>
        public static void AddReward(float deltaReward)
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.RecordReward(deltaReward);
            }
        }
        
        /// <summary>
        /// Record state visitation
        /// </summary>
        public static void StateVisited(int stateId)
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.RecordState(stateId);
            }
        }
        
        /// <summary>
        /// Record capture event
        /// </summary>
        public static void Capture()
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.RecordCapture(Time.time);
            }
        }
        
        /// <summary>
        /// Record first contact (see/hear player)
        /// </summary>
        public static void FirstContact(string contactType = "unknown")
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.RecordFirstContact(contactType, Time.time);
            }
        }
        
        /// <summary>
        /// Begin episode
        /// </summary>
        public static void EpisodeStart(int seed, string scenarioId, EpisodeMode mode, float epsilon)
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.BeginEpisode(seed, scenarioId, mode, epsilon);
            }
        }
        
        /// <summary>
        /// End episode
        /// </summary>
        public static void EpisodeEnd(TerminationReason reason)
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.EndEpisode(reason);
            }
        }
        
        /// <summary>
        /// Record TD error
        /// </summary>
        public static void TDError(float tdError)
        {
            if (MetricsManager.Instance != null)
            {
                MetricsManager.Instance.RecordTDError(tdError);
            }
        }
    }
}
