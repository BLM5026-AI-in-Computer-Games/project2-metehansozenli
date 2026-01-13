using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace AITest.Sector
{
    /// <summary>
    /// Sektör yöneticisi - Inspector'dan 6-8 sektör verisi
    /// Singleton pattern + Hiding Stats Persistence
    /// </summary>
    public class Sectorizer : MonoBehaviour
    {
        public static Sectorizer Instance { get; private set; }
        
        [Header("Sector Configuration")]
        [Tooltip("6-8 sektör verisi (A-H)")]
        public SectorData[] sectors = new SectorData[6];
        
        [Header("Persistence")]
        [Tooltip("Hiding stats'i otomatik yükle/kaydet")]
        public bool autoPersistence = true;
        
        [Header("Debug")]
        [Tooltip("Scene view'da sektörleri çiz")]
        public bool drawGizmos = true;
        
        [Tooltip("Portal/Anchor/Sweep noktalarýný göster")]
        public bool drawPoints = true;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            InitializeAllHidingStats();
            
            // ? Auto-load hiding stats
            if (autoPersistence)
            {
                LoadHidingStats();
            }
        }
        
        private void OnDestroy()
        {
            // ? Auto-save hiding stats
            if (autoPersistence && Instance == this)
            {
                SaveHidingStats();
            }
        }
        
        /// <summary>
        /// Tüm sektörlerin hiding stats'ýný baþlat
        /// </summary>
        private void InitializeAllHidingStats()
        {
            if (sectors == null) return;
            
            foreach (var sector in sectors)
            {
                if (sector != null)
                {
                    sector.InitializeHidingStats();
                }
            }
            
            Debug.Log($"[Sectorizer] Hiding stats initialized for {sectors.Length} sectors");
        }
        
        /// <summary>
        /// ? YENÝ: Hiding stats'i kaydet
        /// </summary>
        public void SaveHidingStats()
        {
            if (sectors == null || sectors.Length == 0) return;
            
            HidingStatsData data = new HidingStatsData
            {
                sectorStats = new List<SectorHidingStats>()
            };
            
            foreach (var sector in sectors)
            {
                if (sector == null || sector.hidingStats == null) continue;
                
                SectorHidingStats sStats = new SectorHidingStats
                {
                    sectorId = sector.id,
                    spotStats = sector.hidingStats
                };
                
                data.sectorStats.Add(sStats);
            }
            
            if (data.sectorStats.Count == 0)
            {
                Debug.Log("[Sectorizer] No hiding stats to save.");
                return;
            }
            
            string json = JsonUtility.ToJson(data, true);
            PlayerPrefs.SetString("Sectorizer_HidingStats", json);
            PlayerPrefs.Save();
            
            Debug.Log($"<color=lime>[Sectorizer] Hiding stats saved: {data.sectorStats.Count} sectors</color>");
        }
        
        /// <summary>
        /// ? YENÝ: Hiding stats'i yükle
        /// </summary>
        public void LoadHidingStats()
        {
            if (!PlayerPrefs.HasKey("Sectorizer_HidingStats"))
            {
                Debug.Log("[Sectorizer] No saved hiding stats found.");
                return;
            }
            
            string json = PlayerPrefs.GetString("Sectorizer_HidingStats");
            HidingStatsData data = JsonUtility.FromJson<HidingStatsData>(json);
            
            if (data == null || data.sectorStats == null || data.sectorStats.Count == 0)
            {
                Debug.LogWarning("[Sectorizer] Invalid hiding stats data.");
                return;
            }
            
            int loadedCount = 0;
            foreach (var sStats in data.sectorStats)
            {
                var sector = GetById(sStats.sectorId);
                if (sector != null && sector.hidingStats != null && sStats.spotStats != null)
                {
                    // Boyutlar uyuþmalý
                    if (sector.hidingStats.Length == sStats.spotStats.Length)
                    {
                        sector.hidingStats = sStats.spotStats;
                        loadedCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[Sectorizer] Hiding stats size mismatch for sector {sStats.sectorId}");
                    }
                }
            }
            
            Debug.Log($"<color=cyan>[Sectorizer] Hiding stats loaded: {loadedCount}/{data.sectorStats.Count} sectors</color>");
        }
        
        /// <summary>
        /// ? YENÝ: Hiding stats'i temizle
        /// </summary>
        public void ClearHidingStats()
        {
            InitializeAllHidingStats(); // Reset to defaults
            PlayerPrefs.DeleteKey("Sectorizer_HidingStats");
            PlayerPrefs.Save();
            Debug.Log("<color=yellow>[Sectorizer] Hiding stats cleared</color>");
        }
        
        /// <summary>
        /// ID'ye göre sektör bul (A-H)
        /// </summary>
        public SectorData GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return sectors.FirstOrDefault(s => s.id == id);
        }
        
        /// <summary>
        /// Pozisyona göre sektör bul
        /// </summary>
        public SectorData GetByPosition(Vector2 pos)
        {
            foreach (var sector in sectors)
            {
                if (sector != null && sector.Contains(pos))
                    return sector;
            }
            return null;
        }
        
        /// <summary>
        /// Pozisyona göre sektör ID döndür
        /// </summary>
        public string GetIdByPosition(Vector2 pos)
        {
            var sector = GetByPosition(pos);
            return sector?.id ?? "None";
        }
        
        /// <summary>
        /// Sektörün en yakýn portalýný bul
        /// </summary>
        public Vector2 GetNearestPortal(SectorData sector, Vector2 from)
        {
            if (sector == null) return from;
            return sector.GetNearestPortal(from);
        }
        
        /// <summary>
        /// Sektörün en yakýn anchor'unu bul
        /// </summary>
        public Vector2 GetNearestAnchor(SectorData sector, Vector2 from)
        {
            if (sector == null) return from;
            return sector.GetNearestAnchor(from);
        }
        
        /// <summary>
        /// Sweep route'u en yakýndan baþlat
        /// </summary>
        public Vector2[] GetNearestSweepRoute(SectorData sector, Vector2 from)
        {
            if (sector == null) return new Vector2[0];
            return sector.GetNearestSweepRoute(from);
        }
        
        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!drawGizmos || sectors == null) return;
            
            Color[] colors = new Color[]
            {
                new Color(1f, 0.5f, 0.5f, 0.3f), // Kýrmýzý
                new Color(0.5f, 1f, 0.5f, 0.3f), // Yeþil
                new Color(0.5f, 0.5f, 1f, 0.3f), // Mavi
                new Color(1f, 1f, 0.5f, 0.3f),   // Sarý
                new Color(1f, 0.5f, 1f, 0.3f),   // Magenta
                new Color(0.5f, 1f, 1f, 0.3f),   // Cyan
                new Color(1f, 0.7f, 0.5f, 0.3f), // Turuncu
                new Color(0.7f, 0.5f, 1f, 0.3f), // Mor
            };
            
            for (int i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                
                Color col = colors[i % colors.Length];
                
                // Bounds
                Gizmos.color = col;
                Vector3 min = new Vector3(sector.bounds.xMin, sector.bounds.yMin, 0);
                Vector3 max = new Vector3(sector.bounds.xMax, sector.bounds.yMax, 0);
                Vector3 size = max - min;
                Gizmos.DrawCube(min + size * 0.5f, size);
                
                if (!drawPoints) continue;
                
                // Portals (Kýrmýzý)
                Gizmos.color = Color.red;
                if (sector.portals != null)
                {
                    foreach (var portal in sector.portals)
                    {
                        Gizmos.DrawWireSphere(portal, 0.5f);
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(portal + Vector2.up * 0.7f, "Portal");
                        #endif
                    }
                }
                
                // Anchors (Yeþil)
                Gizmos.color = Color.green;
                if (sector.anchors != null)
                {
                    foreach (var anchor in sector.anchors)
                    {
                        Gizmos.DrawWireSphere(anchor, 0.4f);
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(anchor + Vector2.up * 0.7f, "Anchor");
                        #endif
                    }
                }
                
                // Sweep points (RENK = Hiding Olasýlýðý!)
                if (sector.sweepPoints != null)
                {
                    for (int j = 0; j < sector.sweepPoints.Length; j++)
                    {
                        // ? Hiding olasýlýðýna göre renk!
                        float prob = 0.33f;  // Default
                        if (sector.hidingStats != null && j < sector.hidingStats.Length)
                        {
                            prob = sector.hidingStats[j].GetProbability();
                        }
                        
                        // Yeþil (düþük) ? Kýrmýzý (yüksek)
                        Gizmos.color = Color.Lerp(Color.green, Color.red, prob);
                        Gizmos.DrawSphere(sector.sweepPoints[j], 0.3f + prob * 0.3f);  // Boyut da artar!
                        
                        // Sweep route çizgisi
                        if (j < sector.sweepPoints.Length - 1)
                        {
                            Gizmos.color = Color.blue;
                            Gizmos.DrawLine(sector.sweepPoints[j], sector.sweepPoints[j + 1]);
                        }
                        
                        #if UNITY_EDITOR
                        string label = $"S{j}";
                        if (sector.hidingStats != null && j < sector.hidingStats.Length)
                        {
                            label += $"\n{prob:P0}";  // Olasýlýðý göster!
                        }
                        UnityEditor.Handles.Label(sector.sweepPoints[j] + Vector2.up * 0.5f, label);
                        #endif
                    }
                }
                
                // Sektör ID label
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(sector.bounds.center, $"Sector {sector.id}");
                #endif
            }
        }
        #endregion
    }
    
    /// <summary>
    /// ? YENÝ: Serializable hiding stats data
    /// </summary>
    [System.Serializable]
    public class HidingStatsData
    {
        public List<SectorHidingStats> sectorStats;
    }
    
    [System.Serializable]
    public class SectorHidingStats
    {
        public string sectorId;
        public HidingSpotStats[] spotStats;
    }
}
