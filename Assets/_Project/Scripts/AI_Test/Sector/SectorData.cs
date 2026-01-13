using System;
using UnityEngine;
using System.Linq;

namespace AITest.Sector
{
    /// <summary>
    /// Tek bir sektörün verisi + Hiding Spot Learning
    /// </summary>
    [Serializable]
    public class SectorData
    {
        [Header("Identification")]
        [Tooltip("Sektör ID (A, B, C...)")]
        public string id = "A";
        
        [Header("Bounds")]
        [Tooltip("Sektörün dünya koordinatlarýnda dikdörtgen sýnýrlarý")]
        public Rect bounds;
        
        [Header("Navigation Points")]
        [Tooltip("Giriþ/çýkýþ noktalarý (1-3 portal)")]
        public Vector2[] portals = new Vector2[2];
        
        [Tooltip("Ýç hedef noktalar (1-2 anchor)")]
        public Vector2[] anchors = new Vector2[1];
        
        [Header("Hiding Spots (= Sweep Points)")]
        [Tooltip("Saklanma noktalarý (player için hiding, enemy için sweep)")]
        public Vector2[] sweepPoints = new Vector2[3];
        
        [Header("Hiding Spot Learning (Runtime)")]
        [Tooltip("Her hiding spot için öðrenme istatistikleri")]
        public HidingSpotStats[] hidingStats;
        
        /// <summary>
        /// Hiding stats baþlat (Sectorizer.Awake'de çaðrýlacak)
        /// </summary>
        public void InitializeHidingStats()
        {
            if (sweepPoints == null || sweepPoints.Length == 0) return;
            
            hidingStats = new HidingSpotStats[sweepPoints.Length];
            for (int i = 0; i < hidingStats.Length; i++)
            {
                hidingStats[i] = new HidingSpotStats { sweepIndex = i };
            }
        }
        
        /// <summary>
        /// Verilen pozisyon bu sektör içinde mi?
        /// </summary>
        public bool Contains(Vector2 worldPos)
        {
            return bounds.Contains(worldPos);
        }
        
        /// <summary>
        /// En yakýn portal'ý bul
        /// </summary>
        public Vector2 GetNearestPortal(Vector2 from)
        {
            if (portals == null || portals.Length == 0)
                return bounds.center;
            
            Vector2 nearest = portals[0];
            float minDist = Vector2.Distance(from, nearest);
            
            for (int i = 1; i < portals.Length; i++)
            {
                float dist = Vector2.Distance(from, portals[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = portals[i];
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// En yakýn anchor'u bul
        /// </summary>
        public Vector2 GetNearestAnchor(Vector2 from)
        {
            if (anchors == null || anchors.Length == 0)
                return bounds.center;
            
            Vector2 nearest = anchors[0];
            float minDist = Vector2.Distance(from, nearest);
            
            for (int i = 1; i < anchors.Length; i++)
            {
                float dist = Vector2.Distance(from, anchors[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = anchors[i];
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// Sweep route (AKILLI! Hiding olasýlýðýna göre sýralanmýþ!)
        /// </summary>
        public Vector2[] GetNearestSweepRoute(Vector2 from)
        {
            if (sweepPoints == null || sweepPoints.Length == 0)
                return new Vector2[] { bounds.center };
            
            // ? HÝDÝNG STATS VARSA OLASILIK SIRALAMASI!
            if (hidingStats != null && hidingStats.Length == sweepPoints.Length)
            {
                return sweepPoints
                    .Select((point, index) => new { 
                        Point = point, 
                        Probability = hidingStats[index].GetProbability(), 
                        Index = index 
                    })
                    .OrderByDescending(x => x.Probability)  // Yüksek olasýlýk ÖNCE!
                    .ThenBy(x => Vector2.Distance(x.Point, from))  // Eþitse yakýn olan
                    .Select(x => x.Point)
                    .ToArray();
            }
            
            // Stats yoksa en yakýndan baþla (eski davranýþ)
            int startIndex = 0;
            float minDist = Vector2.Distance(from, sweepPoints[0]);
            
            for (int i = 1; i < sweepPoints.Length; i++)
            {
                float dist = Vector2.Distance(from, sweepPoints[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    startIndex = i;
                }
            }
            
            Vector2[] route = new Vector2[sweepPoints.Length];
            for (int i = 0; i < sweepPoints.Length; i++)
            {
                route[i] = sweepPoints[(startIndex + i) % sweepPoints.Length];
            }
            
            return route;
        }
    }
    
    /// <summary>
    /// Hiding Spot Ýstatistikleri (Her sweep point için)
    /// Player sýk saklanýrsa olasýlýk ?, az saklanýrsa ?
    /// </summary>
    [Serializable]
    public class HidingSpotStats
    {
        public int sweepIndex;              // Hangi sweep point (0-2)
        public int playerFoundCount;        // Kaç kez player burada bulundu (sweep'te)
        public int playerNotFoundCount;     // Kaç kez boþ bulundu (sweep'te)
        public float lastFoundTime;         // En son ne zaman bulundu
        
        // ? YENÝ: Passive tracking (sweep yapmadan öðrenme!)
        public float passivePresenceScore;  // Player bu noktada ne kadar zaman geçirdi (saniye cinsinden)
        public float lastPassiveUpdateTime; // Son passive update zamaný
        
        /// <summary>
        /// Hiding olasýlýðý (0-1 arasý)
        /// Formül: (found + passiveScore) / (total + passiveScore) + temporal decay
        /// </summary>
        public float GetProbability()
        {
            int total = playerFoundCount + playerNotFoundCount;
            
            // ? Passive score'u normalize et (0-10 arasý, her 30s = 1 puan)
            float normalizedPassiveScore = Mathf.Clamp(passivePresenceScore / 30f, 0f, 10f);
            
            // Combined data
            float combinedFound = playerFoundCount + normalizedPassiveScore;
            float combinedTotal = total + normalizedPassiveScore * 2;  // Passive veri daha az aðýrlýklý
            
            if (combinedTotal == 0) return 0.33f;  // Baþlangýç (bilinmiyor)
            
            float rawProbability = combinedFound / combinedTotal;
            
            // ? TEMPORAL DECAY: Eski data daha az etkili!
            float timeSinceFound = Time.time - Mathf.Max(lastFoundTime, lastPassiveUpdateTime);
            float decayFactor = Mathf.Exp(-timeSinceFound / 120f);  // 2 dakikada %63 azalma
            
            // Eski veri ? baþlangýç deðerine (0.33) dön
            float decayedProbability = Mathf.Lerp(0.33f, rawProbability, decayFactor);
            
            return Mathf.Clamp(decayedProbability, 0.05f, 0.95f);
        }
        
        /// <summary>
        /// Player bulunduðunda çaðrýlýr (sweep'te)
        /// </summary>
        public void RecordPlayerFound()
        {
            playerFoundCount++;
            lastFoundTime = Time.time;
        }
        
        /// <summary>
        /// Player bulunmadýðýnda çaðrýlýr (sweep'te)
        /// </summary>
        public void RecordPlayerNotFound()
        {
            playerNotFoundCount++;
        }
        
        /// <summary>
        /// ? YENÝ: Passive presence (sweep yapmadan öðrenme!)
        /// Player bu hiding spot yakýnýndayken her saniye çaðrýlýr
        /// </summary>
        public void RecordPassivePresence(float deltaTime)
        {
            passivePresenceScore += deltaTime;
            lastPassiveUpdateTime = Time.time;
        }
        
        /// <summary>
        /// Debug string
        /// </summary>
        public override string ToString()
        {
            return $"S{sweepIndex}: {playerFoundCount}F/{playerNotFoundCount}NF + {passivePresenceScore:F0}s passive = {GetProbability():P0}";
        }
    }
}
