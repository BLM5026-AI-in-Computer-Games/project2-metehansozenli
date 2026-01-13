using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace Metrics
{
    /// <summary>
    /// Mini Charts - Simple line graph visualization using Unity UI
    /// Draws rolling statistics (success rate, time to capture, reward)
    /// </summary>
    public class MiniCharts : MonoBehaviour
    {
        [Header("Chart Settings")]
        [Tooltip("Chart width in pixels")]
        public int chartWidth = 200;
        
        [Tooltip("Chart height in pixels")]
        public int chartHeight = 60;
        
        [Tooltip("Number of data points to show")]
        public int dataPointsToShow = 100;
        
        [Tooltip("Update interval (seconds)")]
        public float updateInterval = 1f;
        
        [Header("Chart Targets")]
        [Tooltip("Success rate chart RawImage")]
        public RawImage successRateChart;
        
        [Tooltip("Time to capture chart RawImage")]
        public RawImage timeToCaptureChart;
        
        [Tooltip("Reward chart RawImage")]
        public RawImage rewardChart;
        
        [Header("Colors")]
        public Color successColor = Color.green;
        public Color timeColor = Color.cyan;
        public Color rewardColor = Color.yellow;
        public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        public Color gridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        
        private Texture2D successTexture;
        private Texture2D timeTexture;
        private Texture2D rewardTexture;
        
        private float nextUpdateTime;
        
        private void Start()
        {
            // Create textures
            successTexture = CreateTexture();
            timeTexture = CreateTexture();
            rewardTexture = CreateTexture();
            
            if (successRateChart) successRateChart.texture = successTexture;
            if (timeToCaptureChart) timeToCaptureChart.texture = timeTexture;
            if (rewardChart) rewardChart.texture = rewardTexture;
            
            nextUpdateTime = Time.time + updateInterval;
        }
        
        private void Update()
        {
            if (Time.time >= nextUpdateTime)
            {
                UpdateCharts();
                nextUpdateTime = Time.time + updateInterval;
            }
        }
        
        private Texture2D CreateTexture()
        {
            Texture2D tex = new Texture2D(chartWidth, chartHeight, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            return tex;
        }
        
        private void UpdateCharts()
        {
            if (MetricsManager.Instance == null) return;
            
            var episodes = MetricsManager.Instance.GetAllEpisodes();
            if (episodes.Count == 0) return;
            
            // Update success rate chart
            if (successRateChart && successTexture)
            {
                DrawLineChart(successTexture, episodes, e => e.success ? 1f : 0f, 
                    0f, 1f, successColor, "Success Rate");
                successTexture.Apply();
            }
            
            // Update time to capture chart
            if (timeToCaptureChart && timeTexture)
            {
                // Filter successful episodes only
                var successfulEpisodes = episodes.FindAll(e => e.success && e.timeToCapture > 0);
                if (successfulEpisodes.Count > 0)
                {
                    float maxTime = successfulEpisodes.Max(e => e.timeToCapture);
                    DrawLineChart(timeTexture, successfulEpisodes, e => e.timeToCapture, 
                        0f, maxTime, timeColor, "Time to Capture");
                    timeTexture.Apply();
                }
            }
            
            // Update reward chart
            if (rewardChart && rewardTexture)
            {
                float minReward = episodes.Min(e => e.totalReward);
                float maxReward = episodes.Max(e => e.totalReward);
                DrawLineChart(rewardTexture, episodes, e => e.totalReward, 
                    minReward, maxReward, rewardColor, "Reward");
                rewardTexture.Apply();
            }
        }
        
        private void DrawLineChart<T>(Texture2D texture, System.Collections.Generic.List<T> data, 
            System.Func<T, float> valueExtractor, float minValue, float maxValue, Color lineColor, string label)
        {
            // Clear texture
            Color[] pixels = new Color[chartWidth * chartHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = backgroundColor;
            }
            texture.SetPixels(pixels);
            
            // Draw grid lines (horizontal)
            for (int y = 0; y < chartHeight; y += chartHeight / 4)
            {
                for (int x = 0; x < chartWidth; x++)
                {
                    texture.SetPixel(x, y, gridColor);
                }
            }
            
            if (data.Count < 2) return;
            
            // Get last N data points
            int startIdx = Mathf.Max(0, data.Count - dataPointsToShow);
            int count = data.Count - startIdx;
            
            // Draw line
            float range = maxValue - minValue;
            if (range < 0.001f) range = 1f; // Avoid division by zero
            
            for (int i = 1; i < count; i++)
            {
                float value1 = valueExtractor(data[startIdx + i - 1]);
                float value2 = valueExtractor(data[startIdx + i]);
                
                int x1 = (int)((i - 1) / (float)count * chartWidth);
                int x2 = (int)(i / (float)count * chartWidth);
                
                int y1 = (int)((value1 - minValue) / range * chartHeight);
                int y2 = (int)((value2 - minValue) / range * chartHeight);
                
                // Clamp
                x1 = Mathf.Clamp(x1, 0, chartWidth - 1);
                x2 = Mathf.Clamp(x2, 0, chartWidth - 1);
                y1 = Mathf.Clamp(y1, 0, chartHeight - 1);
                y2 = Mathf.Clamp(y2, 0, chartHeight - 1);
                
                // Draw line segment (simple Bresenham)
                DrawLine(texture, x1, y1, x2, y2, lineColor);
            }
        }
        
        private void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            
            while (true)
            {
                if (x0 >= 0 && x0 < chartWidth && y0 >= 0 && y0 < chartHeight)
                {
                    texture.SetPixel(x0, y0, color);
                }
                
                if (x0 == x1 && y0 == y1) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}
