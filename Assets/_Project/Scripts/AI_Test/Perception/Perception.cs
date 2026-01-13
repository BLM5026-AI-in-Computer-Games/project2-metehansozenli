using UnityEngine;
using AITest.Core;
using AITest.Sector;

namespace AITest.Perception
{
    /// <summary>
    /// G�r�� (raycast fan) + Ses (noise event) alg� sistemi
    /// </summary>
    public class Perception : MonoBehaviour
    {
        [Header("Vision")]
        [Tooltip("G�r�� menzili (metre)")]
        [Range(1f, 20f)] public float viewDistance = 10f;
        
        [Tooltip("G�r�� a��s� (derece)")]
        [Range(30f, 180f)] public float viewAngle = 90f;
        
        [Tooltip("Raycast say�s�")]
        [Range(3, 15)] public int rayCount = 7;
        
        [Tooltip("Duvar layer mask")]
        public LayerMask obstructionMask;
        
        [Header("Audio")]
        [Tooltip("Ses duyma menzili (metre)")]
        [Range(1f, 30f)] public float hearingRange = 25f; // 15 ? 25 (daha geni� range!)
        
        [Tooltip("Ses event ne kadar s�re g�ncel say�l�r (saniye)")]
        [Range(1f, 10f)] public float soundMemoryTime = 20f;
        
        [Tooltip("G�rme haf�zas� s�resi (saniye) - Bu s�re sonra LastSeen s�f�rlan�r")]
        [Range(5f, 30f)] public float visionMemoryTime = 20f; // ? YEN�!
        
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
            // ? NoiseBus haz�r de�ilse bekle!
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
        /// NoiseBus haz�r olana kadar bekle
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
                // Debug log kald�r
                // Debug.Log($"<color=yellow>[Perception] Unsubscribed from NoiseBus</color>");
            }
        }
        
        private void Update()
        {
            UpdateVision();
            UpdateTimeSinceContact();
        }
        
        /// <summary>
        /// G�r�� kontrol� (raycast fan)
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
            
            // 1. Mesafe kontrol�
            if (distToPlayer > viewDistance)
            {
                PlayerVisible = false;
                return;
            }
            
            // ? 2. Forward direction: Player �NCEK� framede g�r�ld�yse ona bak, de�ilse hareket y�n�
            Vector2 forward;
            if (PlayerVisible) // �nceki framede g�r�ld� m�?
            {
                forward = toPlayer.normalized; // Player y�n�ne kitle
            }
            else
            {
                forward = GetMovementDirection(); // Hareket y�n�
            }
            
            // 3. A�� kontrol�
            float angleToPlayer = Vector2.Angle(forward, toPlayer.normalized);
            
            if (angleToPlayer > viewAngle * 0.5f)
            {
                PlayerVisible = false;
                return;
            }
            
            // ? 4. DUVAR KONTROL� (RAYCAST) - Kritik!
            // Enemy ile Player aras�nda duvar var m� kontrol et
            RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distToPlayer, obstructionMask);
            
            if (hit.collider != null)
            {
                // Hit ald�k - player m� yoksa duvar m�?
                bool hitIsPlayer = hit.transform == player || hit.transform.IsChildOf(player);
                
                if (!hitIsPlayer)
                {
                    // Duvar engeli! Player g�r�lm�yor
                    PlayerVisible = false;
                    return;
                }
            }
            
            // ? 5. HIDING POINT CHECK - Player saklan�yorsa g�r�nmesin!
            if (AITest.World.WorldRegistry.Instance != null)
            {
                bool isHiding = AITest.World.WorldRegistry.Instance.IsPlayerHiding(playerPos);
                if (isHiding)
                {
                    // Player hiding point'te saklan�yor ve d��man kontrol etmemi�!
                    PlayerVisible = false;
                    return;
                }
            }
            
            // ? T�m kontroller ge�ti - Player g�r�n�yor!
            PlayerVisible = true;
            LastSeenPos = playerPos;
            lastSeenTime = Time.time;
            
            // Sektor ID güncelle
            if (Sectorizer.Instance != null)
            {
                LastSeenSectorId = Sectorizer.Instance.GetIdByPosition(playerPos);
            }
        }
        
        /// <summary>
        /// Hareket y�n�n� al (velocity veya transform.up)
        /// </summary>
        private Vector2 GetMovementDirection()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.linearVelocity.magnitude > 0.2f)
            {
                return rb.linearVelocity.normalized;
            }
            return transform.right; // Fallback (Aligned with AICharacterController X-axis rotation)
        }
        
        /// <summary>
        /// Noise event handler
        /// </summary>
        private void OnNoiseReceived(Vector2 position, float radius, string sectorId, bool isGlobal)
        {
            Vector2 origin = transform.position;
            float dist = Vector2.Distance(origin, position);
            
            // Debug log kald�r (spam �nle)
            // Debug.Log($"<color=magenta>[Perception] ?? OnNoiseReceived! Type={(isGlobal ? "GLOBAL" : "LOCAL")} Pos={position} Dist={dist:F1} Sector={sectorId}</color>");
            
            // Global noise: Range kontrol� YOK!
            if (isGlobal)
            {
                LastHeardPos = position;
                LastHeardSectorId = sectorId;
                lastHeardTime = Time.time;
                
                Debug.Log($"<color=yellow>[Perception] ? GLOBAL noise @ {sectorId}</color>");
                return;
            }
            
            // Local noise: Range kontrol� VAR!
            if (dist <= hearingRange && dist <= radius)
            {
                LastHeardPos = position;
                LastHeardSectorId = sectorId;
                lastHeardTime = Time.time;
                
                Debug.Log($"<color=yellow>[Perception] ? LOCAL noise @ {sectorId} (dist={dist:F1})</color>");
            }
            // Ba�ar�s�z duyma log'u kald�r (spam!)
            // else {
            //     Debug.Log($"<color=red>[Perception] ? LOCAL noise too far!</color>");
            // }
        }
        
        /// <summary>
        /// Son temas zaman�n� g�ncelle
        /// </summary>
        private void UpdateTimeSinceContact()
        {
            float timeSinceSeen = Time.time - lastSeenTime;
            float timeSinceHeard = Time.time - lastHeardTime;
            
            TimeSinceContact = Mathf.Min(timeSinceSeen, timeSinceHeard);
            
            // Ses haf�zas�
            HasRecentHear = timeSinceHeard < soundMemoryTime;
            
            // ? HAFIZA TIMEOUT: 10 saniye sonra LastSeen s�f�rla!
            if (timeSinceSeen > visionMemoryTime && LastSeenSectorId != "None")
            {
                LastSeenSectorId = "None";
                LastSeenPos = Vector2.zero;
                Debug.Log($"<color=orange>[Perception] ?? Vision memory expired! LastSeen reset.</color>");
            }
            
            // ? Ses haf�zas� da timeout
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
            
            // ? Forward direction: Player g�r�n�yorsa ona, de�ilse hareket y�n�
            Vector2 forward;
            if (PlayerVisible && player != null)
            {
                forward = ((Vector2)player.position - origin).normalized;
            }
            else
            {
                forward = GetMovementDirection();
            }
            
            // G�r�� menzili �emberi
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(origin, viewDistance);
            
            // ? G�r�� konisi (raycast fan) - Duvar engelleri ile s�n�rl�!
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
                
                // ? RAYCAST (duvar kontrol�) - DUVARDA DUR!
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, viewDistance, obstructionMask);
                
                // ? Ray uzunlu�u: Hit varsa hit.distance, yoksa viewDistance
                float rayDist = hit.collider ? hit.distance : viewDistance;
                
                // ? Renk: Duvar varsa KIRMIZI (engelli), yoksa SARI (a��k)
                if (hit.collider)
                {
                    // Duvar engeli - k�rm�z� ray (K�R��ZG�S�)
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                    Gizmos.DrawRay(origin, dir * rayDist); // Sadece duvara kadar!
                    
                    // Hit point g�ster (duvar)
                    Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
                    Gizmos.DrawWireSphere(hit.point, 0.15f);
                }
                else
                {
                    // Engel yok - sar� ray (tam uzunluk)
                    Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                    Gizmos.DrawRay(origin, dir * rayDist);
                }
            }
            
            // ? Player'a �zel raycast (ye�il = g�r�n�yor, k�rm�z� = duvar engeli)
            if (player)
            {
                Vector2 toPlayer = (Vector2)player.position - origin;
                float distToPlayer = toPlayer.magnitude;
                
                if (distToPlayer <= viewDistance)
                {
                    // ? Raycast at (duvar kontrol�)
                    RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distToPlayer, obstructionMask);
                    
                    // Hit analizi
                    bool wallBlocking = false;
                    if (hit.collider != null)
                    {
                        // Hit ald�k - player m� duvar m�?
                        bool hitIsPlayer = hit.transform == player || hit.transform.IsChildOf(player);
                        wallBlocking = !hitIsPlayer;
                    }
                    
                    if (wallBlocking)
                    {
                        // ?? Duvar engeli var - KIRMIZI �izgi sadece duvara kadar!
                        Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
                        Gizmos.DrawLine(origin, hit.point);
                        
                        // Duvar hit point
                        Gizmos.color = new Color(1f, 0f, 0f, 1f);
                        Gizmos.DrawWireSphere(hit.point, 0.3f);
                        
                        // Player marker (g�r�nm�yor - gri)
                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                        Gizmos.DrawWireSphere(player.position, 0.4f);
                    }
                    else if (PlayerVisible)
                    {
                        // ? Player g�r�n�yor - YE��L �izgi
                        Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
                        Gizmos.DrawLine(origin, player.position);
                        
                        // Player marker (g�r�n�yor - ye�il)
                        Gizmos.color = new Color(0f, 1f, 0f, 1f);
                        Gizmos.DrawWireSphere(player.position, 0.5f);
                    }
                    else
                    {
                        // ?? A�� d���nda - TURUNCU noktal� �izgi
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                        
                        // Noktal� �izgi efekti (her 0.5m'de bir segment)
                        float segmentLength = 0.5f;
                        int segments = Mathf.CeilToInt(distToPlayer / segmentLength);
                        
                        for (int i = 0; i < segments; i += 2) // Her 2 segmentte 1 �iz
                        {
                            Vector2 start = origin + toPlayer.normalized * (i * segmentLength);
                            Vector2 end = origin + toPlayer.normalized * Mathf.Min((i + 1) * segmentLength, distToPlayer);
                            Gizmos.DrawLine(start, end);
                        }
                        
                        // Player marker (a�� d��� - turuncu)
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
                        Gizmos.DrawWireSphere(player.position, 0.4f);
                    }
                }
            }
            
            // Son g�r�len pozisyon (k�rm�z�)
            if (Time.time - lastSeenTime < 5f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(LastSeenPos, 0.5f);
            }
            
            // Son duyulan pozisyon (sar�)
            if (HasRecentHear)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(LastHeardPos, 0.7f);
            }
            
            // ? Forward direction ok (k�rm�z� - player g�r�n�yorsa, mavi - hareket y�n�)
            Gizmos.color = PlayerVisible ? new Color(0f, 1f, 0f, 0.8f) : new Color(0f, 0.5f, 1f, 0.6f);
            Gizmos.DrawRay(origin, forward * viewDistance * 0.5f);
        }
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Reset perception memory (called at episode start/end)
        /// </summary>
        public void ResetMemory()
        {
            LastSeenPos = Vector2.zero;
            LastSeenSectorId = "None";
            lastSeenTime = -999f;
            
            LastHeardPos = Vector2.zero;
            LastHeardSectorId = "None";
            lastHeardTime = -999f;
            
            HasRecentHear = false;
            PlayerVisible = false;
        }
        
        #endregion
    }
}
