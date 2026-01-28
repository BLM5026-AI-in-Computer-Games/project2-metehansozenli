using UnityEngine;
using AITest.Learning;
using System.Collections.Generic; // ? PROMPT 16: For List<EnemyMode>
using System.Linq; // For Max() on arrays
using Metrics; // ✅ NEW: Metrics system

namespace AITest.Enemy
{
    /// <summary>
    /// Enemy Brain - Q-Learning decision controller
    /// 
    /// PROMPT 10: SMDP Training Loop
    /// - Maintains current option
    /// - Extracts state ? chooses action ? executes option
    /// - On completion: compute reward ? update Q ? repeat
    /// - Handles interrupts (SeePlayer, HearNoise)
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        [Header("Components")]
        public EnemyContext context;
        public StateExtractor stateExtractor;
        public SimpleStateExtractor simpleStateExtractor; // ✅ NEW: Simple MDP
        public QLearningPolicy qLearningPolicy; // ✅ Renamed from qLearning
        public RewardCalculator rewardCalculator;
        public ChaseExecutor chaseExecutor; // ? PROMPT 14: Chase system
        public ActionMasker actionMasker; // ? PROMPT 16: Action masking

        [Header("Options (7 total - AmbushOption removed)")]
        public PatrolOption patrolOption;
        public InvestigateOption investigateOption;
        public HeatSearchOption heatSearchOption;
        public SweepOption sweepOption;
        public HideSpotCheckOption hideSpotCheckOption;
        public HeatSweepOption heatSweepOption;
        public AmbushHotChokeOption ambushHotChokeOption;

        [Header("Learning Settings")]
        [Tooltip("Enable Q-learning (false = heuristic only)")]
        public bool enableLearning = true;

        [Tooltip("Use simple state extractor (486 states) instead of full (497k states)")]
        public bool useSimpleStateExtractor = true; // 

        [Tooltip("Explore during execution (?-greedy)")]
        public bool explore = true;

        [Tooltip("Auto-save Q-table every N episodes")]
        [Range(0, 100)] public int autoSaveInterval = 10;

        [Header("Debug")]
        public bool showDebugLogs = true;

        // State
        private IEnemyOption currentOption;
        private RLStateKey lastState; // For full state
        public int lastStateKey; 
        public EnemyMode currentMode; 
        private EnemyMode lastAction;
        private float optionStartTime;

        private void OnEnable()
        {
            // ⚡ Force component to stay enabled
            if (!this.enabled)
            {
                this.enabled = true;
                Debug.LogWarning("[EnemyBrain] Component was disabled! Force enabling...");
            }
        }

        private void Awake()
        {
            // ⚡ Force enable (safety check)
            this.enabled = true;

            // Auto-find components
            if (!context) context = GetComponent<EnemyContext>();
            if (!stateExtractor) stateExtractor = GetComponent<StateExtractor>();
            if (!simpleStateExtractor) simpleStateExtractor = GetComponent<SimpleStateExtractor>(); // ✅ NEW
            if (!rewardCalculator) rewardCalculator = GetComponent<RewardCalculator>();
            if (!chaseExecutor) chaseExecutor = GetComponent<ChaseExecutor>(); // ⚡ PROMPT 14
            if (!actionMasker) actionMasker = GetComponent<ActionMasker>(); // ⚡ PROMPT 16

            // Auto-find options
            if (!patrolOption) patrolOption = GetComponent<PatrolOption>();
            if (!investigateOption) investigateOption = GetComponent<InvestigateOption>();
            if (!heatSearchOption) heatSearchOption = GetComponent<HeatSearchOption>();
            if (!sweepOption) sweepOption = GetComponent<SweepOption>();
            if (!hideSpotCheckOption) hideSpotCheckOption = GetComponent<HideSpotCheckOption>();
            // if (!ambushOption) ambushOption = GetComponent<AmbushOption>(); // ❌ REMOVED
            if (!heatSweepOption) heatSweepOption = GetComponent<HeatSweepOption>(); // ✅ NEW
            if (!ambushHotChokeOption) ambushHotChokeOption = GetComponent<AmbushHotChokeOption>(); // ✅ NEW

            // Initialize Q-learning
            if (qLearningPolicy == null)
            {
                qLearningPolicy = new QLearningPolicy();
            }

            // ? USER REQUEST: Reset Q-table on Play Start (Fresh Session), 
            // but keep learning across episodes within the session.
            qLearningPolicy.ResetQTable();
            Debug.Log("<color=yellow>[EnemyBrain] Q-table RESET for fresh learning session (User Request)</color>");

            // ? PERSISTENCE DISABLED (Uncomment to load saved data)
            /*
            if (enableLearning)
            {
                string filename = useSimpleStateExtractor ? "qtable_simple" : "qtable";
                if (PlayerPrefs.HasKey(filename))
                {
                    qLearningPolicy.LoadQTable(filename);
                }
            }
            */

            // ⚡ PROMPT 14: Subscribe to capture event
            if (chaseExecutor)
            {
                chaseExecutor.OnPlayerCaptured += OnPlayerCaptured;
            }

            // ⚡ Debug log
            if (showDebugLogs)
                Debug.Log("<color=lime>[EnemyBrain] Awake complete - Component ENABLED</color>");
        }

        private void Start()
        {
            // Start with first option
            StartNewOption();
        }

        private void Update()
        {
            // ? PROMPT 14: HARD INTERRUPT - Chase takes priority
            if (chaseExecutor && context.CanSeePlayer())
            {
                if (!chaseExecutor.IsChasing)
                {
                        // Start chase (pause current option)
                    if (currentOption != null)
                    {
                        // ✅ NEW: Credit assignment for Chase Interruption
                        HandleInterruptReward("SeePlayer (Chase)");
                        currentOption.Stop(context);

                        if (showDebugLogs)
                            Debug.Log("<color=red>[EnemyBrain] HARD INTERRUPT: CHASE STARTED!</color>");
                    }

                    chaseExecutor.StartChase();
                }

                // Update chase
                ChaseStatus chaseStatus = chaseExecutor.UpdateChase();

                if (chaseStatus == ChaseStatus.Captured)
                {
                    // ? Captured - handled by OnPlayerCaptured event
                    return;
                }
                else if (chaseStatus == ChaseStatus.Lost)
                {
                    // ? Lost target - resume options
                    if (showDebugLogs)
                        Debug.Log("<color=yellow>[EnemyBrain] Chase ended - resuming options</color>");

                    StartNewOption();
                }

                return; // Chase is active, skip option ticking
            }

            // ? Normal option execution
            if (currentOption == null)
                return;

            // ? Tick current option
            OptionStatus status = currentOption.Tick(context, Time.deltaTime);

            // ? Check for interrupts
            if (CheckForInterrupts())
                return; // Already switched

            // ? Handle completion
            if (status != OptionStatus.Running)
            {
                OnOptionCompleted(status);
            }
        }

        /// <summary>
        /// ? PROMPT 10: Start new option (Q-learning cycle)
        /// </summary>
        private void StartNewOption()
        {
            // ? 1. Extract state (SIMPLE or FULL)
            int stateKey;

            if (useSimpleStateExtractor && simpleStateExtractor)
            {
                // ✅ Use simple state (54 states)
                var simpleState = simpleStateExtractor.ExtractState();
                stateKey = simpleState.GetHashKey();
            }
            else if (stateExtractor)
            {
                // Use full state (497k states)
                RLStateKey state = stateExtractor.ExtractState();
                stateKey = state.ToPackedInt();
            }
            else
            {
                Debug.LogError("[EnemyBrain] No state extractor found!");
                return;
            }

            // ? 2. Choose action (Q-learning or heuristic)
            EnemyMode action;

            if (enableLearning)
            {
                // ? PROMPT 16: Get valid actions (action masking)
                List<EnemyMode> validActions = null;
                if (actionMasker)
                {
                    // ✅ Use appropriate state for action masking
                    if (useSimpleStateExtractor && simpleStateExtractor)
                    {
                        // Use SimpleRLStateKey for masking
                        var simpleState = simpleStateExtractor.ExtractState();
                        validActions = actionMasker.GetValidActions(simpleState);
                    }
                    else if (stateExtractor)
                    {
                        // Legacy Full State Masking (Disabled)
                        // This path is deprecated in favor of 162-state optimized mode
                        validActions = null; 
                    }
                }

                action = qLearningPolicy.ChooseAction(stateKey, explore, validActions);
            }
            else
            {
                var fullState = stateExtractor ? stateExtractor.ExtractState() : new RLStateKey();
                action = ChooseActionHeuristic(fullState);
            }

            // ? 3. Get option for action
            IEnemyOption option = GetOptionForAction(action);

            if (option == null)
            {
                Debug.LogError($"[EnemyBrain] No option found for action {action}!");
                return;
            }

            // ? 4. Start option
            SwitchOption(option);

            // ? 5. Save state/action for Q-update
            lastStateKey = stateKey; // ✅ Save for episode manager
            lastAction = action;
            currentMode = action; // ✅ Update current mode
            optionStartTime = Time.time;

            // ✅ Record action selection in metrics
            MetricsHooks.ActionSelected((int)action, action.ToString());

            // ? Debug log
            if (showDebugLogs)
            {
                float[] qValues = qLearningPolicy.GetQValues(stateKey);

                Debug.Log($"<color=cyan>[EnemyBrain] ===== NEW OPTION =====</color>");
                Debug.Log($"  State Key: {stateKey} ({(useSimpleStateExtractor ? "SIMPLE" : "FULL")})");
                Debug.Log($"  Action: {action} (ε={qLearningPolicy.epsilon:F3})");
                
                // Show state details
                if (useSimpleStateExtractor && simpleStateExtractor)
                {
                    var simpleState = simpleStateExtractor.ExtractState();
                    Debug.Log($"  State: {simpleState}");
                }

                // ? PROMPT 16: Show masked actions (Mask logging disabled for optimization)
                /*
                if (actionMasker)
                {
                     // (Optional) Implement GetAllMaskReasons in ActionMasker if detailed debug needed
                }
                */

                if (showDebugLogs)
                {
                    _debugLastQStr = string.Join(", ", System.Array.ConvertAll(qValues, q => q.ToString("F2")));
                    
                    // Decode state for readability
                    if (useSimpleStateExtractor)
                    {
                        var s = simpleStateExtractor.ExtractState();
                        string pres = s.playerPresence == 0 ? "VIS" : (s.playerPresence == 1 ? "HRD" : "LST");
                        string room = s.roomContext == 0 ? "SAME" : (s.roomContext == 1 ? "ADJ" : "FAR");
                        string heatH = s.heatHere == 0 ? "CLD" : (s.heatHere == 1 ? "WRM" : "HOT");
                        string heatN = s.heatNearby == 0 ? "CLD" : (s.heatNearby == 1 ? "WRM" : "HOT");
                        string phase = s.strategicPhase == 0 ? "EARLY" : "PANIC";
                        
                        _debugLastStateStr = $"Pres:{pres} Room:{room} Heat:{heatH}/{heatN} Phase:{phase}";
                    }
                    else
                    {
                        _debugLastStateStr = "FullState(Hidden)";
                    }
                }
            }
        }

        /// <summary>
        /// ? PROMPT 10: Option completed ? Q-update ? next option
        /// </summary>
        private void OnOptionCompleted(OptionStatus status)
        {
            float duration = Time.time - optionStartTime;

            // ? 1. Compute reward
            float reward = rewardCalculator.ComputeReward(status, lastAction, duration, context);

            // ? 2. Extract next state
            int nextStateKey;

            if (useSimpleStateExtractor && simpleStateExtractor)
            {
                var simpleState = simpleStateExtractor.ExtractState();
                nextStateKey = simpleState.GetHashKey();
            }
            else if (stateExtractor)
            {
                RLStateKey nextState = stateExtractor.ExtractState();
                nextStateKey = nextState.ToPackedInt();
            }
            else
            {
                nextStateKey = 0;
            }

            // ? 3. Q-learning update
            if (enableLearning)
            {
                qLearningPolicy.UpdateQ(lastStateKey, lastAction, reward, nextStateKey, duration);
            }

            // ? 4. Update state extractor (last action)
            if (stateExtractor)
                stateExtractor.SetLastAction(lastAction);
            // if (simpleStateExtractor) simpleStateExtractor.SetLastAction(lastAction); // Removed in new architecture

            // ? Debug log
            if (showDebugLogs)
            {
                string breakdown = rewardCalculator.GetRewardBreakdown(status, lastAction, duration, context).Replace("\n", " | ");
                Debug.Log($"<color=lime>[AI RESULT]</color> Action: <b>{lastAction}</b> -> Status: {status} -> Reward: <b>{reward:F2}</b> | {breakdown}");
            }

            // ? 5. Start next option
            StartNewOption();
        }

        /// <summary>
        /// Check for interrupts (SeePlayer, HearNoise)
        /// </summary>
        private bool CheckForInterrupts()
        {
            if (currentOption == null)
                return false;

            // ? PRIORITY 1: SeePlayer (always)
            if (context.CanSeePlayer())
            {
                if (currentOption.CanBeInterruptedBy(InterruptType.SeePlayer))
                {
                    if (showDebugLogs)
                        Debug.Log("<color=red>[EnemyBrain] INTERRUPT: SeePlayer!</color>");

                    // Force switch to Investigate (chase)
                    ForceInterrupt(EnemyMode.InvestigateLastHeard);
                    return true;
                }
            }

            // ? PRIORITY 2: HearNoise (only if Patrol)
            if (context.worldModel.HasHeardRecently)
            {
                if (currentOption.CanBeInterruptedBy(InterruptType.HearNoise))
                {
                    if (showDebugLogs)
                        Debug.Log("<color=yellow>[EnemyBrain] INTERRUPT: HearNoise!</color>");

                    // Switch to Investigate
                    ForceInterrupt(EnemyMode.InvestigateLastHeard);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Force interrupt (override current option)
        /// </summary>
        private void ForceInterrupt(EnemyMode forcedAction)
        {
            // Stop current option (no Q-update - interrupted)
            if (currentOption != null)
            {
                // ✅ NEW: Credit assignment for interruption
                HandleInterruptReward($"ForceInterrupt({forcedAction})");
                currentOption.Stop(context);
            }

            // Get forced option
            IEnemyOption forcedOption = GetOptionForAction(forcedAction);

            if (forcedOption != null)
            {
                SwitchOption(forcedOption);

                // Update tracking
                lastAction = forcedAction;
                optionStartTime = Time.time;
            }
        }

        // Debug storage
        private string _debugLastStateStr = "";
        private string _debugLastQStr = "";

        /// <summary>
        /// Switch to new option
        /// </summary>
        private void SwitchOption(IEnemyOption newOption)
        {
            // Stop current
            if (currentOption != null)
            {
                currentOption.Stop(context);
            }

            // Start new (Initialize instead of Start to avoid Unity conflict)
            currentOption = newOption;
            currentOption.Initialize(context);

            if (showDebugLogs)
            {
                // Find target room / description
                string targetDesc = "None";

                if (newOption is PatrolOption patrol)
                {
                    targetDesc = "Route: " + patrol.GetRouteDescription();
                }
                else if (newOption is SweepOption sweep)
                {
                    targetDesc = "Sweep: " + sweep.GetTargetDescription();
                }
                else if (context.agentMover.Destination.HasValue)
                {
                    Vector2 dest = context.agentMover.Destination.Value;
                    if (AITest.World.WorldRegistry.Instance)
                    {
                        var r = AITest.World.WorldRegistry.Instance.GetRoomAtPosition(dest);
                        if (r != null) targetDesc = "Target: " + r.roomName;
                        else targetDesc = "Hallway/Unknown";
                    }
                }

                // Add perceptron details if available
                var ts = GetComponent<AITest.Learning.TargetSelector>();
                if (ts && !string.IsNullOrEmpty(ts.LastPerceptronLog) && (newOption is PatrolOption || newOption is SweepOption))
                {
                    targetDesc += $" | [Perceptron: {ts.LastPerceptronLog}]";
                }

                Debug.Log($"<color=cyan>[AI DECISION]</color> State: [{_debugLastStateStr}] -> Action: <b>{newOption.Mode}</b> (Q={qLearningPolicy.GetQValue(lastStateKey, (int)newOption.Mode):F2}) -> {targetDesc} | Qs: [{_debugLastQStr}]");
            }
        }

        /// <summary>
        /// Get option instance for action
        /// </summary>
        private IEnemyOption GetOptionForAction(EnemyMode action)
        {
            switch (action)
            {
                case EnemyMode.Patrol:
                    return patrolOption;
                case EnemyMode.InvestigateLastHeard:
                    return investigateOption;
                // case EnemyMode.HeatSearchPeak: return heatSearchOption; // REMOVED
                case EnemyMode.SweepArea:
                    return sweepOption;
                case EnemyMode.HideSpotCheck:
                    return hideSpotCheckOption;
                case EnemyMode.HeatSweep: // ✅ RoomTracker-based
                    return heatSweepOption;
                case EnemyMode.AmbushHotChoke: // ✅ RoomTracker-based
                    return ambushHotChokeOption;
                default:
                    return patrolOption; // Fallback
            }
        }

        /// <summary>
        /// Heuristic action selection (fallback if learning disabled)
        /// </summary>
        private EnemyMode ChooseActionHeuristic(RLStateKey state)
        {
            // Simple heuristic rules

            if (state.seePlayer == 1)
                return EnemyMode.InvestigateLastHeard; // Chase!

            if (state.hearRecently == 1)
                return EnemyMode.InvestigateLastHeard;

            if (state.heatConfidence >= 2) // High heat
            {
                // 50% chance between ambush and heat search when heat is high
                if (Random.value < 0.5f)
                    return EnemyMode.AmbushHotChoke;
                else
                    return EnemyMode.HeatSweep; // Was HeatSearchPeak
            }

            if (state.nearHideSpots == 1)
                return EnemyMode.HideSpotCheck;

            if (state.timeSinceSeenBin == 0) // Just saw player
                return EnemyMode.SweepArea;

            // Default: Patrol
            return EnemyMode.Patrol;
        }

        /// <summary>
        /// End episode (decay epsilon, save Q-table)
        /// </summary>
        public void EndEpisode()
        {
            if (enableLearning)
            {
                qLearningPolicy.DecayEpsilon();
            }

            // Reset reward calculator
            rewardCalculator.Reset();
        }

        /// <summary>
        /// Context menu: Save Q-table
        /// </summary>
        [ContextMenu("Save Q-Table")]
        public void SaveQTable()
        {
            string filename = useSimpleStateExtractor ? "qtable_simple" : "qtable";
            qLearningPolicy.SaveQTable(filename);
        }

        /// <summary>
        /// Context menu: Load Q-table
        /// </summary>
        [ContextMenu("Load Q-Table")]
        public void LoadQTable()
        {
            string filename = useSimpleStateExtractor ? "qtable_simple" : "qtable";
            qLearningPolicy.LoadQTable(filename);
        }

        /// <summary>
        /// Context menu: Reset Q-table
        /// </summary>
        [ContextMenu("Reset Q-Table")]
        public void ResetQTable()
        {
            qLearningPolicy.ResetQTable();
        }

        /// <summary>
        /// Context menu: Print current state
        /// </summary>
        [ContextMenu("Print Current State")]
        public void PrintCurrentState()
        {
            int stateKey;

            if (useSimpleStateExtractor && simpleStateExtractor)
            {
                var simpleState = simpleStateExtractor.ExtractState();
                stateKey = simpleState.GetHashKey();
                Debug.Log($"<color=cyan>===== CURRENT STATE (SIMPLE) =====</color>");
                Debug.Log($"State: {simpleState}");
            }
            else if (stateExtractor)
            {
                RLStateKey state = stateExtractor.ExtractState();
                stateKey = state.ToPackedInt();
                Debug.Log($"<color=cyan>===== CURRENT STATE (FULL) =====</color>");
                Debug.Log($"State: {state}");
            }
            else
            {
                Debug.LogError("[EnemyBrain] No state extractor found!");
                return;
            }

            float[] qValues = qLearningPolicy.GetQValues(stateKey);

            Debug.Log($"State Key: {stateKey}");
            Debug.Log($"Q-values:");

            for (int i = 0; i < qValues.Length; i++)
            {
                EnemyMode mode = (EnemyMode)i;
                Debug.Log($"  {mode}: {qValues[i]:F3}");
            }

            Debug.Log($"Stats: {qLearningPolicy.GetStatsSummary()}");
        }

        /// <summary>
        /// ? PROMPT 14: Player captured event handler
        /// </summary>
        private void OnPlayerCaptured(float captureTime, Vector2 capturePos)
        {
            if (showDebugLogs)
            {
                Debug.Log($"<color=lime>[EnemyBrain] ??? PLAYER CAPTURED! ???</color>");
                Debug.Log($"  Capture Time: {captureTime:F2}s");
                Debug.Log($"  Position: {capturePos}");
            }

            // ? Big reward for capture
            float bigReward = chaseExecutor ? chaseExecutor.captureReward : 50f;

            // ? Apply reward to last Q-update
            if (enableLearning && lastStateKey != 0)
            {
                int currentStateKey;

                if (useSimpleStateExtractor && simpleStateExtractor)
                {
                    var simpleState = simpleStateExtractor.ExtractState();
                    currentStateKey = simpleState.GetHashKey();
                }
                else if (stateExtractor)
                {
                    RLStateKey currentState = stateExtractor.ExtractState();
                    currentStateKey = currentState.ToPackedInt();
                }
                else
                {
                    currentStateKey = 0;
                }

                // Immediate Q-update with capture reward
                // ❌ REMOVED: Double counting fix! EpisodeManager handles this.
                // qLearningPolicy.UpdateQ(lastStateKey, lastAction, bigReward, currentStateKey, captureTime);

                if (showDebugLogs)
                    Debug.Log($"<color=lime>[EnemyBrain] Capture Event received (Reward via EpisodeManager)</color>");
            }

            // ⚡ NOTE: EpisodeManager will handle episode end and reset
        }

        /// <summary>
        /// ? Handle Reward/Q-Update for Interrupted Options (NEW)
        /// Ensures data isn't lost when switching mid-option (e.g. noise heard)
        /// </summary>
        private void HandleInterruptReward(string interruptReason)
        {
             if (currentOption == null || !enableLearning) return;

             float duration = Time.time - optionStartTime;
             
             // 1. Compute partial reward
             // (Status is Running because it was interrupted)
             float reward = rewardCalculator.ComputeReward(OptionStatus.Running, lastAction, duration, context);

             // 2. Extract current state as "next state"
             int nextStateKey = GetCurrentStateKey();

             // 3. Update Q-table
             qLearningPolicy.UpdateQ(lastStateKey, lastAction, reward, nextStateKey, duration);

             if (showDebugLogs)
             {
                 Debug.Log($"<color=orange>[EnemyBrain] Option {lastAction} INTERRUPTED by {interruptReason}!</color>");
                 Debug.Log($"  Partial Reward: {reward:F2} | Duration: {duration:F2}s");
             }
        }

        private int GetCurrentStateKey()
        {
             if (useSimpleStateExtractor && simpleStateExtractor)
             {
                 var simpleState = simpleStateExtractor.ExtractState();
                 return simpleState.GetHashKey();
             }
             else if (stateExtractor)
             {
                 RLStateKey state = stateExtractor.ExtractState();
                 return state.ToPackedInt();
             }
             return 0;
        }
    }
}
