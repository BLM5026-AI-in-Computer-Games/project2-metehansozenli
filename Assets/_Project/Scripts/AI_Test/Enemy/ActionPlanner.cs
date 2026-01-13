using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // ? YENÝ: LINQ for Where/OrderBy
using AITest.Learning;
using AITest.Sector;
using AITest.Perception;

namespace AITest.Enemy
{
    /// <summary>
    /// RL action'ý ? hedef/rota'ya çevirir
    /// GoTo, Sweep, Ambush, Patrol
    /// </summary>
    public class ActionPlanner : MonoBehaviour
    {
        [Header("References")]
        public AICharacterController AICharacterController;
        public Perception.Perception perception;
        public Sectorizer sectorizer;
        
        [Header("Sweep Settings")]
        [Tooltip("Her sweep noktasýnda durakla (saniye)")]
        public float sweepPauseDuration = 0.75f;
        
        [Header("Ambush Settings")]
        [Tooltip("Ambush bekleme süresi (saniye)")]
        public float ambushWaitDuration = 6f;
        
        [Header("Action Lock")]
        [Tooltip("Action flip-flop önleme (saniye)")]
        public float actionLockDuration = 1.0f; // ? 2.0 ? 1.0s (daha hýzlý karar deðiþimi!)
        
        [Tooltip("Minimum execution süresi (saniye)")]
        public float minExecutionTime = 1.0f; // ? 1.5 ? 1.0s
        
        [Header("Debug")]
        public bool debugMode = true; // ? TRUE yap (action log'larýný aç!)
        
        [Header("Patrol Settings")]
        [Tooltip("Kaç sektöre patrol yapýlacak")]
        [Range(2, 5)]
        public int patrolSectorCount = 3;
        
        [Tooltip("Heatmap yoðunluðu eþiði (bu deðerin üstündeki sektörler tercih edilir)")]
        [Range(0f, 1f)]
        public float heatmapThreshold = 0.2f;
        
        [Tooltip("Exploration oraný (random sektör seçme þansý %)")]
        [Range(0f, 0.5f)]
        public float explorationRate = 0.2f; // %20 random sektor

        private RLAction currentAction;
        private bool isExecuting = false;
        private float lastActionTime = -999f;
        
        public bool IsExecuting => isExecuting;
        public RLAction CurrentAction => currentAction;
        
        private void Awake()
        {
            if (!AICharacterController) AICharacterController = GetComponent<AICharacterController>();
            if (!perception) perception = GetComponent<Perception.Perception>();
            if (!sectorizer) sectorizer = Sectorizer.Instance;
        }
        
        /// <summary>
        /// Action'ý execute et
        /// </summary>
        public void Execute(RLAction action, string targetSectorId = null)
        {
            // ? FIX: Chase action için özel durum - Player görünüyorsa action deðiþtirilebilir!
            bool isUrgentChase = (action == RLAction.GoToLastSeen || action == RLAction.GoToLastHeard) 
                                 && perception && perception.PlayerVisible;
            
            // ? 1. Action çalýþýyorsa YENÝ action'ý REDDET! (EXCEPT urgent chase)
            if (isExecuting && !isUrgentChase)
            {
                if (debugMode)
                    Debug.Log($"<color=red>[ActionPlanner] ? REJECTED: {action} (busy with {currentAction})</color>");
                return;
            }
            
            // ? 2. Urgent chase ise mevcut action'ý iptal et!
            if (isExecuting && isUrgentChase)
            {
                if (debugMode)
                    Debug.Log($"<color=orange>[ActionPlanner] ?? INTERRUPTING {currentAction} for URGENT CHASE!</color>");
                
                StopAllCoroutines(); // Mevcut action'ý kes!
                isExecuting = false;
            }
            
            // ? 3. Ayný action tekrar baþlatma (cooldown) - EXCEPT urgent chase
            if (currentAction == action && Time.time - lastActionTime < actionLockDuration && !isUrgentChase)
            {
                if (debugMode)
                    Debug.Log($"<color=orange>[ActionPlanner] ?? SAME ACTION cooldown: {action}</color>");
                return;
            }
            
            currentAction = action;
            lastActionTime = Time.time;
            
            // ? BURADA isExecuting = true set et!
            isExecuting = true;
            
            if (debugMode)
                Debug.Log($"<color=cyan>[ActionPlanner] ? Executing action: {action} (sector={targetSectorId}){(isUrgentChase ? " [URGENT!]" : "")}</color>");
            
            switch (action)
            {
                case RLAction.GoToLastSeen:
                    StartCoroutine(ExecuteGoTo(perception.LastSeenPos, "LastSeen"));
                    break;
                
                case RLAction.GoToLastHeard:
                    StartCoroutine(ExecuteGoTo(perception.LastHeardPos, "LastHeard"));
                    break;
                
                case RLAction.SweepNearest3:
                    StartCoroutine(ExecuteSweep(targetSectorId));
                    break;
                
                case RLAction.AmbushBestPortal:
                    StartCoroutine(ExecuteAmbush(targetSectorId));
                    break;
                
                case RLAction.Patrol:
                    StartCoroutine(ExecutePatrol());
                    break;
            }
        }
        
        private IEnumerator ExecuteGoTo(Vector2 target, string label)
        {
            // ? KONTROL: Target geçerli mi?
            if (target == Vector2.zero || float.IsNaN(target.x) || float.IsNaN(target.y))
            {
                Debug.LogWarning($"<color=red>[ActionPlanner] ? Invalid target for {label}! Skipping...</color>");
                isExecuting = false;
                yield break;
            }
            
            // ? REAL-TIME CHASE MODE: Player görünüyorsa sürekli pozisyonu güncelle!
            bool isRealTimeChase = label == "LastSeen" && perception && perception.PlayerVisible;
            
            if (isRealTimeChase)
            {
                Debug.Log($"<color=lime>[ActionPlanner] ?? REAL-TIME CHASE MODE ACTIVATED!</color>");
                
                // Player görünür olduðu sürece takip et!
                float chaseStartTime = Time.time;
                float maxChaseTime = 30f; // Timeout (30 saniye)
                
                while (perception.PlayerVisible && Time.time - chaseStartTime < maxChaseTime)
                {
                    // Güncel player pozisyonunu al
                    Vector2 currentPlayerPos = perception.player.position;
                    
                    // Her frame pozisyonu güncelle
                    AICharacterController.GoTo(currentPlayerPos);
                    
                    // Yakalama kontrolü (1.5m içinde)
                    float distToPlayer = Vector2.Distance(transform.position, currentPlayerPos);
                    if (distToPlayer < 1.5f)
                    {
                        Debug.Log($"<color=lime>[ActionPlanner] ? PLAYER CAUGHT! Distance: {distToPlayer:F2}m</color>");
                        break;
                    }
                    
                    // Her 0.1 saniyede pozisyon güncelle
                    yield return new WaitForSeconds(0.1f);
                }
                
                // Player kayboldu veya yakalandý
                if (perception.PlayerVisible)
                {
                    Debug.Log($"<color=lime>[ActionPlanner] ? Chase successful (caught or timeout)</color>");
                }
                else
                {
                    Debug.Log($"<color=yellow>[ActionPlanner] ?? Player lost from sight, going to last position...</color>");
                    // Son görülen pozisyona git
                    target = perception.LastSeenPos;
                    AICharacterController.GoTo(target);
                    
                    float timeout = 10f;
                    float startTime = Time.time;
                    yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime > timeout);
                }
                
                isExecuting = false;
                yield break;
            }
            
            // ? NORMAL MODE (Player görünmüyor, sadece LastSeen/LastHeard'e git)
            
            // Portal üzerinden git (sektör bilgisi varsa)
            var targetSector = sectorizer?.GetByPosition(target);
            if (targetSector != null && sectorizer != null)
            {
                Vector2 portal = sectorizer.GetNearestPortal(targetSector, transform.position);
                AICharacterController.GoTo(portal);
                
                // ? Timeout ekle (10 saniye)
                float timeout = 10f;
                float startTime = Time.time;
                yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime > timeout);
                
                if (Time.time - startTime > timeout)
                {
                    Debug.LogWarning($"[ActionPlanner] {label}: Portal timeout!");
                }
            }
            
            // Hedef pozisyona git
            AICharacterController.GoTo(target);
            
            // ? Timeout ekle (10 saniye)
            float timeout2 = 10f;
            float startTime2 = Time.time;
            yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime2 > timeout2);
            
            if (Time.time - startTime2 > timeout2)
            {
                Debug.LogWarning($"[ActionPlanner] {label}: Target timeout!");
            }
            
            if (debugMode)
                Debug.Log($"<color=lime>[ActionPlanner] ? {label} reached!</color>");
            
            isExecuting = false;
        }
        
        private IEnumerator ExecuteSweep(string sectorId)
        {
            var sector = sectorizer?.GetById(sectorId);
            if (sector == null)
            {
                Debug.LogWarning($"[ActionPlanner] Sweep: Sector {sectorId} not found!");
                isExecuting = false;
                yield break;
            }
            
            // Sweep route al (en yakýndan baþlat)
            Vector2[] sweepRoute = sectorizer.GetNearestSweepRoute(sector, transform.position);
            
            if (sweepRoute.Length == 0)
            {
                Debug.LogWarning("[ActionPlanner] Sweep: No sweep points!");
                isExecuting = false;
                yield break;
            }
            
            Debug.Log($"<color=cyan>[ActionPlanner] Sweep: {sweepRoute.Length} points in sector {sectorId}</color>");
            
            // Önce portal'a git
            Vector2 portal = sectorizer.GetNearestPortal(sector, transform.position);
            AICharacterController.GoTo(portal);
            
            // Timeout
            float timeout1 = 10f;
            float startTime1 = Time.time;
            yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime1 > timeout1);
            
            if (Time.time - startTime1 > timeout1)
            {
                Debug.LogWarning($"<color=red>[ActionPlanner] Sweep: Portal TIMEOUT!</color>");
                isExecuting = false;
                yield break;
            }
            
            // Her sweep noktasýna git
            for (int i = 0; i < Mathf.Min(3, sweepRoute.Length); i++)
            {
                Vector2 sweepPoint = sweepRoute[i];
                // ? LOG KALDIRILDI - Her sweep point spam yapýyordu
                
                AICharacterController.GoTo(sweepPoint);
                
                // Timeout
                float timeout = 10f;
                float startTime = Time.time;
                yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime > timeout);
                
                if (Time.time - startTime > timeout)
                {
                    // ? Timeout warning kaldýrýldý (normal bir durum)
                    continue;
                }
                
                // ? Sadece baþarýlý sweep'lerde log
                if (debugMode)
                    Debug.Log($"<color=lime>[ActionPlanner] Sweep {i+1}/3 OK</color>");
                
                // ? HIDING SPOT LEARNING: Player görünüyor mu?
                bool playerFound = perception && perception.PlayerVisible;
                
                // Sweep route'taki index'i bul (original sweep points'te hangi index?)
                int originalIndex = FindSweepPointIndex(sector, sweepPoint);
                
                if (originalIndex >= 0 && sector.hidingStats != null && originalIndex < sector.hidingStats.Length)
                {
                    if (playerFound)
                    {
                        sector.hidingStats[originalIndex].RecordPlayerFound();
                        Debug.Log($"<color=red>[HidingLearning] ?? Player FOUND at {sectorId}-S{originalIndex}!</color>");
                        break;  // Player bulundu, sweep bitir!
                    }
                    else
                    {
                        sector.hidingStats[originalIndex].RecordPlayerNotFound();
                        // ? Not found log kaldýrýldý (spam)
                    }
                }
                
                // Durakla
                yield return new WaitForSeconds(sweepPauseDuration);
            }
            
            if (debugMode)
                Debug.Log($"<color=lime>[ActionPlanner] ? Sweep completed!</color>");
            
            isExecuting = false;
        }
        
        /// <summary>
        /// Sweep point'in original index'ini bul
        /// </summary>
        private int FindSweepPointIndex(SectorData sector, Vector2 point)
        {
            if (sector.sweepPoints == null) return -1;
            
            for (int i = 0; i < sector.sweepPoints.Length; i++)
            {
                if (Vector2.Distance(sector.sweepPoints[i], point) < 0.1f)
                {
                    return i;
                }
            }
            
            return -1;
        }
        
        private IEnumerator ExecuteAmbush(string sectorId)
        {
            var sector = sectorizer?.GetById(sectorId);
            if (sector == null)
            {
                Debug.LogWarning($"[ActionPlanner] Ambush: Sector {sectorId} not found!");
                isExecuting = false;
                yield break;
            }
            
            // En yakýn portal'a git
            Vector2 portal = sectorizer.GetNearestPortal(sector, transform.position);
            
            Debug.Log($"<color=yellow>[ActionPlanner] Ambush: Going to portal {portal} in sector {sectorId}</color>");
            
            AICharacterController.GoTo(portal);
            
            // ? TIMEOUT EKLE!
            float timeout = 10f;
            float startTime = Time.time;
            yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime > timeout);
            
            if (Time.time - startTime > timeout)
            {
                Debug.LogWarning($"<color=red>[ActionPlanner] Ambush: Portal TIMEOUT!</color>");
                isExecuting = false;
                yield break;
            }
            
            if (debugMode)
                Debug.Log($"<color=yellow>[ActionPlanner] Ambush position reached, waiting...</color>");
            
            // Bekle
            float waitStart = Time.time;
            while (Time.time - waitStart < ambushWaitDuration)
            {
                // Player görünürse ambush baþarýlý (EnemyBrain reward verecek)
                if (perception && perception.PlayerVisible)
                {
                    if (debugMode)
                        Debug.Log($"<color=lime>[ActionPlanner] Ambush SUCCESS!</color>");
                    break;
                }
                
                yield return null;
            }
            
            if (debugMode)
                Debug.Log($"<color=lime>[ActionPlanner] ? Ambush completed!</color>");
            
            isExecuting = false;
        }
        
        private IEnumerator ExecutePatrol()
        {
            // ? KONTROL: Sectorizer var mý?
            if (sectorizer == null)
            {
                Debug.LogError("[ActionPlanner] Patrol: Sectorizer is NULL!");
                isExecuting = false;
                yield break;
            }
            
            // ? KONTROL: Yeterli sektör var mý?
            if (sectorizer.sectors == null || sectorizer.sectors.Length < patrolSectorCount)
            {
                Debug.LogWarning($"[ActionPlanner] Patrol: Not enough sectors! ({sectorizer.sectors?.Length ?? 0}/{patrolSectorCount})");
                isExecuting = false;
                yield break;
            }
            
            // ? DÝNAMÝK ROTA: HeatmapTracker'dan en sýk ziyaret edilen sektörleri al
            string[] patrolRoute = GetDynamicPatrolRoute();
            
            if (patrolRoute == null || patrolRoute.Length == 0)
            {
                Debug.LogWarning("[ActionPlanner] Patrol: No valid patrol route!");
                isExecuting = false;
                yield break;
            }
            
            Debug.Log($"<color=cyan>[ActionPlanner] ?? Dynamic patrol route (heatmap-based): {string.Join(" › ", patrolRoute)}</color>");
            
            foreach (var sectorId in patrolRoute)
            {
                var sector = sectorizer.GetById(sectorId);
                
                if (sector == null)
                {
                    // ? Warning kaldýrýldý (spam)
                    continue;
                }
                
                Vector2 anchor = sectorizer.GetNearestAnchor(sector, transform.position);
                
                // ? Her sektör log'u kaldýrýldý
                
                AICharacterController.GoTo(anchor);
                
                // ? TIMEOUT EKLE!
                float timeout = 15f;
                float startTime = Time.time;
                yield return new WaitUntil(() => AICharacterController.Arrived() || Time.time - startTime > timeout);
                
                if (Time.time - startTime > timeout)
                {
                    // ? Timeout warning kaldýrýldý
                    // Timeout olsa bile devam et
                }
                
                // Kýsa bekleme
                yield return new WaitForSeconds(0.5f);
            }
            
            if (debugMode)
                Debug.Log($"<color=lime>[ActionPlanner] ? Patrol completed!</color>");
            
            isExecuting = false;
        }
        
        /// <summary>
        /// ? YENÝ: HeatmapTracker'dan en sýk ziyaret edilen sektörleri al
        /// WITH EXPLORATION
        /// </summary>
        private string[] GetDynamicPatrolRoute()
        {
            if (!AITest.Core.HeatmapTracker.Instance)
            {
                // Fallback: Static route
                return new string[] { "A", "C", "E" };
            }
            
            // ? EXPLORATION: Random sektör seçme þansý
            if (Random.value < explorationRate)
            {
                return GetRandomPatrolRoute();
            }
            
            // Tüm sektörlerin heatmap yoðunluðunu hesapla
            System.Collections.Generic.List<(string id, float density)> sectorDensities = new();
            
            foreach (var sector in sectorizer.sectors)
            {
                if (sector == null || string.IsNullOrEmpty(sector.id)) continue;
                
                // Sektör merkezindeki heatmap yoðunluðunu al
                Vector2 center = sector.bounds.center;
                float density = AITest.Core.HeatmapTracker.Instance.GetDensityAt(center);
                
                sectorDensities.Add((sector.id, density));
                
                // ? DEBUG LOG KALDIRILDI - Her sektörü yazdýrýyordu!
            }
            
            // Yoðunluða göre sýrala (azalan)
            sectorDensities.Sort((a, b) => b.density.CompareTo(a.density));
            
            // Ýlk N sektörü seç (en sýk ziyaret edilenler)
            var topSectors = new System.Collections.Generic.List<string>();
            int count = 0;
            
            foreach (var (id, density) in sectorDensities)
            {
                // Minimum threshold kontrolü (çok az ziyaret edilmiþ sektörleri atla)
                if (density >= heatmapThreshold || count < 2) // En az 2 sektör seç
                {
                    topSectors.Add(id);
                    count++;
                    
                    if (count >= patrolSectorCount) break;
                }
            }
            
            // Eðer hiç yoðunluk yoksa (oyun baþý), static route kullan
            if (topSectors.Count == 0)
            {
                return new string[] { "A", "C", "E" };
            }
            
            // ? BONUS: %30 þans ile 1 random sektör ekle (exploration)
            if (topSectors.Count < sectorizer.sectors.Length && Random.value < 0.3f)
            {
                var unvisitedSectors = sectorizer.sectors
                    .Where(s => s != null && !topSectors.Contains(s.id))
                    .Select(s => s.id)
                    .ToList();
                
                if (unvisitedSectors.Count > 0)
                {
                    string randomSector = unvisitedSectors[Random.Range(0, unvisitedSectors.Count)];
                    topSectors.Add(randomSector);
                }
            }
            
            // Enemy'nin mevcut konumuna göre en yakýndan baþlayacak þekilde sýrala
            topSectors.Sort((a, b) =>
            {
                var sectorA = sectorizer.GetById(a);
                var sectorB = sectorizer.GetById(b);
                
                if (sectorA == null || sectorB == null) return 0;
                
                float distA = Vector2.Distance(transform.position, sectorA.bounds.center);
                float distB = Vector2.Distance(transform.position, sectorB.bounds.center);
                
                return distA.CompareTo(distB);
            });
            
            return topSectors.ToArray();
        }
        
        /// <summary>
        /// ? YENÝ: Tamamen random patrol route
        /// </summary>
        private string[] GetRandomPatrolRoute()
        {
            var availableSectors = sectorizer.sectors
                .Where(s => s != null && !string.IsNullOrEmpty(s.id))
                .OrderBy(x => Random.value) // Shuffle
                .Take(patrolSectorCount)
                .Select(s => s.id)
                .ToArray();
            
            return availableSectors;
        }
    }
}
