using UnityEngine;
using AITest.Learning;
using AITest.Perception;
using AITest.Sector;
using AITest.Core;
using AITest.Utils;

namespace AITest.Enemy
{
    /// <summary>
    /// Düşman beyin sistemi - Q-Learning ile davranış optimizasyonu
    /// 
    /// RAPOR SİSTEMİ:
    /// "Düşmanın yüksek düzey davranış kararlarını yönetir. Her eylemin sonucunda aldığı
    /// ödül veya ceza üzerinden çevresine uyum sağlamayı öğrenir. Bu süreçte devriye,
    /// arama ve kovalamaca vb. gibi davranış durumları optimize edilir; böylece düşman,
    /// deneyim biriktirerek en etkili stratejileri geliştirir."
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        [Header("Components")]
        public Perception.Perception perception;
        public QLearner qLearner;
        public ActionPlanner actionPlanner;
        public SectorAgent sectorAgent;
        public Sectorizer sectorizer;
        public ThreatPerceptron threatPerceptron;
        public LightSensor lightSensor;
        public AITest.Utils.Logger logger;

        [Header("Decision")]
        public float decisionInterval = 0.5f; // Her 0.5s'de bir karar

        [Header("Behavior System (Rapor)")]
        [Tooltip("Mevcut davranış durumu (Devriye/Arama/Kovalamaca)")]
        public BehaviorState currentBehavior = BehaviorState.Patrol;
        
        [Tooltip("Davranış değişim eşikleri")]
        public float chaseTimeThreshold = 5f;    // 0-5s: Kovalamaca
        public float searchTimeThreshold = 30f;  // 5-30s: Arama
        // 30s+: Devriye

        [Header("Rewards (Davranış Optimizasyonu)")]
        public float rewardPlayerCaught = 2.0f;      // Büyük ödül!
        public float rewardClueFound = 0.3f;         // Ipucu bulma
        public float rewardCorrectBehavior = 0.1f;   // Doğru davranış seçimi
        public float penaltyWrongBehavior = -0.2f;   // Yanlış davranış
        public float penaltyIdleTime = -0.02f;       // Boşta kalma
        
        [Header("Proximity Rewards (Tiered)")]
        [Tooltip("10m içine girme bonusu (bir kez)")]
        public float proximityTier1Reward = 0.15f;   // ⚡ YENİ: Medium range
        
        [Tooltip("5m içine girme bonusu (bir kez)")]
        public float proximityTier2Reward = 0.30f;   // ⚡ YENİ: Close range
        
        [Tooltip("Proximity reward aktif mi?")]
        public bool useProximityRewards = true;      // ⚡ YENİ: Toggle

        [Header("Threat Assessment (Perceptron)")]
        [Tooltip("Tehdit değerlendirmesi aktif mi?")]
        public bool useThreatAssessment = true;
        
        [Tooltip("Tehdit skorunu reward'a uygula")]
        public bool applyThreatToReward = true;

        [Header("Learning Control")]
        public bool learningEnabled = true;

        // State tracking
        private BehaviorState lastBehavior;
        private RLState lastState;
        private float nextDecisionTime;
        private float sessionStartTime;
        private bool playerCaptured;
        private bool captureRewardGiven;
        
        // ⚡ Proximity tracking (tiered)
        private bool proximityTier1Reached = false;  // 10m içi
        private bool proximityTier2Reached = false;  // 5m içi
        private float previousMinDistance = float.MaxValue;
        private Vector2 previousPosition; // ⚡ YENİ: Mesafe farkı için
        private float previousDistanceToTarget = float.MaxValue; // ⚡ YENİ: Approach reward

        // Statistics
        public int TotalDecisions { get; private set; }
        public int SuccessfulCaptures { get; private set; }
        public float CaptureTime { get; private set; }

        private void Awake()
        {
            if (!perception) perception = GetComponent<Perception.Perception>();
            if (!qLearner) qLearner = GetComponent<QLearner>();
            if (!actionPlanner) actionPlanner = GetComponent<ActionPlanner>();
            if (!sectorAgent) sectorAgent = GetComponent<SectorAgent>();
            if (!sectorizer) sectorizer = Sectorizer.Instance;
            if (!threatPerceptron) threatPerceptron = GetComponent<ThreatPerceptron>();
            if (!lightSensor) lightSensor = GetComponent<LightSensor>();
            if (!logger) logger = GetComponent<AITest.Utils.Logger>();
            
            sessionStartTime = Time.time;
        }

        private void Start()
        {
            nextDecisionTime = Time.time + 1.0f;
            lastState = BuildState();
            lastBehavior = BehaviorState.Patrol;
            currentBehavior = BehaviorState.Patrol;
            
            playerCaptured = false;
            captureRewardGiven = false;
            TotalDecisions = 0;
            SuccessfulCaptures = 0;
            CaptureTime = 0f;
            sessionStartTime = Time.time;
            
            // ⚡ Reset proximity tracking
            proximityTier1Reached = false;
            proximityTier2Reached = false;
            previousMinDistance = float.MaxValue;
            previousPosition = transform.position; // ⚡ YENİ
            previousDistanceToTarget = float.MaxValue; // ⚡ YENİ

            if (qLearner)
            {
                qLearner.ResetLearning();
            }
            
            Debug.Log("<color=lime>[EnemyBrain] 🧠 Behavior learning system initialized (Patrol/Search/Chase)</color>");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L)) learningEnabled = !learningEnabled;
            if (Input.GetKeyDown(KeyCode.T)) useThreatAssessment = !useThreatAssessment;

            // Yakalama kontrolü
            if (!playerCaptured && perception && perception.PlayerVisible && perception.player)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, perception.player.position);
                if (distanceToPlayer < 1.5f)
                {
                    playerCaptured = true;
                    captureRewardGiven = false;
                    CaptureTime = Time.time - sessionStartTime;
                    SuccessfulCaptures++;
                    
                    Debug.Log($"<color=lime>[EnemyBrain] ★★★ PLAYER CAPTURED in {CaptureTime:F1}s! Total captures: {SuccessfulCaptures}</color>");
                    Debug.Log($"<color=lime>[EnemyBrain] 📊 Successful behavior: {currentBehavior}</color>");
                    
                    // ⚡ Train threat perceptron on successful capture
                    if (useThreatAssessment && threatPerceptron)
                    {
                        TrainThreatPerceptron(1.0f); // High threat (player was there!)
                    }
                }
            }

            // Karar döngüsü
            if (Time.time >= nextDecisionTime)
            {
                nextDecisionTime = Time.time + decisionInterval;
                DecisionLoop();
            }
        }

        /// <summary>
        /// Ana karar döngüsü - Q-Learning ile davranış seçimi
        /// </summary>
        private void DecisionLoop()
        {
            TotalDecisions++;
            
            RLState s = BuildState();
            
            // ⚡ Q-LEARNING: Davranış seçimi (3 davranış: Patrol/Search/Chase)
            BehaviorState behavior = ChooseBehavior(s);
            
            // Q-value before update
            float qBefore = GetBehaviorQValue(s, behavior);

            // Davranışı execute et
            // ⚡ FIX: Chase için özel durum - IsExecuting kontrolü yok!
            bool isUrgentChase = behavior == BehaviorState.Chase && perception && perception.PlayerVisible;
            
            if (actionPlanner && (!actionPlanner.IsExecuting || isUrgentChase))
            {
                ExecuteBehavior(behavior);
            }
            else if (isUrgentChase)
            {
                Debug.Log($"<color=yellow>[EnemyBrain] ⚠️ Chase blocked by IsExecuting! Forcing execution...</color>");
                ExecuteBehavior(behavior); // Force execute chase!
            }
            
            // Reward hesapla
            float reward = ComputeBehaviorReward(behavior);
            
            // Yeni state
            RLState s2 = BuildState();

            // ⚡ Q-LEARNING: Güncelle
            if (learningEnabled)
            {
                UpdateBehaviorQ(s, behavior, reward, s2);
            }

            // Q-value after update
            float qAfter = GetBehaviorQValue(s2, behavior);
            float tdError = qAfter - qBefore;
            
            // Log
            if (logger)
            {
                LogBehaviorDecision(s, behavior, reward, qBefore, qAfter, tdError);
            }

            // State güncelle
            lastState = s2;
            lastBehavior = behavior;
            currentBehavior = behavior;
        }

        /// <summary>
        /// State inşa et (Q-Learning için)
        /// </summary>
        private RLState BuildState()
        {
            var s = new RLState
            {
                enemySectorId = sectorizer?.GetIdByPosition(transform.position) ?? "None",
                lastSeenSectorId = perception ? perception.LastSeenSectorId : "None",
                lastHeardSectorId = perception ? perception.LastHeardSectorId : "None",
                timeSinceContactBucket = RLState.GetTimeBucket(perception ? perception.TimeSinceContact : 999f),
                playerStyleBucket = AITest.Player.PlayerStatsCache.CurrentBucket
            };
            return s;
        }

        /// <summary>
        /// ⚡ YENİ: Davranış seçimi (Q-Learning)
        /// State'e göre en iyi davranışı seç (Patrol/Search/Chase)
        /// </summary>
        private BehaviorState ChooseBehavior(RLState state)
        {
            // Time-based heuristic (başlangıç bias)
            float timeSinceContact = perception ? perception.TimeSinceContact : 999f;
            
            // ⚡ DÜZELTME: RL'den direkt behavior al (mod 3 YOK artık!)
            // QL earner action space = 3 (Patrol, Search, Chase)
            RLAction rlAction = qLearner.ChooseAction(state);
            BehaviorState behavior;
            
            // ⚡ Açık eşleme (mod yerine)
            if (rlAction == RLAction.Patrol || (int)rlAction == 4) // Patrol or explicit 4
                behavior = BehaviorState.Patrol;
            else if (rlAction == RLAction.GoToLastSeen || rlAction == RLAction.GoToLastHeard || (int)rlAction == 0 || (int)rlAction == 1)
                behavior = BehaviorState.Chase; // Chase-related actions
            else if (rlAction == RLAction.SweepNearest3 || (int)rlAction == 2)
                behavior = BehaviorState.Search;
            else
                behavior = BehaviorState.Patrol; // Fallback
            
            // ⚡ Heuristic override SADECE ilk 10 decision (50 → 10)
            if (qLearner.GetQTableSize() < 10) // İlk 10 state
            {
                if (timeSinceContact < chaseTimeThreshold)
                    behavior = BehaviorState.Chase;
                else if (timeSinceContact < searchTimeThreshold)
                    behavior = BehaviorState.Search;
                else
                    behavior = BehaviorState.Patrol;
            }
            
            Debug.Log($"<color=cyan>[EnemyBrain] 🎯 Behavior: {behavior} (timeSinceContact={timeSinceContact:F1}s, ε={qLearner.CurrentEpsilon:F2})</color>");
            
            return behavior;
        }

        /// <summary>
        /// Davranışı execute et
        /// </summary>
        private void ExecuteBehavior(BehaviorState behavior)
        {
            string targetSector = null;
            
            switch (behavior)
            {
                case BehaviorState.Chase:
                    // ⚡ REAL-TIME CHASE: Player görünüyorsa direkt pozisyonunu takip et!
                    if (perception && perception.PlayerVisible && perception.player)
                    {
                        // Player görünüyor - gerçek zamanlı takip!
                        Vector2 playerPos = perception.player.position;
                        actionPlanner.Execute(RLAction.GoToLastSeen, null); // Action başlat
                        
                        Debug.Log($"<color=yellow>[EnemyBrain] 🎯 REAL-TIME CHASE: Tracking player at {playerPos}</color>");
                    }
                    else if (perception && perception.LastSeenSectorId != "None")
                    {
                        // Player kayboldu - son görülen pozisyona git
                        targetSector = perception.LastSeenSectorId;
                        actionPlanner.Execute(RLAction.GoToLastSeen, targetSector);
                        Debug.Log($"<color=yellow>[EnemyBrain] 🏃 CHASE: Going to last seen at {targetSector}</color>");
                    }
                    else if (perception && perception.LastHeardSectorId != "None")
                    {
                        // Ses vardı - oraya git
                        targetSector = perception.LastHeardSectorId;
                        actionPlanner.Execute(RLAction.GoToLastHeard, targetSector);
                        Debug.Log($"<color=yellow>[EnemyBrain] 👂 CHASE: Going to last heard at {targetSector}</color>");
                    }
                    break;
                    
                case BehaviorState.Search:
                    // ⚡ AKILLI ARAMA: Hotspot varsa oraya git, yoksa sweep yap
                    if (perception && perception.LastSeenSectorId != "None")
                    {
                        targetSector = perception.LastSeenSectorId;
                        
                        // Hotspot kontrolü (player'ın sık durduğu yerler)
                        if (HeatmapTracker.Instance)
                        {
                            var hotspots = HeatmapTracker.Instance.GetTopHotspots(3, 0.15f);
                            
                            if (hotspots.Count > 0)
                            {
                                // Hotspot'lara git (en yoğundan başla)
                                Debug.Log($"<color=orange>[EnemyBrain] 🔍 SEARCH: {hotspots.Count} hotspots found, investigating...</color>");
                                // Not: ActionPlanner'da hotspot rotası kullanılacak
                            }
                        }
                        
                        actionPlanner.Execute(RLAction.SweepNearest3, targetSector);
                        Debug.Log($"<color=orange>[EnemyBrain] 🔍 SEARCH: Sweeping sector {targetSector}</color>");
                    }
                    else
                    {
                        // Heatmap'ten en yoğun sektörü seç
                        targetSector = GetHighestHeatmapSector();
                        
                        if (!string.IsNullOrEmpty(targetSector))
                        {
                            actionPlanner.Execute(RLAction.SweepNearest3, targetSector);
                            Debug.Log($"<color=orange>[EnemyBrain] 🔍 SEARCH: Sweeping heatmap sector {targetSector}</color>");
                        }
                    }
                    break;
                    
                case BehaviorState.Patrol:
                    // Devriye: Heatmap-based dinamik rota
                    actionPlanner.Execute(RLAction.Patrol, null);
                    Debug.Log($"<color=cyan>[EnemyBrain] 👮 PATROL: Dynamic route (heatmap-based)</color>");
                    break;
            }
        }

        /// <summary>
        /// En yoğun sektörü bul (heatmap)
        /// </summary>
        private string GetHighestHeatmapSector()
        {
            if (!HeatmapTracker.Instance || sectorizer == null || sectorizer.sectors == null)
                return "A"; // Fallback
            
            string bestSector = "A";
            float maxDensity = 0f;
            
            foreach (var sector in sectorizer.sectors)
            {
                if (sector == null) continue;
                
                float density = HeatmapTracker.Instance.GetDensityAt(sector.bounds.center);
                if (density > maxDensity)
                {
                    maxDensity = density;
                    bestSector = sector.id;
                }
            }
            
            return bestSector;
        }

        /// <summary>
        /// ⚡ Davranış reward'ı hesapla (öğrenme için)
        /// </summary>
        private float ComputeBehaviorReward(BehaviorState behavior)
        {
            float r = 0f;

            // 1. YAKALAMA ÖDÜLÜ (en büyük!)
            if (playerCaptured && !captureRewardGiven)
            {
                r += rewardPlayerCaught; // +2.0
                captureRewardGiven = true;
                Debug.Log($"<color=lime>[EnemyBrain] ★ CAPTURE REWARD: +{rewardPlayerCaught} (behavior={behavior})</color>");
            }

            // 2. IPUCU BULMA ÖDÜLÜ
            if (perception && perception.TimeSinceContact < 5f)
            {
                r += rewardClueFound; // +0.3
            }

            // 3. DAVRANIŞA UYGUN EYLEM ÖDÜLÜ
            float timeSinceContact = perception ? perception.TimeSinceContact : 999f;
            
            if (behavior == BehaviorState.Chase && timeSinceContact < chaseTimeThreshold)
            {
                r += rewardCorrectBehavior; // +0.1 (doğru zaman)
            }
            else if (behavior == BehaviorState.Search && timeSinceContact >= chaseTimeThreshold && timeSinceContact < searchTimeThreshold)
            {
                r += rewardCorrectBehavior;
            }
            else if (behavior == BehaviorState.Patrol && timeSinceContact >= searchTimeThreshold)
            {
                r += rewardCorrectBehavior;
            }
            else
            {
                r += penaltyWrongBehavior; // -0.2 (yanlış zaman)
            }

            // 4. IDLE CEZASI
            r += penaltyIdleTime * decisionInterval; // -0.02 * 0.5s
            
            // ⚡ 5. TIERED PROXIMITY REWARD (Kademeli yaklaşma bonusu)
            if (useProximityRewards && behavior == BehaviorState.Chase && perception && perception.PlayerVisible && perception.player)
            {
                float distance = Vector2.Distance(transform.position, perception.player.position);
                
                // Tier 2: 5m içi (close range) - En değerli!
                if (distance < 5f && !proximityTier2Reached)
                {
                    r += proximityTier2Reward; // +0.30 (bir kez)
                    proximityTier2Reached = true;
                    Debug.Log($"<color=yellow>[EnemyBrain] 🎯 TIER 2 PROXIMITY: +{proximityTier2Reward} (dist={distance:F1}m - CLOSE!)</color>");
                }
                // Tier 1: 10m içi (medium range)
                else if (distance < 10f && !proximityTier1Reached)
                {
                    r += proximityTier1Reward; // +0.15 (bir kez)
                    proximityTier1Reached = true;
                    Debug.Log($"<color=yellow>[EnemyBrain] 🎯 TIER 1 PROXIMITY: +{proximityTier1Reward} (dist={distance:F1}m)</color>");
                }
                
                // Monotonic check: Sadece yaklaştığında ekstra bonus
                if (distance < previousMinDistance)
                {
                    float approachBonus = 0.02f; // Küçük sürekli bonus (exploit önleme)
                    r += approachBonus;
                    previousMinDistance = distance;
                }

                // ⚡ Mesafe bazlı yoğun ödül (yakınsa daha fazla ödül)
                float distanceToPlayer = Vector2.Distance(transform.position, perception.player.position);
                if (distanceToPlayer < 3f)
                {
                    r += 0.1f; // Çok yakınsa ek ödül
                }
                else if (distanceToPlayer < 5f)
                {
                    r += 0.05f; // Yakınsa biraz ödül
                }
            }
            
            // 6. THREAT MODULATION (Perceptron output)
            if (applyThreatToReward && threatPerceptron && useThreatAssessment)
            {
                float threatScore = ComputeThreatScore();
                float multiplier = threatPerceptron.GetRewardMultiplier();
                r *= multiplier; // 0.7x - 1.5x based on threat
                
                // Train perceptron
                float targetThreat = CalculateTargetThreat();
                TrainThreatPerceptron(targetThreat);
            }

            // ⚡ Reset proximity tiers when behavior changes
            if (behavior != BehaviorState.Chase)
            {
                if (proximityTier1Reached || proximityTier2Reached)
                {
                    Debug.Log($"<color=cyan>[EnemyBrain] 🔄 Proximity tiers reset (behavior={behavior})</color>");
                }
                proximityTier1Reached = false;
                proximityTier2Reached = false;
                previousMinDistance = float.MaxValue;
            }

            return r;
        }
        
        /// <summary>
        /// Tehdit skoru hesapla (Perceptron)
        /// </summary>
        private float ComputeThreatScore()
        {
            if (!perception || !threatPerceptron) return 0.5f;

            Vector2 enemyPos = transform.position;
            Vector2 playerPos = perception.player ? (Vector2)perception.player.position : perception.LastSeenPos;
            
            float distance = Vector2.Distance(enemyPos, playerPos);
            bool los = perception.PlayerVisible;
            float lightLevel = lightSensor ? lightSensor.GetLightLevel(enemyPos) : 0.5f;
            float heatmap = HeatmapTracker.Instance ? HeatmapTracker.Instance.GetDensityAt(enemyPos) : 0.3f;
            float timeSince = perception.TimeSinceContact;
            bool recentHear = perception.HasRecentHear;
            float visScore = los ? 1.0f : 0.0f;

            return threatPerceptron.ComputeThreatScore(
                distance, los, lightLevel, heatmap, timeSince, recentHear, visScore
            );
        }

        /// <summary>
        /// Hedef tehdit skoru hesapla (training için)
        /// </summary>
        private float CalculateTargetThreat()
        {
            if (!perception) return 0.5f;

            float threat = 0f;

            // Player görünüyorsa yüksek tehdit
            if (perception.PlayerVisible)
                threat += 0.5f;

            // Player yakınsa tehdit artar
            if (perception.player)
            {
                float dist = Vector2.Distance(transform.position, perception.player.position);
                threat += Mathf.Clamp01(1f - (dist / 20f)) * 0.3f;
            }

            // Taze ipucu varsa tehdit artar
            if (perception.TimeSinceContact < 5f)
                threat += 0.2f;

            return Mathf.Clamp01(threat);
        }

        /// <summary>
        /// Threat perceptron'u eğit
        /// </summary>
        private void TrainThreatPerceptron(float targetThreat)
        {
            if (!perception || !threatPerceptron) return;

            Vector2 enemyPos = transform.position;
            Vector2 playerPos = perception.player ? (Vector2)perception.player.position : perception.LastSeenPos;
            
            float distance = Vector2.Distance(enemyPos, playerPos);
            bool los = perception.PlayerVisible;
            float lightLevel = lightSensor ? lightSensor.GetLightLevel(enemyPos) : 0.5f;
            float heatmap = HeatmapTracker.Instance ? HeatmapTracker.Instance.GetDensityAt(enemyPos) : 0.3f;
            float timeSince = perception.TimeSinceContact;
            bool recentHear = perception.HasRecentHear;
            float visScore = los ? 1.0f : 0.0f;

            threatPerceptron.Train(
                distance, los, lightLevel, heatmap, timeSince, recentHear, visScore, targetThreat
            );
        }
        
        /// <summary>
        /// Davranış Q-value'sini al
        /// </summary>
        private float GetBehaviorQValue(RLState state, BehaviorState behavior)
        {
            // Behavior'u RLAction'a map et
            RLAction action = (RLAction)((int)behavior);
            return qLearner.GetQValue(state, action);
        }

        /// <summary>
        /// Davranış Q-value'sini güncelle
        /// </summary>
        private void UpdateBehaviorQ(RLState s, BehaviorState behavior, float reward, RLState s2)
        {
            RLAction action = (RLAction)((int)behavior);
            qLearner.UpdateQ(s, action, reward, s2);
        }

        /// <summary>
        /// Davranış kararını logla
        /// </summary>
        private void LogBehaviorDecision(RLState s, BehaviorState behavior, float reward, float qBefore, float qAfter, float tdError)
        {
            // ⚡ Console log - SADECE önemli olaylar (her 20 decision'da bir)
            if (TotalDecisions % 20 == 0) // ⚡ 10 → 20 (daha az log)
            {
                Debug.Log($"<color=cyan>[EnemyBrain] 📊 #{TotalDecisions}: {behavior}, R={reward:F2}, Q={qBefore:F2}→{qAfter:F2}, ε={qLearner.CurrentEpsilon:F2}</color>");
            }
            
            // CSV logger (her decision)
            RLAction action = (RLAction)((int)behavior);
            logger?.LogDecision(s, action, reward, qBefore, qAfter, tdError);
        }
    }
}
