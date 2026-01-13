using UnityEngine;
using System.Collections.Generic;
using System.Linq; // ⚡ YENİ: LINQ for OrderByDescending
using AITest.Sector; // ⚡ YENİ: Sectorizer için

namespace AITest.Core
{
    /// <summary>
    /// Tracks player position history and calculates density heatmap
    /// Used by ThreatPerceptron to detect frequently visited areas
    /// </summary>
    public class HeatmapTracker : MonoBehaviour
    {
        [Header("Tracking Settings")]
        [Tooltip("Player position update interval (seconds)")]
        [Range(0.5f, 5f)]
        public float updateInterval = 0.5f; // ⚡ 1f → 0.5f (2x daha hızlı tracking)
        
        [Tooltip("Heatmap cell size (meters)")]
        [Range(0.5f, 2f)]
        public float cellSize = 1f;
        
        [Tooltip("Maximum history size (entries)")]
        public int maxHistorySize = 300; // 5 minutes at 1s interval → 2.5 dakika at 0.5s
        
        [Tooltip("Density calculation radius (meters)")]
        [Range(1f, 10f)]
        public float densityRadius = 5f; // ⚡ 2f → 5f (daha geniş alan, daha fazla entry)
        
        [Header("Temporal Decay")]
        [Tooltip("Eski data decay oranı (saniye başına) - DÜŞÜK tutun!")]
        [Range(0f, 0.01f)]
        public float decayRate = 0.0005f; // ⚡ 0.001f → 0.0005f (2x daha yavaş decay!)
        
        [Tooltip("Minimum density threshold (altındakiler sıfırlanır)")]
        [Range(0f, 0.2f)]
        public float minDensityThreshold = 0.01f; // ⚡ 0.02f → 0.01f (daha düşük eşik)
        
        [Header("References")]
        public Transform player;
        
        [Header("Debug")]
        public bool debugMode = false;
        public bool drawGizmos = false;
        
        [Tooltip("Debug log aralığı (saniye)")]
        public float debugLogInterval = 10f;
        
        [Tooltip("Path çizgisini göster (performans için opsiyonel)")]
        public bool drawPath = true; // ⚡ YENİ: Path toggle
        
        [Tooltip("Heatmap grid'i göster (top 10 yoğun cell)")]
        public bool drawHeatmapGrid = true; // ⚡ YENİ: Heatmap toggle
        
        [Tooltip("Path simplification (her N entry'de 1 çiz)")]
        [Range(1, 10)]
        public int pathSimplification = 3; // ⚡ YENİ: Her 3 entry'de 1 çiz

        // Position history (circular buffer with timestamps)
        private List<(Vector2 position, float timestamp)> positionHistory = new List<(Vector2, float)>();
        private float lastUpdateTime;
        private float lastDebugLogTime; // ⚡ YENİ: Debug log timer
        
        // Heatmap grid (key: grid cell, value: (count, lastUpdateTime))
        private Dictionary<Vector2Int, (int count, float lastUpdate)> heatmapGrid = new Dictionary<Vector2Int, (int, float)>();
        
        // Singleton
        public static HeatmapTracker Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Auto-find player
            if (!player)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj) player = playerObj.transform;
            }
        }
        
        private void Start()
        {
            lastUpdateTime = Time.time;
            
            // ⚡ Diagnostic check
            if (!player)
            {
                Debug.LogError("<color=red>[HeatmapTracker] ❌ PLAYER NOT FOUND! HeatmapTracker DISABLED!</color>");
            }
            else
            {
                Debug.Log($"<color=lime>[HeatmapTracker] ✅ Initialized! Player: {player.name}, Update Interval: {updateInterval}s</color>");
            }
        }
        
        private void Update()
        {
            if (!player) return;
            
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                TrackPosition();
                lastUpdateTime = Time.time;
            }
            
            // ⚡ Periodic debug log
            if (debugMode && Time.time - lastDebugLogTime >= debugLogInterval)
            {
                LogHeatmapStatus();
                lastDebugLogTime = Time.time;
            }
        }
        
        private void TrackPosition()
        {
            Vector2 pos = player.position;
            float currentTime = Time.time;
            
            // Add to history with timestamp
            positionHistory.Add((pos, currentTime));
            
            // Limit history size
            if (positionHistory.Count > maxHistorySize)
            {
                positionHistory.RemoveAt(0);
            }
            
            // Update grid with timestamp
            Vector2Int gridCell = WorldToGrid(pos);
            if (heatmapGrid.ContainsKey(gridCell))
            {
                var existing = heatmapGrid[gridCell];
                heatmapGrid[gridCell] = (existing.count + 1, currentTime);
            }
            else
            {
                heatmapGrid[gridCell] = (1, currentTime);
            }
            
            // ⚡ Apply decay to old grid cells
            ApplyTemporalDecay(currentTime);
            
            // ❌ LOG KALDIRILDI - Spam yapıyordu
        }
        
        /// <summary>
        /// ⚡ YENİ: Heatmap durumunu logla
        /// </summary>
        private void LogHeatmapStatus()
        {
            if (positionHistory.Count == 0)
            {
                Debug.Log("<color=yellow>[HeatmapTracker] No data yet</color>");
                return;
            }
            
            // Top 5 en yoğun sektörleri bul
            if (!Sectorizer.Instance) return;
            
            var sectorDensities = new System.Collections.Generic.List<(string id, float density)>();
            
            foreach (var sector in Sectorizer.Instance.sectors)
            {
                if (sector == null) continue;
                
                float density = GetDensityAt(sector.bounds.center);
                sectorDensities.Add((sector.id, density));
            }
            
            sectorDensities.Sort((a, b) => b.density.CompareTo(a.density));
            
            string topSectors = "";
            for (int i = 0; i < Mathf.Min(5, sectorDensities.Count); i++)
            {
                topSectors += $"{sectorDensities[i].id}={sectorDensities[i].density:F2} ";
            }
            
            Debug.Log($"<color=magenta>[HeatmapTracker] 📊 History: {positionHistory.Count}/{maxHistorySize} | Grid: {heatmapGrid.Count} cells | Top sectors: {topSectors}</color>");
        }
        
        /// <summary>
        /// ? YENİ: Eski data'yı azalt (temporal decay)
        /// </summary>
        private void ApplyTemporalDecay(float currentTime)
        {
            var cellsToRemove = new List<Vector2Int>();
            var cellsToUpdate = new List<(Vector2Int cell, int newCount, float lastUpdate)>();
            
            foreach (var kvp in heatmapGrid)
            {
                Vector2Int cell = kvp.Key;
                (int count, float lastUpdate) = kvp.Value;
                
                // Zaman farkı
                float timeSinceUpdate = currentTime - lastUpdate;
                
                // Decay uygula (exponential decay)
                float decayFactor = Mathf.Exp(-decayRate * timeSinceUpdate);
                int newCount = Mathf.RoundToInt(count * decayFactor);
                
                if (newCount < 1)
                {
                    cellsToRemove.Add(cell);
                }
                else if (newCount != count)
                {
                    cellsToUpdate.Add((cell, newCount, lastUpdate));
                }
            }
            
            // Apply updates
            foreach (var cell in cellsToRemove)
            {
                heatmapGrid.Remove(cell);
            }
            
            foreach (var (cell, newCount, lastUpdate) in cellsToUpdate)
            {
                heatmapGrid[cell] = (newCount, lastUpdate);
            }
        }
        
        /// <summary>
        /// Get density at a specific position (0-1 normalized)
        /// ⚡ WITH TEMPORAL DECAY + OPTIMIZED FORMULA
        /// </summary>
        public float GetDensityAt(Vector2 position)
        {
            if (positionHistory.Count == 0) return 0f;
            
            float currentTime = Time.time;
            
            // Count nearby history entries WITH temporal weight
            float weightedCount = 0f;
            float radiusSq = densityRadius * densityRadius;
            
            foreach (var (histPos, timestamp) in positionHistory)
            {
                float distSq = (histPos - position).sqrMagnitude;
                if (distSq <= radiusSq)
                {
                    // Temporal weight (eski data daha az etkili)
                    float timeSince = currentTime - timestamp;
                    float timeWeight = Mathf.Exp(-decayRate * timeSince);
                    
                    // ⚡ Distance weight (yakın entry'ler daha değerli)
                    float distanceWeight = 1f - (Mathf.Sqrt(distSq) / densityRadius);
                    
                    weightedCount += timeWeight * distanceWeight;
                }
            }
            
            // ⚡ Normalize (daha hassas formül)
            // Normalization factor: En fazla kaç entry olabilir bu radius'ta?
            float maxPossibleEntries = Mathf.PI * densityRadius * densityRadius / (cellSize * cellSize);
            float density = weightedCount / Mathf.Max(10f, maxPossibleEntries); // Min 10 entry baseline
            
            // Threshold control (çok düşük density'yi sıfırla)
            if (density < minDensityThreshold)
                return 0f;
            
            return Mathf.Clamp01(density * 3f); // ⚡ 5f → 3f (daha dengeli scaling)
        }
        
        /// <summary>
        /// Get grid cell visit count (WITH DECAY)
        /// </summary>
        public int GetVisitCount(Vector2 position)
        {
            Vector2Int gridCell = WorldToGrid(position);
            if (!heatmapGrid.ContainsKey(gridCell)) return 0;
            
            // Apply current decay
            float currentTime = Time.time;
            (int count, float lastUpdate) = heatmapGrid[gridCell];
            float timeSince = currentTime - lastUpdate;
            float decayFactor = Mathf.Exp(-decayRate * timeSince);
            
            return Mathf.RoundToInt(count * decayFactor);
        }
        
        /// <summary>
        /// Clear all tracking data
        /// </summary>
        public void ClearHistory()
        {
            positionHistory.Clear();
            heatmapGrid.Clear();
            Debug.Log("<color=yellow>[HeatmapTracker] History cleared</color>");
        }
        
        /// <summary>
        /// ⚡ YENİ: En yoğun N noktayı bul (Hotspot Detection)
        /// Search behavior için kullanılır
        /// </summary>
        public List<Vector2> GetTopHotspots(int maxCount = 5, float minDensity = 0.15f)
        {
            if (positionHistory.Count < 10) return new List<Vector2>(); // Yeterli data yok
            
            var hotspots = new List<(Vector2 pos, float density)>();
            
            // Grid cell'leri density'e göre sırala
            var sortedCells = heatmapGrid
                .OrderByDescending(kvp => kvp.Value.count)
                .Take(maxCount * 2); // 2x fazla al, sonra filtrele
            
            foreach (var cell in sortedCells)
            {
                Vector2 worldPos = new Vector2(
                    cell.Key.x * cellSize + cellSize * 0.5f,
                    cell.Key.y * cellSize + cellSize * 0.5f
                );
                
                float density = GetDensityAt(worldPos);
                
                if (density >= minDensity)
                {
                    hotspots.Add((worldPos, density));
                }
            }
            
            // Top N hotspot (density'e göre sıralı)
            return hotspots
                .OrderByDescending(h => h.density)
                .Take(maxCount)
                .Select(h => h.pos)
                .ToList();
        }
        
        private Vector2Int WorldToGrid(Vector2 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.y / cellSize)
            );
        }
        
        private void OnDrawGizmos()
        {
            if (!drawGizmos || positionHistory == null || positionHistory.Count == 0) return;
            
            // ⚡ 1. Draw position history (SIMPLIFIED)
            if (drawPath && positionHistory.Count > 1)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // Magenta
                
                for (int i = 0; i < positionHistory.Count - pathSimplification; i += pathSimplification)
                {
                    int nextIndex = Mathf.Min(i + pathSimplification, positionHistory.Count - 1);
                    Gizmos.DrawLine(positionHistory[i].position, positionHistory[nextIndex].position);
                }
                
                // Current position marker (son pozisyon)
                if (positionHistory.Count > 0)
                {
                    Gizmos.color = new Color(1f, 0f, 1f, 1f);
                    Gizmos.DrawWireSphere(positionHistory[positionHistory.Count - 1].position, 0.3f);
                }
            }
            
            // ⚡ 2. Draw heatmap grid (top 10 visited cells)
            if (drawHeatmapGrid && heatmapGrid.Count > 0)
            {
                var sortedCells = new List<KeyValuePair<Vector2Int, (int count, float lastUpdate)>>(heatmapGrid);
                sortedCells.Sort((a, b) => b.Value.count.CompareTo(a.Value.count));
                
                for (int i = 0; i < Mathf.Min(10, sortedCells.Count); i++)
                {
                    var cell = sortedCells[i];
                    Vector2 worldPos = new Vector2(cell.Key.x * cellSize + cellSize * 0.5f, cell.Key.y * cellSize + cellSize * 0.5f);
                    
                    // Color intensity based on visit count (max count = full red)
                    float maxCount = sortedCells[0].Value.count;
                    float intensity = Mathf.Clamp01(cell.Value.count / (float)maxCount);
                    Gizmos.color = new Color(1f, 0f, 0f, intensity * 0.6f);
                    Gizmos.DrawCube(worldPos, Vector3.one * cellSize * 0.9f);
                    
                    // Label (visit count)
                    #if UNITY_EDITOR
                    UnityEditor.Handles.color = Color.white;
                    UnityEditor.Handles.Label(worldPos + Vector2.up * 0.5f, $"{cell.Value.count}");
                    #endif
                }
            }
        }
    }
}
