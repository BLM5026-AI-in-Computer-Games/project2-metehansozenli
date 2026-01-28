using UnityEngine;
using System.Collections;
using AITest.UI; 
using Metrics; 
using System.Collections.Generic;

namespace AITest.Learning
{
    /// <summary>
    /// Episode Manager - Manages Training Episodes for Q-Learning
    /// 
    /// PROFESSOR FEEDBACK: "Episode basinda player ve enemy random konumlarda olsun.
    /// Episode oyuncu yakalandiginda ya da N saniye kacabildiginde bitsin."
    /// 
    /// Episode Flow:
    /// 1. Start Episode -> Random positions
    /// 2. Run Episode -> Enemy chases player
    /// 3. End Episode -> Player caught OR timeout
    /// 4. Record Metrics -> Reward, steps, success
    /// 5. Reset -> Next episode
    /// </summary>
    public class EpisodeManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Enemy transform (will be repositioned)")]
        public Transform enemyTransform;
        
        [Tooltip("Player transform (will be repositioned)")]
        public Transform playerTransform;
        
        [Tooltip("Enemy brain (for Q-learning)")]
        public AITest.Enemy.EnemyBrain enemyBrain;
        
        [Tooltip("Enemy world model (belief state) - for resetting LastSeen on episode start")]
        public AITest.WorldModel.EnemyWorldModel worldModel;
        
        [Tooltip("Learning metrics tracker")]
        public AITest.UI.LearningMetrics metrics;      
        [Tooltip("Quest manager (for quest completion detection)")]
        public AITest.Quest.QuestManager questManager;        
        [Header("Episode Settings")]
        [Tooltip("Episode timeout (seconds) - Episode ends if player survives this long")]
        public float episodeTimeout = 30f;
        
        [Tooltip("Capture distance (meters) - Player caught if enemy gets this close")]
        public float captureDistance = 1.5f;
        
        [Tooltip("Auto-start episodes")]
        public bool autoStart = true;
        
        [Tooltip("Delay between episodes (seconds)")]
        public float episodeDelay = 1f;
        
        [Header("Spawn Area")]
        [Tooltip("Randomize positions each episode (if false, uses initial Unity positions)")]
        public bool randomizePositions = false;
        
        [Tooltip("Spawn area bounds (only used if randomizePositions = true)")]
        public Bounds spawnBounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 0f));
        
        [Tooltip("Minimum distance between enemy and player at spawn (meters)")]
        public float minSpawnDistance = 5f;
        
        [Header("Training Settings")]
        [Tooltip("Total episodes to run (0 = infinite)")]
        public int maxEpisodes = 1000;
        
        [Tooltip("Time scale during training (higher = faster)")]
        [Range(1f, 100f)] public float trainingTimeScale = 1f;
        
        [Header("Rewards")]
        [Tooltip("Reward for catching player")]
        public float captureReward = 10f; 
        
        [Tooltip("Reward for player escaping (timeout)")]
        public float escapeReward = -5f;
        
        [Tooltip("Reward per timestep (time penalty)")]
        public float timestepReward = -0.01f;
        
        [Tooltip("Reward for getting closer to player")]
        public float closeReward = 0.1f;
        
        [Tooltip("Penalty for getting farther from player")]
        public float farPenalty = -0.05f;
        
        [Header("State")]
        [Tooltip("Current episode number (readonly)")]
        public int currentEpisode = 0;
        
        [Tooltip("Episode running (readonly)")]
        public bool episodeRunning = false;
        
        [Tooltip("Episode elapsed time (readonly)")]
        public float episodeElapsedTime = 0f;
        
        [Tooltip("Total reward this episode (readonly)")]
        public float episodeTotalReward = 0f;
        
        [Tooltip("Steps this episode (readonly)")]
        public int episodeSteps = 0;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        public bool showGizmos = true;
        
        // Episode state
        private float episodeStartTime;
        private Vector2 lastPlayerPosition;
        private float lastDistanceToPlayer;
        private bool isTraining = false;
        
        // Initial positions (saved from Unity)
        private Vector3 initialEnemyPosition;
        private Vector3 initialPlayerPosition;

        private void Start()
        {
            // Save initial positions from Unity
            if (enemyTransform)
                initialEnemyPosition = enemyTransform.position;
            if (playerTransform)
                initialPlayerPosition = playerTransform.position;
            
            // Auto-find quest manager
            if (questManager == null)
            {
                questManager = AITest.Quest.QuestManager.Instance;
            }
            
            // Subscribe to quest completion
            if (questManager != null)
            {
                questManager.OnQuestCompleted.AddListener(OnQuestCompleted);
            }
            
            if (autoStart)
            {
                StartTraining();
            }
        }

        private void Update()
        {
            if (!episodeRunning) return;
            
            episodeElapsedTime = Time.time - episodeStartTime;
            
            // Episode ends ONLY on:
            // 1. Quest completion (OnQuestCompleted listener)
            // 2. Enemy capture (CheckCaptureCondition)
            CheckCaptureCondition();
            
            // Update distance-based reward
            // UpdateDistanceReward(); // ❌ DISABLE CHEATING REWARD
        }
        
        /// <summary>
        /// Called when quest is completed (player wins)
        /// </summary>
        private void OnQuestCompleted()
        {
            if (!episodeRunning) return;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=green>[Episode {currentEpisode}] QUEST COMPLETED! Player wins!</color>");
            }
            
            EndEpisode("QUEST COMPLETED (Player Wins)", false); // false = enemy failed
        }

        [Header("Experiment Settings")]
        [Tooltip("If true, use fixed seed for reproducibility")]
        public bool useRandomSeed = false;
        [Tooltip("Seed value (e.g. 42, 123, 999)")]
        public int randomSeed = 42;

        #region Training Control

        /// <summary>
        /// Start training session
        /// </summary>
        public void StartTraining()
        {
            if (isTraining)
            {
                Debug.LogWarning("[EpisodeManager] Training already running!");
                return;
            }

            // ✅ SEED INITIALIZATION
            if (useRandomSeed)
            {
                UnityEngine.Random.InitState(randomSeed);
                Debug.Log($"<color=magenta>[EXP] Initialized Random Seed: {randomSeed}</color>");
            }
            else
            {
                // Capture the random seed used (for logging)
                randomSeed = (int)System.DateTime.Now.Ticks;
                // Don't re-init random here, just use whatever Unity has, but record it.
            }
            
            isTraining = true;
            currentEpisode = 0;
            
            // Set time scale
            Time.timeScale = trainingTimeScale;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=lime>[EpisodeManager] Training started! Target: {maxEpisodes} episodes, TimeScale: {trainingTimeScale}x</color>");
            }
            
            // Start first episode
            StartCoroutine(RunEpisodes());
        }

        /// <summary>
        /// Stop training session
        /// </summary>
        public void StopTraining()
        {
            if (!isTraining)
            {
                Debug.LogWarning("[EpisodeManager] Training not running!");
                return;
            }
            
            isTraining = false;
            episodeRunning = false;
            
            // Reset time scale
            Time.timeScale = 1f;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=yellow>[EpisodeManager] Training stopped at episode {currentEpisode}</color>");
            }
            
            // Save Q-table
            if (enemyBrain && enemyBrain.qLearningPolicy != null)
            {
                enemyBrain.qLearningPolicy.SaveQTable($"qtable_simple_{currentEpisode}");
            }
        }

        /// <summary>
        /// Run episodes coroutine
        /// </summary>
        private IEnumerator RunEpisodes()
        {
            while (isTraining && (maxEpisodes == 0 || currentEpisode < maxEpisodes))
            {
                currentEpisode++;
                
                StartEpisode();
                
                // Wait for episode to finish
                while (episodeRunning)
                {
                    yield return null;
                }
                
                // Delay between episodes
                yield return new WaitForSeconds(episodeDelay);
            }
            
            // Training complete
            if (showDebugLogs)
            {
                Debug.Log($"<color=lime>[EpisodeManager] Training COMPLETE! {currentEpisode} episodes finished.</color>");
            }
            
            StopTraining();
            
            // ✅ AUTO-QUIT application/playmode after training
            Debug.Log("<color=red>[EpisodeManager] Auto-quitting application...</color>");
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        #endregion

        #region Episode Lifecycle

        /// <summary>
        /// Start new episode
        /// </summary>
        public void StartEpisode()
        {
            episodeRunning = true;
            episodeStartTime = Time.time;
            episodeElapsedTime = 0f;
            episodeTotalReward = 0f;
            episodeSteps = 0;
            
            // Reset quest for new episode
            if (questManager != null)
            {
                questManager.ResetForTrainingEpisode(restartQuest: true);
            }

            // ✅ Reset Heat Graph (Avoid persistence across episodes)
            if (AITest.Heat.TransitionHeatGraph.Instance)
            {
                AITest.Heat.TransitionHeatGraph.Instance.ClearAllHeat();
            }
            
            // Randomize positions (optional)
            if (randomizePositions)
            {
                RandomizePositions();
            }
            else
            {
                // Reset to initial positions
                ResetToInitialPositions();
            }
            
            // Reset velocities
            ResetVelocities();
            
            // ✅ Reset Perception memory at episode start (clear old hearing/seeing events)
            if (enemyBrain)
            {
                var perception = enemyBrain.GetComponent<AITest.Perception.Perception>();
                if (perception)
                {
                    perception.ResetMemory();
                }
            }
            
            // ✅ Notify MetricsManager of episode start
            // Use the global experiment seed for tracking, or a combined hash
            // ? RESET PLAYER BOT
            var playerBot = playerTransform.GetComponent<PlayerTrainingBotController>();
            // Also try GetComponent in root just in case
            if (playerBot == null) playerBot = playerTransform.GetComponent<PlayerTrainingBotController>();

            if (playerBot != null)
            {
                // Deterministic seed per episode
                playerBot.ResetForNewEpisode(randomSeed + currentEpisode);
            }
            
            MetricsHooks.EpisodeStart(randomSeed, "default", EpisodeMode.TRAIN, 
                enemyBrain != null && enemyBrain.qLearningPolicy != null ? enemyBrain.qLearningPolicy.epsilon : 0.1f);
            
            // Reset enemy state
            if (enemyBrain)
            {
                // Initialize first state
                lastPlayerPosition = playerTransform.position;
                lastDistanceToPlayer = Vector2.Distance(enemyTransform.position, playerTransform.position);
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[Episode {currentEpisode}] START - Enemy: {enemyTransform.position}, Player: {playerTransform.position}</color>");
            }
        }

        /// <summary>
        /// Reset to initial positions saved from Unity
        /// </summary>
        private void ResetToInitialPositions()
        {
            if (enemyTransform) enemyTransform.position = initialEnemyPosition;
            if (playerTransform) playerTransform.position = initialPlayerPosition;
        }

        /// <summary>
        /// Randomize enemy and player positions (Room-based priority)
        /// </summary>
        private void RandomizePositions()
        {
            bool spawnedInRooms = false;

            // Try to spawn in distinct rooms first (Filter out Junctions)
            if (AITest.World.WorldRegistry.Instance)
            {
                var allRooms = AITest.World.WorldRegistry.Instance.GetAllRooms();
                
                // Filter: Only non-junction rooms
                var validRooms = new List<AITest.World.RoomZone>();
                if (allRooms != null)
                {
                    foreach (var room in allRooms)
                    {
                        if (room != null && !room.isJunction)
                            validRooms.Add(room);
                    }
                }

                // Fallback: If not enough non-junction rooms, use ALL rooms
                if (validRooms.Count < 2 && allRooms != null && allRooms.Count >= 2)
                {
                    if (showDebugLogs) Debug.LogWarning("[EpisodeManager] Not enough non-junction rooms! Using all rooms.");
                    validRooms = new List<AITest.World.RoomZone>(allRooms);
                }

                if (validRooms.Count >= 2)
                {
                    // Pick random room for Enemy
                    int enemyRoomIndex = Random.Range(0, validRooms.Count);
                    var enemyRoom = validRooms[enemyRoomIndex];

                    // Pick DISTINCT room for Player
                    int playerRoomIndex;
                    do
                    {
                        playerRoomIndex = Random.Range(0, validRooms.Count);
                    } while (playerRoomIndex == enemyRoomIndex);
                    
                    var playerRoom = validRooms[playerRoomIndex];

                    if (enemyTransform) enemyTransform.position = enemyRoom.Center;
                    if (playerTransform) playerTransform.position = playerRoom.Center;
                    
                    spawnedInRooms = true;
                    
                    if (showDebugLogs)
                        Debug.Log($"<color=cyan>[EpisodeManager] Spawned in Rooms: Enemy in {enemyRoom.roomName}, Player in {playerRoom.roomName}</color>");
                }
            }

            // Fallback to Bounds-based spawning if rooms failed
            if (!spawnedInRooms)
            {
                Vector2 enemyPos, playerPos;
                int attempts = 0;
                const int maxAttempts = 100;
                
                do
                {
                    // Random position within bounds
                    enemyPos = new Vector2(
                        Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                        Random.Range(spawnBounds.min.y, spawnBounds.max.y)
                    );
                    
                    playerPos = new Vector2(
                        Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                        Random.Range(spawnBounds.min.y, spawnBounds.max.y)
                    );
                    
                    attempts++;
                    
                    // Check minimum distance
                    if (Vector2.Distance(enemyPos, playerPos) >= minSpawnDistance)
                    {
                        break;
                    }
                }
                while (attempts < maxAttempts);
                
                // Apply positions
                if (enemyTransform) enemyTransform.position = enemyPos;
                if (playerTransform) playerTransform.position = playerPos;
            }
        }

        /// <summary>
        /// Reset velocities and rotations
        /// </summary>
        private void ResetVelocities()
        {
            // Stop enemy velocity and rotation
            var enemyRb = enemyTransform.GetComponent<Rigidbody2D>();
            if (enemyRb)
            {
                enemyRb.linearVelocity = Vector2.zero;
                enemyRb.angularVelocity = 0f;
            }
            enemyTransform.rotation = Quaternion.identity; // Reset rotation
            
            // ✅ Reset Enemy Mover (Fix 'flying' issue)
            var enemyMover = enemyTransform.GetComponent<AITest.Enemy.AIAgentMover>();
            if (enemyMover) enemyMover.ClearDestination();

            // Stop player velocity and rotation
            var playerRb = playerTransform.GetComponent<Rigidbody2D>();
            if (playerRb)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
            }
            playerTransform.rotation = Quaternion.identity; // Reset rotation
            
            // ✅ Reset Player Mover (if exists)
            var playerMover = playerTransform.GetComponent<AITest.Enemy.AIAgentMover>();
            if (playerMover) playerMover.ClearDestination();
        }

        /// <summary>
        /// End episode
        /// </summary>
        /// <param name="reason">Reason for ending (Capture/Timeout)</param>
        /// <param name="success">Did enemy succeed?</param>
        public void EndEpisode(string reason, bool success)
        {
            if (!episodeRunning) return;
            
            episodeRunning = false;
            
            // Calculate final reward
            float finalReward = success ? captureReward : escapeReward;
            episodeTotalReward += finalReward;
            
            // Perform final Q-update
            if (enemyBrain && enemyBrain.qLearningPolicy != null)
            {
                // Get final state
                int finalStateKey = GetCurrentStateKey();
                
                // Update Q with terminal reward
                // (using last action and final reward)
                if (enemyBrain.currentMode != AITest.Enemy.EnemyMode.Patrol)
                {
                    enemyBrain.qLearningPolicy.UpdateQ(
                        enemyBrain.lastStateKey,
                        enemyBrain.currentMode,
                        finalReward,
                        finalStateKey,
                        episodeElapsedTime
                    );
                }
                
                // Call EndEpisode (decay epsilon, reset)
                enemyBrain.EndEpisode();
            }
            
            // Record metrics
            if (metrics)
            {
                metrics.RecordEpisode(
                    currentEpisode,
                    episodeTotalReward,
                    episodeElapsedTime,
                    episodeSteps,
                    success
                );
            }
            
            // ✅ Notify MetricsManager of episode end
            TerminationReason termReason = success ? TerminationReason.CAPTURE : 
                (reason.Contains("TIMEOUT") ? TerminationReason.TIMEOUT : TerminationReason.QUEST_COMPLETE);
            MetricsHooks.EpisodeEnd(termReason);
            
            // ✅ Clear enemy's LastSeen memory immediately (so next episode doesn't show old position)
            if (worldModel)
            {
                worldModel.ResetBelief();
            }
            
            // ✅ Also reset Perception memory (LastSeenPos, LastHeardPos in Perception component)
            if (enemyBrain)
            {
                var perception = enemyBrain.GetComponent<AITest.Perception.Perception>();
                if (perception)
                {
                    perception.ResetMemory();
                }
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=lime>[Episode {currentEpisode}] END - {reason} | Reward: {episodeTotalReward:F2} | Time: {episodeElapsedTime:F2}s | Success: {success}</color>");
            }
            
            // Log stats every 10 episodes
            if (currentEpisode % 10 == 0 && enemyBrain && enemyBrain.qLearningPolicy != null)
            {
                Debug.Log($"<color=yellow>[Episode {currentEpisode}] {enemyBrain.qLearningPolicy.GetStatsSummary()}</color>");
            }
            
            // ✅ LOG METRICS TO CSV
            LogEpisodeMetrics(reason, success);
        }

        /// <summary>
        /// Log Episode Metrics to CSV for Graphs
        /// </summary>
        private void LogEpisodeMetrics(string result, bool success)
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "../TrainingData");
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "episode_metrics.csv");

            try
            {
                if (!System.IO.File.Exists(path))
                    System.IO.File.WriteAllText(path, "Episode,Seed,Duration,Steps,Reward,Result,Success\n");

                string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2:F2},{3},{4:F2},{5},{6}\n",
                    currentEpisode, randomSeed, episodeElapsedTime, episodeSteps, episodeTotalReward, result, success ? 1 : 0);

                System.IO.File.AppendAllText(path, line);
            }
            catch(System.Exception e)
            {
                Debug.LogWarning($"[EpisodeManager] Failed to log metrics: {e.Message}");
            }
        }

        #endregion

        #region Episode Conditions

        /// <summary>
        /// Check if enemy captured player
        /// </summary>
        /// <summary>
        /// Check if enemy captured player
        /// </summary>
        private void CheckCaptureCondition()
        {
            if (!enemyTransform || !playerTransform) return;
            
            // ? FIX: Don't capture if player is hiding (SpriteRenderer disabled)
            // The 'HideSpotCheckOption' will handle capturing hidden players by forcing them out first.
            var playerRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();
            if (playerRenderer != null && !playerRenderer.enabled)
            {
                return; // Player is hidden, safe from proximity capture
            }

            float distance = Vector2.Distance(enemyTransform.position, playerTransform.position);
            
            if (distance < captureDistance)
            {
                EndEpisode("PLAYER CAPTURED", success: true);
            }
        }

        /// <summary>
        /// Update distance-based reward (incremental)
        /// </summary>
        private void UpdateDistanceReward()
        {
            if (!enemyTransform || !playerTransform) return;
            
            float currentDistance = Vector2.Distance(enemyTransform.position, playerTransform.position);
            
            // Check if got closer or farther
            if (currentDistance < lastDistanceToPlayer)
            {
                // Got closer -> small positive reward
                float reward = closeReward;
                episodeTotalReward += reward;
            }
            else if (currentDistance > lastDistanceToPlayer)
            {
                // Got farther -> small penalty
                float reward = farPenalty;
                episodeTotalReward += reward;
            }
            
            // Timestep penalty
            episodeTotalReward += timestepReward;
            
            // Update last distance
            lastDistanceToPlayer = currentDistance;
            episodeSteps++;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get current state key (for Q-table)
        /// </summary>
        private int GetCurrentStateKey()
        {
            if (enemyBrain && enemyBrain.simpleStateExtractor)
            {
                var state = enemyBrain.simpleStateExtractor.ExtractState();
                return state.GetHashKey();
            }
            
            return 0;
        }

        /// <summary>
        /// Manually start episode (for testing)
        /// </summary>
        [ContextMenu("Start Episode")]
        public void ManualStartEpisode()
        {
            currentEpisode++;
            StartEpisode();
        }

        /// <summary>
        /// Manually end episode (for testing)
        /// </summary>
        [ContextMenu("End Episode (Success)")]
        public void ManualEndEpisodeSuccess()
        {
            EndEpisode("MANUAL END", success: true);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // Draw spawn bounds
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnBounds.center, spawnBounds.size);
            
            // Draw capture radius
            if (episodeRunning && enemyTransform)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(enemyTransform.position, captureDistance);
            }
        }

        #endregion
    }
}
