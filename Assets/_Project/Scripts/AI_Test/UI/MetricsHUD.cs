using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace AITest.UI
{
    /// <summary>
    /// Metrics HUD - In-game visualization of training metrics
    /// 
    /// PURPOSE: Real-time feedback during training for monitoring convergence
    /// - Episode counter and success rate
    /// - Rolling averages (reward, duration, time-to-capture)
    /// - Action distribution bar chart
    /// - Epsilon decay visualization
    /// - TD error trend (convergence proxy)
    /// 
    /// USAGE: Attach to Canvas GameObject, assign UI elements in inspector
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MetricsHUD : MonoBehaviour
    {
        #region Configuration
        [Header("Display Settings")]
        [Tooltip("Show/hide HUD")]
        public bool showHUD = true;

        [Tooltip("Update frequency (seconds)")]
        [Range(0.1f, 2f)]
        public float updateInterval = 0.5f;

        [Tooltip("Toggle key")]
        public KeyCode toggleKey = KeyCode.F1;

        [Header("UI References")]
        [Tooltip("Main panel (for show/hide)")]
        public GameObject mainPanel;

        [Tooltip("Episode text (e.g., 'Episode: 245/500')")]
        public Text episodeText;

        [Tooltip("Success rate text")]
        public Text successRateText;

        [Tooltip("Average reward text")]
        public Text avgRewardText;

        [Tooltip("Average duration text")]
        public Text avgDurationText;

        [Tooltip("Epsilon text")]
        public Text epsilonText;

        [Tooltip("TD error text")]
        public Text tdErrorText;

        [Tooltip("Time to capture text")]
        public Text timeToCaptureText;

        [Header("Action Distribution Bar Chart")]
        [Tooltip("Parent for action bars")]
        public Transform actionBarsParent;

        [Tooltip("Action bar prefab (Image with Text child)")]
        public GameObject actionBarPrefab;

        [Header("Action Bar Colors")]
        public Color[] actionColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1.0f),   // Patrol - Blue
            new Color(1.0f, 0.3f, 0.3f),   // Investigate - Red
            new Color(1.0f, 0.7f, 0.2f),   // HeatSearch - Orange
            new Color(0.3f, 1.0f, 0.3f),   // Sweep - Green
            new Color(0.7f, 0.3f, 1.0f),   // HideSpot - Purple
            new Color(1.0f, 0.5f, 0.8f),   // Ambush - Pink
            new Color(0.5f, 1.0f, 1.0f),   // HeatSweep - Cyan
            new Color(1.0f, 1.0f, 0.3f),   // AmbushChoke - Yellow
        };
        #endregion

        #region State
        private float nextUpdateTime;
        private AITest.Metrics.MetricsCollector metricsCollector;
        private AITest.Learning.EpisodeManager episodeManager;
        private AITest.Enemy.EnemyBrain enemyBrain;

        // Action bars (created dynamically)
        private Image[] actionBarImages;
        private Text[] actionBarTexts;
        private string[] actionNames = { "Patrol", "Investigate", "HeatSearch", "Sweep", "HideSpot", "Ambush", "HeatSweep", "AmbushChoke" };
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Auto-find components
            metricsCollector = FindFirstObjectByType<AITest.Metrics.MetricsCollector>();
            episodeManager = FindFirstObjectByType<AITest.Learning.EpisodeManager>();
            enemyBrain = FindFirstObjectByType<AITest.Enemy.EnemyBrain>();

            // Create action bars
            CreateActionBars();

            // Initial visibility
            if (mainPanel)
            {
                mainPanel.SetActive(showHUD);
            }
        }

        private void Update()
        {
            // Toggle visibility
            if (Input.GetKeyDown(toggleKey))
            {
                showHUD = !showHUD;
                if (mainPanel)
                {
                    mainPanel.SetActive(showHUD);
                }
            }

            // Update HUD
            if (showHUD && Time.time >= nextUpdateTime)
            {
                UpdateHUD();
                nextUpdateTime = Time.time + updateInterval;
            }
        }
        #endregion

        #region HUD Update
        /// <summary>
        /// Update all HUD elements
        /// </summary>
        private void UpdateHUD()
        {
            if (!metricsCollector) return;

            // Get rolling stats
            var stats = metricsCollector.GetRollingStats();
            int totalEpisodes = metricsCollector.GetTotalEpisodes();
            int maxEpisodes = episodeManager ? episodeManager.maxEpisodes : 500;

            // Episode counter
            if (episodeText)
            {
                episodeText.text = $"Episode: {totalEpisodes}/{maxEpisodes}";
            }

            // Success rate
            if (successRateText)
            {
                successRateText.text = $"Success Rate: {stats.successRate * 100:F1}%";

                // Color code: Green if > 50%, Yellow if > 25%, Red otherwise
                if (stats.successRate > 0.5f)
                    successRateText.color = Color.green;
                else if (stats.successRate > 0.25f)
                    successRateText.color = Color.yellow;
                else
                    successRateText.color = Color.red;
            }

            // Average reward
            if (avgRewardText)
            {
                avgRewardText.text = $"Avg Reward: {stats.avgReward:F1}";
            }

            // Average duration
            if (avgDurationText)
            {
                avgDurationText.text = $"Avg Duration: {stats.avgDuration:F1}s";
            }

            // Epsilon
            if (epsilonText && enemyBrain && enemyBrain.qLearningPolicy != null)
            {
                float epsilon = enemyBrain.qLearningPolicy.epsilon;
                epsilonText.text = $"Epsilon: {epsilon:F3}";

                // Color code: Red (high exploration) -> Green (low exploration)
                epsilonText.color = Color.Lerp(Color.green, Color.red, epsilon);
            }

            // TD error
            if (tdErrorText)
            {
                tdErrorText.text = $"Avg TD Error: {stats.avgTDError:F3}";

                // Color code: Green if < 1.0, Yellow if < 5.0, Red otherwise
                if (stats.avgTDError < 1.0f)
                    tdErrorText.color = Color.green;
                else if (stats.avgTDError < 5.0f)
                    tdErrorText.color = Color.yellow;
                else
                    tdErrorText.color = Color.red;
            }

            // Time to capture
            if (timeToCaptureText)
            {
                if (stats.avgTimeToCapture > 0)
                {
                    timeToCaptureText.text = $"Avg Capture Time: {stats.avgTimeToCapture:F1}s";
                }
                else
                {
                    timeToCaptureText.text = "Avg Capture Time: N/A";
                }
            }

            // Action distribution bars
            UpdateActionBars(stats.totalActionCounts);
        }

        /// <summary>
        /// Update action distribution bar chart
        /// </summary>
        private void UpdateActionBars(int[] actionCounts)
        {
            if (actionBarImages == null || actionCounts == null) return;

            int totalActions = actionCounts.Sum();
            if (totalActions == 0) return;

            for (int i = 0; i < 8; i++)
            {
                if (i >= actionBarImages.Length) break;

                // Calculate percentage
                float percentage = (float)actionCounts[i] / totalActions;

                // Update bar width (scale X)
                var rectTransform = actionBarImages[i].rectTransform;
                rectTransform.localScale = new Vector3(percentage, 1f, 1f);

                // Update text
                if (actionBarTexts[i] != null)
                {
                    actionBarTexts[i].text = $"{actionNames[i]}: {percentage * 100:F1}% ({actionCounts[i]})";
                }
            }
        }
        #endregion

        #region Action Bar Creation
        /// <summary>
        /// Create action distribution bars dynamically
        /// </summary>
        private void CreateActionBars()
        {
            if (!actionBarsParent || !actionBarPrefab) return;

            actionBarImages = new Image[8];
            actionBarTexts = new Text[8];

            for (int i = 0; i < 8; i++)
            {
                // Instantiate bar
                GameObject barObj = Instantiate(actionBarPrefab, actionBarsParent);
                barObj.name = $"ActionBar_{actionNames[i]}";

                // Get Image component
                actionBarImages[i] = barObj.GetComponent<Image>();
                if (actionBarImages[i] && i < actionColors.Length)
                {
                    actionBarImages[i].color = actionColors[i];
                }

                // Get Text component (child)
                actionBarTexts[i] = barObj.GetComponentInChildren<Text>();
                if (actionBarTexts[i])
                {
                    actionBarTexts[i].text = $"{actionNames[i]}: 0%";
                }
            }
        }
        #endregion

        #region Manual Refresh
        /// <summary>
        /// Force immediate HUD update (for debugging)
        /// </summary>
        [ContextMenu("Force Update HUD")]
        public void ForceUpdate()
        {
            UpdateHUD();
        }
        #endregion
    }
}
