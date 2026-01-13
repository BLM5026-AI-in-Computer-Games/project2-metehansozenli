using UnityEngine;
using AITest.Sector;

namespace AITest.Enemy
{
    /// <summary>
    /// Passive Hiding Spot Tracking
    /// Player hiding spot yakýnýndayken (sweep yapmadan) öðrenme
    /// </summary>
    public class HidingSpotTracker : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Player kaç metre içinde hiding spot kullanýyor sayýlýr?")]
        [Range(1f, 5f)] public float detectionRadius = 3f;
        
        [Tooltip("Güncelleme sýklýðý (saniye)")]
        [Range(0.5f, 5f)] public float updateInterval = 1f;
        
        [Header("References")]
        public Transform player;
        public Sectorizer sectorizer;
        
        [Header("Debug")]
        public bool debugMode = false;
        
        private float nextUpdateTime;
        
        private void Awake()
        {
            // Auto-find player
            if (!player)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj) player = playerObj.transform;
            }
            
            // Auto-find sectorizer
            if (!sectorizer)
            {
                sectorizer = Sectorizer.Instance;
            }
        }
        
        private void Update()
        {
            if (Time.time < nextUpdateTime) return;
            nextUpdateTime = Time.time + updateInterval;
            
            TrackPlayerHiding();
        }
        
        /// <summary>
        /// Player hangi hiding spot yakýnýnda? Kaydet!
        /// </summary>
        private void TrackPlayerHiding()
        {
            if (player == null || sectorizer == null) return;
            
            Vector2 playerPos = player.position;
            
            // Player hangi sektörde?
            var currentSector = sectorizer.GetByPosition(playerPos);
            if (currentSector == null) return;
            
            // Hiding stats var mý?
            if (currentSector.hidingStats == null || currentSector.sweepPoints == null) return;
            
            // En yakýn hiding spot'u bul
            float minDist = float.MaxValue;
            int closestIndex = -1;
            
            for (int i = 0; i < currentSector.sweepPoints.Length; i++)
            {
                float dist = Vector2.Distance(playerPos, currentSector.sweepPoints[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }
            
            // Detection radius içinde mi?
            if (closestIndex >= 0 && minDist <= detectionRadius)
            {
                // ? PASSIVE PRESENCE KAYDET!
                currentSector.hidingStats[closestIndex].RecordPassivePresence(updateInterval);
                
                if (debugMode)
                {
                    Debug.Log($"<color=cyan>[HidingTracker] Player near {currentSector.id}-S{closestIndex} (dist={minDist:F1}m) " +
                             $"Score: {currentSector.hidingStats[closestIndex].passivePresenceScore:F0}s " +
                             $"Prob: {currentSector.hidingStats[closestIndex].GetProbability():P0}</color>");
                }
            }
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!debugMode || player == null || sectorizer == null) return;
            
            Vector2 playerPos = player.position;
            var currentSector = sectorizer.GetByPosition(playerPos);
            
            if (currentSector == null || currentSector.sweepPoints == null) return;
            
            // Detection radius çiz
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(playerPos, detectionRadius);
            
            // En yakýn hiding spot'u vurgula
            float minDist = float.MaxValue;
            int closestIndex = -1;
            
            for (int i = 0; i < currentSector.sweepPoints.Length; i++)
            {
                float dist = Vector2.Distance(playerPos, currentSector.sweepPoints[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }
            
            if (closestIndex >= 0 && minDist <= detectionRadius)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(playerPos, currentSector.sweepPoints[closestIndex]);
                
                UnityEditor.Handles.Label(
                    currentSector.sweepPoints[closestIndex], 
                    $"ACTIVE: {currentSector.hidingStats[closestIndex].passivePresenceScore:F0}s"
                );
            }
        }
        #endif
    }
}
