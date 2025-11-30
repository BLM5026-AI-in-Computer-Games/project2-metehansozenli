using UnityEngine;
using AITest.Core;
using AITest.Sector;

namespace AITest.Perception
{
    /// <summary>
    /// Görüþ (raycast fan) + Ses (noise event) algý sistemi
    /// </summary>
    public class Perception : MonoBehaviour
    {
        [Header("Vision")]
        [Tooltip("Görüþ menzili (metre)")]
        [Range(1f, 20f)] public float viewDistance = 10f;
        
        [Tooltip("Görüþ açýsý (derece)")]
        [Range(30f, 180f)] public float viewAngle = 90f;
        
        [Tooltip("Raycast sayýsý")]
        [Range(3, 15)] public int rayCount = 7;
        
        [Tooltip("Duvar layer mask")]
        public LayerMask obstructionMask;
        
        [Header("Audio")]
        [Tooltip("Ses duyma menzili (metre)")]
        [Range(1f, 30f)] public float hearingRange = 25f; // 15 ? 25 (daha geniþ range!)
        
        [Tooltip("Ses event ne kadar süre güncel sayýlýr (saniye)")]
        [Range(1f, 10f)] public float soundMemoryTime = 20f;
        
        [Tooltip("Görme hafýzasý süresi (saniye) - Bu süre sonra LastSeen sýfýrlanýr")]
        [Range(5f, 30f)] public float visionMemoryTime = 20f; // ? YENÝ!
        
        [Header("References")]
        public Transform player;
        
        [Header("Debug")]
        public bool drawGizmos = true;
        
        // Public outputs
        public bool PlayerVisible { get; private set; }
        public Vector2 LastSeenPos { get; private set; }
        public string LastSeenSectorId { get; private set; } = "None";
        
        public bool HasRecentHear { get; private set; }
        public Vector2 LastHeardPos { get; private set; }
        public string LastHeardSectorId { get; private set; } = "None";
        public float TimeSinceContact { get; private set; }
        
        private float lastSeenTime = -999f;
        private float lastHeardTime = -999f;
        
        private void Awake()
        {
            // Auto-find player
            if (!player)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj) player = playerObj.transform;
            }
        }
        
        private void OnEnable()
        {
            // ? NoiseBus hazýr deðilse bekle!
            if (NoiseBus.Instance == null)
            {
                Debug.LogWarning("[Perception] NoiseBus not ready yet, delaying subscription...");
                StartCoroutine(WaitForNoiseBus());
                return;
            }
            
            // Noise bus subscribe
            NoiseBus.Instance.OnNoise += OnNoiseReceived;
        }
        
        /// <summary>
        /// NoiseBus hazýr olana kadar bekle
        /// </summary>
        private System.Collections.IEnumerator WaitForNoiseBus()
        {
            while (NoiseBus.Instance == null)
            {
                yield return null;
            }
            
            NoiseBus.Instance.OnNoise += OnNoiseReceived;
            Debug.Log($"<color=lime>[Perception] ? Subscribed to NoiseBus (delayed)</color>");
        }
        
        private void OnDisable()
        {
            // Noise bus unsubscribe
            if (NoiseBus.Instance != null)
            {
                NoiseBus.Instance.OnNoise -= OnNoiseReceived;
                // Debug log kaldýr
                // Debug.Log($"<color=yellow>[Perception] Unsubscribed from NoiseBus</color>");
            }
        }
        
        private void Update()
        {
            UpdateVision();
            UpdateTimeSinceContact();
        }
        
        /// <summary>
        /// Görüþ kontrolü (raycast fan)
        /// </summary>
        private void UpdateVision()
        {
            if (!player)
            {
                PlayerVisible = false;
                return;
            }
            
            Vector2 origin = transform.position;
            Vector2 playerPos = player.position;
            Vector2 toPlayer = playerPos - origin;
            float distToPlayer = toPlayer.magnitude;
            
            // 1. Mesafe kontrolü
            if (distToPlayer > viewDistance)
            {
                PlayerVisible = false;
                return;
            }
            
            // ? 2. Forward direction: Player ÖNCEKÝ framede görüldüyse ona bak, deðilse hareket yönü
            Vector2 forward;
            if (PlayerVisible) // Önceki framede görüldü mü?
            {
                forward = toPlayer.normalized; // Player yönüne kitle
            }
            else
            {
                forward = GetMovementDirection(); // Hareket yönü
            }
            
            // 3. Açý kontrolü
            float angleToPlayer = Vector2.Angle(forward, toPlayer.normalized);
            
            if (angleToPlayer > viewAngle * 0.5f)
            {
                PlayerVisible = false;
                return;
            }
            
            // ? 4. DUVAR KONTROLÜ (RAYCAST) - Kritik!
            // Enemy ile Player arasýnda duvar var mý kontrol et
            RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distToPlayer, obstructionMask);
            
            if (hit.collider != null)
            {
                // Hit aldýk - player mý yoksa duvar mý?
                bool hitIsPlayer = hit.transform == player || hit.transform.IsChildOf(player);
                
                if (!hitIsPlayer)
                {
                    // Duvar engeli! Player görülmüyor
                    PlayerVisible = false;
                    return;
                }
            }
            
            // ? Tüm kontroller geçti - Player görünüyor!
            PlayerVisible = true;
            LastSeenPos = playerPos;
            lastSeenTime = Time.time;
            
            // Sektör ID güncelle
            if (Sectorizer.Instance != null)
            {
                LastSeenSectorId = Sectorizer.Instance.GetIdByPosition(playerPos);
            }
        }
        
        /// <summary>
        /// Hareket yönünü al (velocity veya transform.up)
        /// </summary>
        private Vector2 GetMovementDirection()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.linearVelocity.magnitude > 0.2f)
            {
                return rb.linearVelocity.normalized;
            }
            return transform.up; // Fallback
        }
        
        /// <summary>
        /// Noise event handler
        /// </summary>
        private void OnNoiseReceived(Vector2 position, float radius, string sectorId, bool isGlobal)
        {
            Vector2 origin = transform.position;
            float dist = Vector2.Distance(origin, position);
            
            // Debug log kaldýr (spam önle)
            // Debug.Log($"<color=magenta>[Perception] ?? OnNoiseReceived! Type={(isGlobal ? "GLOBAL" : "LOCAL")} Pos={position} Dist={dist:F1} Sector={sectorId}</color>");
            
            // Global noise: Range kontrolü YOK!
            if (isGlobal)
            {
                LastHeardPos = position;
                LastHeardSectorId = sectorId;
                lastHeardTime = Time.time;
                
                Debug.Log($"<color=yellow>[Perception] ? GLOBAL noise @ {sectorId}</color>");
                return;
            }
            
            // Local noise: Range kontrolü VAR!
            if (dist <= hearingRange && dist <= radius)
            {
                LastHeardPos = position;
                LastHeardSectorId = sectorId;
                lastHeardTime = Time.time;
                
                Debug.Log($"<color=yellow>[Perception] ? LOCAL noise @ {sectorId} (dist={dist:F1})</color>");
            }
            // Baþarýsýz duyma log'u kaldýr (spam!)
            // else {
            //     Debug.Log($"<color=red>[Perception] ? LOCAL noise too far!</color>");
            // }
        }
        
        /// <summary>
        /// Son temas zamanýný güncelle
        /// </summary>
        private void UpdateTimeSinceContact()
        {
            float timeSinceSeen = Time.time - lastSeenTime;
            float timeSinceHeard = Time.time - lastHeardTime;
            
            TimeSinceContact = Mathf.Min(timeSinceSeen, timeSinceHeard);
            
            // Ses hafýzasý
            HasRecentHear = timeSinceHeard < soundMemoryTime;
            
            // ? HAFIZA TIMEOUT: 10 saniye sonra LastSeen sýfýrla!
            if (timeSinceSeen > visionMemoryTime && LastSeenSectorId != "None")
            {
                LastSeenSectorId = "None";
                LastSeenPos = Vector2.zero;
                Debug.Log($"<color=orange>[Perception] ?? Vision memory expired! LastSeen reset.</color>");
            }
            
            // ? Ses hafýzasý da timeout
            if (timeSinceHeard > soundMemoryTime && LastHeardSectorId != "None")
            {
                LastHeardSectorId = "None";
                LastHeardPos = Vector2.zero;
                Debug.Log($"<color=orange>[Perception] ?? Sound memory expired! LastHeard reset.</color>");
            }
        }
        
        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            
            Vector2 origin = transform.position;
            
            // ? Forward direction: Player görünüyorsa ona, deðilse hareket yönü
            Vector2 forward;
            if (PlayerVisible && player != null)
            {
                forward = ((Vector2)player.position - origin).normalized;
            }
            else
            {
                forward = GetMovementDirection();
            }
            
            // Görüþ menzili çemberi
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(origin, viewDistance);
            
            // ? Görüþ konisi (raycast fan) - Duvar engelleri ile sýnýrlý!
            float halfAngle = viewAngle * 0.5f;
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - halfAngle;
            float stepAngle = viewAngle / Mathf.Max(1, rayCount - 1);
            
            for (int i = 0; i < rayCount; i++)
            {
                float angle = baseAngle + stepAngle * i;
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );
                
                // ? RAYCAST (duvar kontrolü) - DUVARDA DUR!
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, viewDistance, obstructionMask);
                
                // ? Ray uzunluðu: Hit varsa hit.distance, yoksa viewDistance
                float rayDist = hit.collider ? hit.distance : viewDistance;
                
                // ? Renk: Duvar varsa KIRMIZI (engelli), yoksa SARI (açýk)
                if (hit.collider)
                {
                    // Duvar engeli - kýrmýzý ray (KÜRÇÝZGÝSÝ)
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                    Gizmos.DrawRay(origin, dir * rayDist); // Sadece duvara kadar!
                    
                    // Hit point göster (duvar)
                    Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
                    Gizmos.DrawWireSphere(hit.point, 0.15f);
                }
                else
                {
                    // Engel yok - sarý ray (tam uzunluk)
                    Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                    Gizmos.DrawRay(origin, dir * rayDist);
                }
            }
            
            // ? Player'a özel raycast (yeþil = görünüyor, kýrmýzý = duvar engeli)
            if (player)
            {
                Vector2 toPlayer = (Vector2)player.position - origin;
                float distToPlayer = toPlayer.magnitude;
                
                if (distToPlayer <= viewDistance)
                {
                    // ? Raycast at (duvar kontrolü)
                    RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distToPlayer, obstructionMask);
                    
                    // Hit analizi
                    bool wallBlocking = false;
                    if (hit.collider != null)
                    {
                        // Hit aldýk - player mý duvar mý?
                        bool hitIsPlayer = hit.transform == player || hit.transform.IsChildOf(player);
                        wallBlocking = !hitIsPlayer;
                    }
                    
                    if (wallBlocking)
                    {
                        // ?? Duvar engeli var - KIRMIZI çizgi sadece duvara kadar!
                        Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
                        Gizmos.DrawLine(origin, hit.point);
                        
                        // Duvar hit point
                        Gizmos.color = new Color(1f, 0f, 0f, 1f);
                        Gizmos.DrawWireSphere(hit.point, 0.3f);
                        
                        // Player marker (görünmüyor - gri)
                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                        Gizmos.DrawWireSphere(player.position, 0.4f);
                    }
                    else if (PlayerVisible)
                    {
                        // ? Player görünüyor - YEÞÝL çizgi
                        Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
                        Gizmos.DrawLine(origin, player.position);
                        
                        // Player marker (görünüyor - yeþil)
                        Gizmos.color = new Color(0f, 1f, 0f, 1f);
                        Gizmos.DrawWireSphere(player.position, 0.5f);
                    }
                    else
                    {
                        // ?? Açý dýþýnda - TURUNCU noktalý çizgi
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                        
                        // Noktalý çizgi efekti (her 0.5m'de bir segment)
                        float segmentLength = 0.5f;
                        int segments = Mathf.CeilToInt(distToPlayer / segmentLength);
                        
                        for (int i = 0; i < segments; i += 2) // Her 2 segmentte 1 çiz
                        {
                            Vector2 start = origin + toPlayer.normalized * (i * segmentLength);
                            Vector2 end = origin + toPlayer.normalized * Mathf.Min((i + 1) * segmentLength, distToPlayer);
                            Gizmos.DrawLine(start, end);
                        }
                        
                        // Player marker (açý dýþý - turuncu)
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
                        Gizmos.DrawWireSphere(player.position, 0.4f);
                    }
                }
            }
            
            // Son görülen pozisyon (kýrmýzý)
            if (Time.time - lastSeenTime < 5f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(LastSeenPos, 0.5f);
            }
            
            // Son duyulan pozisyon (sarý)
            if (HasRecentHear)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(LastHeardPos, 0.7f);
            }
            
            // ? Forward direction ok (kýrmýzý - player görünüyorsa, mavi - hareket yönü)
            Gizmos.color = PlayerVisible ? new Color(0f, 1f, 0f, 0.8f) : new Color(0f, 0.5f, 1f, 0.6f);
            Gizmos.DrawRay(origin, forward * viewDistance * 0.5f);
        }
        #endregion
    }
}
