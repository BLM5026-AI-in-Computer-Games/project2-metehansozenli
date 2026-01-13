using UnityEngine;

namespace AITest.Player
{
    /// <summary>
    /// Player davranýþ stilini izler ve bucket'a ayýrýr
    /// ? FIXED: Doðru speed hesaplama, smooth transitions, adaptive thresholds
    /// 0: Aggressive/Fast (hýzlý + gürültülü)
    /// 1: Silent/Slow (yavaþ + sessiz)
    /// 2: Light-Seeking (ýþýkta kalma)
    /// 3: Hider/Other (diðer)
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(NoiseEmitter))]
    public class PlayerStatsMonitor : MonoBehaviour
    {
        [Header("Update Interval")]
        [Tooltip("Ýstatistik güncelleme aralýðý (saniye)")]
        public float updateInterval = 5f;
        
        [Header("Classification Thresholds")]
        [Tooltip("Aggressive speed threshold (m/s)")]
        [Range(3f, 8f)]
        public float aggressiveSpeedThreshold = 5f; // 4f ? 5f (daha gerçekçi)
        
        [Tooltip("Silent speed threshold (m/s)")]
        [Range(1f, 3f)]
        public float silentSpeedThreshold = 2.5f; // 2f ? 2.5f (normal yürüyüþ altý)
        
        [Tooltip("Aggressive noise threshold (count per 5s)")]
        [Range(2, 5)]
        public int aggressiveNoiseThreshold = 3;
        
        [Tooltip("Silent noise threshold (count per 5s)")]
        [Range(0, 2)]
        public int silentNoiseThreshold = 1;
        
        [Tooltip("Light-seeking exposure threshold (0-1)")]
        [Range(0.5f, 1f)]
        public float lightSeekingThreshold = 0.6f;
        
        [Header("Smoothing")]
        [Tooltip("Bucket deðiþim için minimum frame sayýsý (anti-flicker)")]
        [Range(1, 5)]
        public int bucketChangeFrames = 2; // ? YENÝ: Smooth transitions
        
        [Header("Light Detection")]
        [Tooltip("LightZone layer mask")]
        public LayerMask lightZoneMask;
        
        [Tooltip("Light detection radius")]
        [Range(0.3f, 2f)]
        public float lightDetectionRadius = 0.8f; // ? 0.5f ? 0.8f (daha geniþ)
        
        [Header("Debug")]
        public bool debugMode = true;
        public bool showDetailedStats = false;
        
        // Components
        private PlayerController playerController;
        private NoiseEmitter noiseEmitter;
        
        // Stats (current window)
        private float avgSpeed;
        private int noiseCount;
        private float lightExposure;
        
        // Tracking
        private Vector2 lastPosition;
        private float lastUpdateTime;
        private float timeInLight;
        private float totalDistance; // ? YENÝ: Cumulative distance tracking
        
        // Smoothing
        private int currentBucketVotes = 0; // ? YENÝ: Anti-flicker
        private int pendingBucket = 0;
        
        /// <summary>
        /// Player style bucket (0-3) with smoothing
        /// </summary>
        public int PlayerStyleBucket { get; private set; } = 0;
        
        // ? YENÝ: Public accessor for debug
        public float CurrentSpeed => avgSpeed;
        public int CurrentNoiseCount => noiseCount;
        public float CurrentLightExposure => lightExposure;
        
        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            noiseEmitter = GetComponent<NoiseEmitter>();
            
            lastPosition = transform.position;
            lastUpdateTime = Time.time;
            totalDistance = 0f;
        }
        
        private void Update()
        {
            // Iþýk kontrolü (her frame)
            CheckLightExposure();
            
            // Mesafe takibi (her frame) ? YENÝ!
            TrackDistance();
            
            // Periyodik güncelleme
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateStats();
            }
        }
        
        /// <summary>
        /// ? YENÝ: Her frame mesafe topla (daha doðru speed hesabý)
        /// </summary>
        private void TrackDistance()
        {
            Vector2 currentPos = transform.position;
            float frameDist = Vector2.Distance(currentPos, lastPosition);
            
            // Teleport check (> 10m/frame = teleport)
            if (frameDist < 10f)
            {
                totalDistance += frameDist;
            }
            
            lastPosition = currentPos;
        }
        
        private void CheckLightExposure()
        {
            // ? FIXED: Daha geniþ radius
            var hit = Physics2D.OverlapCircle(transform.position, lightDetectionRadius, lightZoneMask);
            if (hit != null)
            {
                timeInLight += Time.deltaTime;
            }
        }
        
        private void UpdateStats()
        {
            float elapsed = Time.time - lastUpdateTime;
            
            // ? FIXED: Güvenli elapsed check
            if (elapsed < 0.1f)
            {
                Debug.LogWarning("[PlayerStats] Elapsed time too short, skipping update.");
                return;
            }
            
            // 1. ? FIXED: Average speed (cumulative distance / time)
            avgSpeed = totalDistance / elapsed;
            totalDistance = 0f; // Reset for next window
            
            // 2. Noise count (snapshot BEFORE reset!)
            noiseCount = noiseEmitter.NoiseCount;
            
            // 3. Light exposure (0-1 oran)
            lightExposure = Mathf.Clamp01(timeInLight / elapsed);
            timeInLight = 0f;
            
            // 4. ? FIXED: Classify THEN reset noise
            int proposedBucket = ClassifyStyle();
            
            // ? Reset noise counter AFTER classification
            noiseEmitter.ResetNoiseCount();
            
            // 5. ? YENÝ: Smooth bucket transitions
            ApplyBucketSmoothing(proposedBucket);
            
            // Reset tracking
            lastUpdateTime = Time.time;
            
            if (debugMode)
            {
                string bucketName = GetBucketName(PlayerStyleBucket);
                Debug.Log($"<color=cyan>[PlayerStats] Speed={avgSpeed:F2} m/s | Noise={noiseCount} | Light={lightExposure:P0} ? <b>{bucketName}</b> (bucket {PlayerStyleBucket})</color>");
                
                if (showDetailedStats)
                {
                    Debug.Log($"  - Distance: {totalDistance:F1}m in {elapsed:F1}s\n" +
                             $"  - Thresholds: Aggressive={aggressiveSpeedThreshold} m/s, Silent={silentSpeedThreshold} m/s\n" +
                             $"  - Votes: {currentBucketVotes}/{bucketChangeFrames}");
                }
            }
        }
        
        /// <summary>
        /// ? IMPROVED: Better classification logic with clear priorities
        /// </summary>
        private int ClassifyStyle()
        {
            // Priority 1: Light-Seeking (overrides speed/noise)
            if (lightExposure >= lightSeekingThreshold)
            {
                return 2; // Light-Seeking
            }
            
            // Priority 2: Aggressive/Fast (high speed AND high noise)
            if (avgSpeed >= aggressiveSpeedThreshold && noiseCount >= aggressiveNoiseThreshold)
            {
                return 0; // Aggressive
            }
            
            // Priority 3: Silent/Slow (low speed AND low noise)
            if (avgSpeed <= silentSpeedThreshold && noiseCount <= silentNoiseThreshold)
            {
                return 1; // Silent
            }
            
            // Priority 4: Mixed behavior checks
            // High speed but quiet? ? Aggressive (rushing but careful)
            if (avgSpeed >= aggressiveSpeedThreshold)
            {
                return 0;
            }
            
            // Slow but noisy? ? Hider (cautious but making mistakes)
            if (avgSpeed <= silentSpeedThreshold)
            {
                return 3;
            }
            
            // Priority 5: Hider/Other (default for ambiguous behavior)
            return 3;
        }
        
        /// <summary>
        /// ? YENÝ: Smooth bucket transitions (anti-flicker)
        /// </summary>
        private void ApplyBucketSmoothing(int proposedBucket)
        {
            if (proposedBucket == PlayerStyleBucket)
            {
                // Same bucket, reset votes
                currentBucketVotes = 0;
                pendingBucket = proposedBucket;
            }
            else
            {
                // Different bucket proposed
                if (proposedBucket == pendingBucket)
                {
                    // Same pending bucket, increment votes
                    currentBucketVotes++;
                    
                    if (currentBucketVotes >= bucketChangeFrames)
                    {
                        // Enough votes, change bucket!
                        PlayerStyleBucket = proposedBucket;
                        currentBucketVotes = 0;
                        
                        if (debugMode)
                        {
                            Debug.Log($"<color=yellow>[PlayerStats] ? Bucket changed: {GetBucketName(PlayerStyleBucket)}</color>");
                        }
                    }
                }
                else
                {
                    // New pending bucket, reset votes
                    pendingBucket = proposedBucket;
                    currentBucketVotes = 1;
                }
            }
        }
        
        /// <summary>
        /// ? YENÝ: Get bucket name for debug
        /// </summary>
        private string GetBucketName(int bucket)
        {
            return bucket switch
            {
                0 => "Aggressive/Fast",
                1 => "Silent/Slow",
                2 => "Light-Seeking",
                3 => "Hider/Other",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        /// ? YENÝ: Force bucket change (for testing)
        /// </summary>
        public void ForceBucket(int bucket)
        {
            if (bucket >= 0 && bucket <= 3)
            {
                PlayerStyleBucket = bucket;
                currentBucketVotes = 0;
                Debug.Log($"<color=magenta>[PlayerStats] FORCED bucket: {GetBucketName(bucket)}</color>");
            }
        }
        
        /// <summary>
        /// ? YENÝ: Get classification reasons (for HUD display)
        /// </summary>
        public string GetClassificationReason()
        {
            return PlayerStyleBucket switch
            {
                0 => $"Speed:{avgSpeed:F1} ? {aggressiveSpeedThreshold} & Noise:{noiseCount} ? {aggressiveNoiseThreshold}",
                1 => $"Speed:{avgSpeed:F1} ? {silentSpeedThreshold} & Noise:{noiseCount} ? {silentNoiseThreshold}",
                2 => $"Light:{lightExposure:P0} ? {lightSeekingThreshold:P0}",
                3 => "Mixed/Other behavior",
                _ => "Unknown"
            };
        }
    }
}
