using AITest.Quest;
using AITest.World;
using AITest.Enemy;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulated Player bot for offline training.
/// - Chooses current quest objective via QuestManager
/// - Moves using EnemyMover2D (A* via Pathfinder + EnemyController)
/// - Interacts programmatically with QuestInteractable (no Input)
/// - Reacts to enemy threat (hide/escape)
/// - NEW: Walk/Sprint + stamina + footstep noise + interaction noise pulses
/// </summary>
[RequireComponent(typeof(BotThreatModel))]
public class PlayerTrainingBotController : MonoBehaviour
{
    public enum BotState
    {
        AcquireQuestTarget,
        Searching, // ? NEW: Stochastic search (simulate not knowing where quest is)
        MoveToQuest,
        InteractQuest,
        ThreatDecision,
        MoveToHide,
        Hiding,
        Escape,
        Recover
    }

    public enum LocomotionMode
    {
        Walk,
        Sprint
    }

    [Header("Refs")]
    public BotThreatModel threat;

    [Header("Movement (A*)")]
    public AIAgentMover mover;

    [Header("World Objects")]
    // ? CHANGED: Auto-filled from Registry
    private List<HideSpot> hideSpots = new(); 
    public List<SafePoint> safePoints = new();

    [Header("Bot Behavior Params (randomized per episode)")]
    [Range(0, 1)] public float riskTolerance = 0.5f;
    [Range(0, 1)] public float hidePreference = 0.7f;
    [Range(0, 1)] public float intelligence = 0.4f; // ? NEW: Chance to find quest per room check

    /// <summary>
    /// 0 = "gürültü umrumda değil", 1 = "çok sessiz"
    /// Sprint kullanımını ve çıkardığı sesi etkiler.
    /// </summary>
    [Range(0, 1)] public float noiseDiscipline = 0.6f;

    [Header("Tuning")]
    public float decisionTickHz = 5f;
    public float interactRange = 0.9f;
    public float hideRange = 0.9f;
    public float hideMinSeconds = 10.0f;
    public float hideMaxSeconds = 25.0f;
    public float recoverSeconds = 0.8f;
    public float searchWaitSeconds = 0.5f; // ? NEW: Time to spend "searching" a room

    [Header("Noise Hook (optional)")]
    /// <summary>
    /// Wire this to your enemy hearing / noise bus.
    /// EmitNoise(position, loudness)
    /// loudness: recommended 0..1 range
    /// </summary>
    public System.Action<Vector2, float> EmitNoise;

    [Header("Locomotion (Walk/Sprint)")]
    [Tooltip("Walk multiplier applied to EnemyController.speed")]
    public float walkSpeedMultiplier = 1.0f;

    [Tooltip("Sprint multiplier applied to EnemyController.speed")]
    public float sprintSpeedMultiplier = 1.8f; // ? INCREASED slightly for better escapes

    [Tooltip("Minimum seconds bot commits to sprint when it decides to sprint")]
    public float sprintMinBurst = 0.8f; // ? INCREASED

    [Tooltip("Maximum seconds bot commits to sprint when it decides to sprint")]
    public float sprintMaxBurst = 2.0f; // ? INCREASED

    [Tooltip("Cooldown after sprint ends (randomized)")]
    public float sprintCooldownMin = 0.4f;

    public float sprintCooldownMax = 1.2f;

    [Header("Stamina")]
    public float staminaMax = 5.0f;
    public float staminaRegenPerSec = 1.25f;
    public float sprintStaminaCostPerSec = 2.2f;

    [Header("Footstep Noise")]
    [Tooltip("Base loudness for walking footsteps (0..1)")]
    [Range(0f, 1f)] public float walkFootstepLoudness = 0.22f;

    [Tooltip("Base loudness for sprint footsteps (0..1)")]
    [Range(0f, 1f)] public float sprintFootstepLoudness = 0.55f;

    [Tooltip("Seconds between footstep noise emissions while walking")]
    public float walkFootstepInterval = 0.45f;

    [Tooltip("Seconds between footstep noise emissions while sprinting")]
    public float sprintFootstepInterval = 0.25f;

    [Header("Interaction Noise")]
    [Tooltip("While interacting, emit a small 'working' noise pulse")]
    [Range(0f, 1f)] public float interactPulseLoudness = 0.18f;

    [Tooltip("Seconds between interaction noise pulses")]
    public float interactPulseInterval = 0.8f;

    [Header("Human-like Timing")]
    [Tooltip("Small delay before starting interaction to look around / align")]
    public float preInteractDelayMin = 0.10f;
    public float preInteractDelayMax = 0.55f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugGizmos = true;

    private BotState state = BotState.AcquireQuestTarget;
    private LocomotionMode locomotion = LocomotionMode.Walk;

    private QuestInteractable currentQuest;
    private HideSpot currentHide;
    private SafePoint currentSafe;

    private float tickAccum;
    private float stateTime;
    private float hideUntil;
    
    // ? NEW: Search state variables
    private bool knowsQuestLocation;
    private Vector2 currentSearchTarget;
    private float searchFinishedTime;

    // locomotion runtime
    private float baseSpeed = 3f;         // cached EnemyController.speed
    private float stamina;
    private float sprintUntil;
    private float nextSprintAllowedTime;
    private float nextFootstepTime;
    private float nextInteractPulseTime;

    // interaction runtime
    private bool interactionStarted;
    private float interactionStartAt;

    void Awake()
    {
        if (!threat) threat = GetComponent<BotThreatModel>();
        if (!mover) mover = GetComponent<AIAgentMover>();

        if (mover == null)
        {
            Debug.LogError("[PlayerTrainingBotController] AIAgentMover not found. " +
                           "Add AIAgentMover (and its required Pathfinder + AICharacterController) to PlayerBot.");
            return;
        }

        // Cache base speed from EnemyController (the real mover)
        if (mover.controller != null)
        {
            baseSpeed = Mathf.Max(0.1f, mover.controller.moveSpeed);
        }

        // ? AUTO-FILL HIDE SPOTS
        if (AITest.World.WorldRegistry.Instance)
        {
            hideSpots = AITest.World.WorldRegistry.Instance.GetAllHideSpots();
        }

        // ? AUTO-FILL SAFE POINTS
        if (safePoints == null || safePoints.Count == 0)
        {
            safePoints = new List<SafePoint>(FindObjectsByType<SafePoint>(FindObjectsSortMode.None));
        }

        stamina = staminaMax;
        ApplyLocomotion(LocomotionMode.Walk);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // stamina regen + sprint drain
        TickStamina(dt);

        // footsteps + interaction pulses (noise system)
        TickNoise(dt);

        tickAccum += dt;
        float tickInterval = 1f / Mathf.Max(1f, decisionTickHz);
        if (tickAccum < tickInterval) return;

        float tickDt = tickAccum;
        tickAccum = 0f;

        stateTime += tickDt;
        threat.Tick(tickDt);

        switch (state)
        {
            case BotState.AcquireQuestTarget: TickAcquireQuestTarget(); break;
            case BotState.Searching: TickSearching(); break; // ? NEW
            case BotState.MoveToQuest: TickMoveToQuest(); break;
            case BotState.InteractQuest: TickInteractQuest(tickDt); break;
            case BotState.ThreatDecision: TickThreatDecision(); break;
            case BotState.MoveToHide: TickMoveToHide(); break;
            case BotState.Hiding: TickHiding(); break;
            case BotState.Escape: TickEscape(); break;
            case BotState.Recover: TickRecover(); break;
        }
    }

    // Call from your episode manager on reset
    public void ResetForNewEpisode(int seed)
    {
        Random.InitState(seed);

        riskTolerance = Random.Range(0.2f, 0.8f);
        hidePreference = Random.Range(0.3f, 0.9f);
        noiseDiscipline = Random.Range(0.3f, 0.9f);
        intelligence = Random.Range(0.2f, 0.7f); // ? NEW: Randomize search smarts

        // Re-fetch hide spots (in case map changed or first run missed it)
        if (AITest.World.WorldRegistry.Instance)
            hideSpots = AITest.World.WorldRegistry.Instance.GetAllHideSpots();

        // Re-fetch safe points
        if (safePoints == null || safePoints.Count == 0)
             safePoints = new List<SafePoint>(FindObjectsByType<SafePoint>(FindObjectsSortMode.None));

        currentQuest = null;
        knowsQuestLocation = false; // Reset knowledge

        if (currentHide != null) currentHide.Exit(gameObject);
        currentHide = null;

        currentSafe = null;

        if (mover != null) mover.ClearDestination();

        // reset locomotion runtime
        stamina = staminaMax;
        sprintUntil = 0f;
        nextSprintAllowedTime = 0f;
        nextFootstepTime = Time.time + Random.Range(0.05f, 0.2f);
        nextInteractPulseTime = 0f;

        ApplyLocomotion(LocomotionMode.Walk);

        // reset interaction runtime
        interactionStarted = false;
        interactionStartAt = 0f;

        state = BotState.AcquireQuestTarget;
        tickAccum = 0f;
        stateTime = 0f;
        hideUntil = 0f;

        if (debugLogs)
            Debug.Log($"[Bot] Reset seed={seed} risk={riskTolerance:F2} hidePref={hidePreference:F2} noiseDisc={noiseDiscipline:F2}");
    }

    private void Transition(BotState next)
    {
        state = next;
        stateTime = 0f;

        // entering a new state often changes locomotion
        if (state == BotState.InteractQuest || state == BotState.Hiding)
            ApplyLocomotion(LocomotionMode.Walk);

        // reset interaction flags when leaving interact state
        if (state != BotState.InteractQuest)
        {
            interactionStarted = false;
            interactionStartAt = 0f;
        }
    }

    // -------------------------
    // QUEST FLOW
    // -------------------------

    private void TickAcquireQuestTarget()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.IsQuestCompleted())
        {
            Transition(BotState.Recover);
            return;
        }

        // Threat first
        if (threat.CurrentThreat != BotThreatModel.ThreatLevel.Low)
        {
            Transition(BotState.ThreatDecision);
            return;
        }

        currentQuest = (QuestManager.Instance != null) ? QuestManager.Instance.GetCurrentInteractable() : null;

        if (currentQuest == null)
        {
            // If no quest found, don't just idle. SEARCH randomly.
            if (AITest.World.WorldRegistry.Instance)
            {
               var rooms = AITest.World.WorldRegistry.Instance.GetAllRooms();
               if (rooms != null && rooms.Count > 0)
               {
                   var r = rooms[Random.Range(0, rooms.Count)];
                   if (r != null)
                   {
                       currentSearchTarget = r.Center;
                       mover?.SetDestination(currentSearchTarget);
                       Transition(BotState.Searching);
                       return;
                   }
               }
            }

            // Fallback if no rooms or something failed
            Transition(BotState.Recover);
            return;
        }

        // ? KNOWLEDGE CHECK
        // If we don't 'know' where it is, we simulate searching
        // Intelligence determines chance to find it or pick random room
        if (!knowsQuestLocation)
        {
            // Pick a random room to search first
            if (AITest.World.WorldRegistry.Instance)
            {
               var rooms = AITest.World.WorldRegistry.Instance.GetAllRooms();
               if (rooms != null && rooms.Count > 0)
               {
                   // Pick random except current
                   var r = rooms[Random.Range(0, rooms.Count)];
                   if (r != null)
                   {
                       currentSearchTarget = r.Center;
                       mover?.SetDestination(currentSearchTarget);
                       Transition(BotState.Searching);
                       return;
                   }
               }
            }
            // Fallback: if no registry, we just "know" it
            knowsQuestLocation = true;
        }

        // We know where it is, move to it!
        // choose locomotion to quest (sometimes sprint if undisciplined / far / confident)
        MaybeSprintForTravel(currentQuest.transform.position, preferQuiet: true);

        mover?.SetDestination(currentQuest.transform.position);
        Transition(BotState.MoveToQuest);
    }

    // ? NEW: Searching logic
    private void TickSearching()
    {
        // Threat overrides search
        if (threat.CurrentThreat != BotThreatModel.ThreatLevel.Low)
        {
            Transition(BotState.ThreatDecision);
            return;
        }

        // Timeout check (prevent stuck)
        if (stateTime > 10f)
        {
            if (debugLogs) Debug.LogWarning("[Bot] Search timed out. Repicking.");
            Transition(BotState.AcquireQuestTarget);
            return;
        }

        float d = Vector2.Distance(transform.position, currentSearchTarget);
        if (d <= 1.5f || (mover != null && mover.ReachedDestination))
        {
            // Arrived at search room center
            // Simulating "looking around"
            if (stateTime < searchWaitSeconds) return; // Wait a bit at the spot

            // Roll to see if we find the quest
            // Intelligence affects finding it. Also if we are physically close to real quest, find it.
            bool actuallyClose = currentQuest && Vector2.Distance(transform.position, currentQuest.transform.position) < 10f;
            
            if (actuallyClose || Random.value < intelligence)
            {
                // Found it!
                knowsQuestLocation = true;
                if (debugLogs) Debug.Log("<color=green>[Bot] Searching... Found Quest!</color>");
                Transition(BotState.AcquireQuestTarget); // Will cascade to MoveToQuest
            }
            else
            {
                // Failed, pick another room
                if (debugLogs) Debug.Log("<color=orange>[Bot] Searching... Wrong room. Retrying.</color>");
                Transition(BotState.AcquireQuestTarget); // Will re-enter and pick new random room
            }
        }
    }

    private void TickMoveToQuest()
    {
        if (currentQuest == null || currentQuest.IsCompleted)
        {
            knowsQuestLocation = false; // Reset for next quest
            Transition(BotState.AcquireQuestTarget);
            return;
        }

        // Threat response: high -> break and decide
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.High)
        {
            Transition(BotState.ThreatDecision);
            return;
        }

        // Mid threat: risk tolerant can continue; otherwise hide/escape
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.Mid && riskTolerance < 0.55f)
        {
            Transition(BotState.ThreatDecision);
            return;
        }

        float d = Vector2.Distance(transform.position, currentQuest.transform.position);
        if (d <= interactRange)
        {
            mover?.Stop();

            // Human-like: short pre-interact delay
            interactionStarted = false;
            interactionStartAt = Time.time + Random.Range(preInteractDelayMin, preInteractDelayMax);

            Transition(BotState.InteractQuest);
            return;
        }

        // If destination reached but still not in range => re-evaluate
        if (mover != null && mover.ReachedDestination && d > interactRange)
        {
            Transition(BotState.ThreatDecision);
        }
    }

    private void TickInteractQuest(float dt)
    {
        if (currentQuest == null || currentQuest.IsCompleted)
        {
            knowsQuestLocation = false; // Reset for next quest
            Transition(BotState.AcquireQuestTarget);
            return;
        }

        // If high threat, break interaction unless very risk tolerant
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.High && riskTolerance < 0.8f)
        {
            currentQuest.CancelInteractionBy(transform);
            Transition(BotState.ThreatDecision);
            return;
        }

        // Start interaction after pre-delay
        if (!interactionStarted)
        {
            if (Time.time < interactionStartAt)
                return;

            currentQuest.BeginInteraction(transform);
            interactionStarted = true;

            // tiny "button click" noise
            EmitNoiseSafe(0.10f);
        }

        bool done = currentQuest.TickInteraction(transform, dt);
        if (done || currentQuest.IsCompleted)
        {
            // ? NEW: Quest Complete NOISE! (Loud mechanical sound)
            EmitNoiseSafe(1.0f); // Max loudness = 25m radius
            if (debugLogs) Debug.Log("<color=yellow>[Bot] Quest Completed! LOUD NOISE EMITTED!</color>");

            knowsQuestLocation = false; // Reset for next quest
            Transition(BotState.AcquireQuestTarget);
        }
    }

    // -------------------------
    // THREAT / HIDE / ESCAPE
    // -------------------------

    private void TickThreatDecision()
    {
        // Under threat: prioritize survival, sprint allowed even if noisy
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.High)
        {
            // If currently interacting, cancel immediately (unless very tolerant)
            if (currentQuest != null && !currentQuest.IsCompleted && riskTolerance < 0.85f)
                currentQuest.CancelInteractionBy(transform);

            HideSpot hs = null;
            bool chooseHide = hidePreference >= 0.5f && TryPickHideSpot(out hs);

            if (chooseHide && hs != null)
            {
                currentHide = hs;
                mover?.SetDestination(currentHide.transform.position);

                // Sprint to hide if stamina allows (panic)
                ForceSprintBurst();
                Transition(BotState.MoveToHide);
                return;
            }

            currentSafe = PickFarthestSafePointFromEnemy();
            if (currentSafe != null)
            {
                mover?.SetDestination(currentSafe.transform.position);
                ForceSprintBurst();
                Transition(BotState.Escape);
                return;
            }

            // fallback: move away from enemy
            if (threat.enemy != null)
            {
                Vector2 away = (Vector2)transform.position +
                              ((Vector2)transform.position - (Vector2)threat.enemy.position).normalized * 4f;
                mover?.SetDestination(away);
                ForceSprintBurst();
                Transition(BotState.Escape);
                return;
            }

            Transition(BotState.Recover);
            return;
        }

        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.Mid)
        {
            // "cautious" bots may retreat; "brave" bots continue
            if (riskTolerance > 0.65f)
            {
                // Prefer quiet movement under mid threat if disciplined
                ApplyLocomotion(LocomotionMode.Walk);
                Transition(BotState.AcquireQuestTarget);
                return;
            }

            currentSafe = PickNearestSafePoint();
            if (currentSafe != null)
            {
                mover?.SetDestination(currentSafe.transform.position);
                // Under mid threat, sprint depends on discipline and stamina
                MaybeSprintForTravel(currentSafe.transform.position, preferQuiet: false);
                Transition(BotState.Escape);
            }
            else
            {
                Transition(BotState.Recover);
            }
            return;
        }

        // Low threat: resume quests
        ApplyLocomotion(LocomotionMode.Walk);
        Transition(BotState.AcquireQuestTarget);
    }

    private void TickMoveToHide()
    {
        if (currentHide == null)
        {
            Transition(BotState.ThreatDecision);
            return;
        }

        float d = Vector2.Distance(transform.position, currentHide.transform.position);
        if (d <= hideRange)
        {
            mover?.Stop();
            currentHide.TryEnter(gameObject);

            hideUntil = Time.time + Random.Range(hideMinSeconds, hideMaxSeconds);

            // when entering hide, go quiet
            ApplyLocomotion(LocomotionMode.Walk);
            Transition(BotState.Hiding);
            return;
        }

        if (mover != null && mover.ReachedDestination && d > hideRange)
        {
            Transition(BotState.ThreatDecision);
        }
    }

    private void TickHiding()
    {
        // 1. Check occupancy
        if (currentHide == null || !currentHide.IsPlayerHiding)
        {
            ApplyLocomotion(LocomotionMode.Sprint);
            Transition(BotState.Escape);
            return;
        }

        // 2. Wait for hide timer
        bool timeUp = Time.time >= hideUntil;
        
        // ? NEW: Enforce Minimum Stay Duration (2.0s) to prevent flickering
        // Even if threat drops instantly, stay a bit to be sure.
        bool minStaySatisfied = Time.time >= (hideUntil - (hideMaxSeconds - 3.0f)); 
        // Logic: hideUntil is (Now + Rand(10, 25)). So we force at least ~3-5s stay relative to start.
        // Better: track entry time.
        // Let's use stateTime.
        if (stateTime < 4.0f) // Minimum 4 seconds strict lock
        {
             // EMERGENCY EXIT ONLY: If enemy is literally opening the locker (Distance < 1.5m)
             if (threat.enemy != null && Vector2.Distance(transform.position, threat.enemy.position) < 1.5f)
             {
                 // Panic exit
             }
             else
             {
                 return; // STAY HIDDEN
             }
        }

        // 3. Assess safety
        bool enemyFar = true;
        if (threat.enemy != null)
        {
            float dist = Vector2.Distance(transform.position, threat.enemy.position);
            if (dist < 8.0f) enemyFar = false; // Increased safety margin from 6.0 to 8.0
        }

        // Exit conditions:
        // - Time is up AND enemy is far (Clean exit)
        // - Threat is LOW AND we stayed long enough (Early safe exit)
        // - Forced exit (timeout)
        
        bool safeToExit = (timeUp && enemyFar) || (threat.CurrentThreat == BotThreatModel.ThreatLevel.Low && enemyFar);
        bool forceExit = Time.time >= hideUntil + 15f; 

        if (safeToExit || forceExit)
        {
            if (currentHide != null)
                currentHide.Exit(gameObject);

            currentHide = null;
            Transition(BotState.Recover);
        }
    }

    private void TickEscape()
    {
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.Low)
        {
            ApplyLocomotion(LocomotionMode.Walk);
            Transition(BotState.AcquireQuestTarget);
            return;
        }

        if (threat.enemy != null && Vector2.Distance(transform.position, threat.enemy.position) < 3.5f)
        {
            ForceSprintBurst();
        }

        // Path validation (existing)
        if (mover != null && mover.IsMoving && threat.enemy != null)
        {
            float distToEnemy = Vector2.Distance(transform.position, threat.enemy.position);
            Vector2 nextPos = (Vector2)transform.position + mover.Velocity * 0.5f; 
            float futureDist = Vector2.Distance(nextPos, threat.enemy.position);

            if (futureDist < distToEnemy - 0.2f)
            {
                mover.Stop();
                currentSafe = null;
            }
        }

        if (currentSafe == null)
        {
             if (safePoints != null && safePoints.Count > 0)
             {
                 // ? IMPROVED SELECTION: Filter out points where path goes through enemy
                 currentSafe = PickBestSafePointWithSafetyCheck();
                 
                 if (currentSafe != null)
                 {
                     mover?.SetDestination(currentSafe.transform.position);
                     MaybeSprintForTravel(currentSafe.transform.position, preferQuiet: false);
                 }
             }
             
             // Fallback
             if (currentSafe == null && threat.enemy != null)
             {
                 // Panic Run Logic (same as before but maybe smarter?)
                 // ... existing logic ...
                 if (AITest.World.WorldRegistry.Instance)
                 {
                     // ... existing room logic ...
                     var rooms = AITest.World.WorldRegistry.Instance.GetAllRooms();
                     AITest.World.RoomZone bestRoom = null;
                     float maxScore = -1f;

                     foreach(var r in rooms) {
                         if(!r) continue;
                         float d = Vector2.Distance(r.Center, threat.enemy.position);
                         // Penalize rooms that require crossing the enemy
                         float angle = Vector2.Angle(r.Center - (Vector2)transform.position, (Vector2)threat.enemy.position - (Vector2)transform.position);
                         if (angle < 45f) d -= 100f; // Strongly penalize instructions towards enemy

                         float score = d + Random.Range(0f, 5f);
                         if (score > maxScore) { maxScore = score; bestRoom = r; }
                     }

                     if (bestRoom != null) {
                         mover?.SetDestination(bestRoom.Center);
                         ForceSprintBurst();
                     }
                 }
                 else
                 {
                     Vector2 dir = ((Vector2)transform.position - (Vector2)threat.enemy.position).normalized;
                     Vector2 panicDest = (Vector2)transform.position + dir * 6f;
                     mover?.SetDestination(panicDest);
                     ForceSprintBurst();
                 }
             }
        }

        if (mover != null && mover.ReachedDestination)
        {
            currentSafe = null;
        }
    }

    // ? NEW HELPER
    private SafePoint PickBestSafePointWithSafetyCheck()
    {
        if (threat.enemy == null) return PickFarthestSafePointFromEnemy();

        SafePoint best = null;
        float bestScore = -9999f;
        
        foreach (var sp in safePoints)
        {
            if (sp == null) continue;
            
            float distToMe = Vector2.Distance(transform.position, sp.transform.position);
            float distToEnemy = Vector2.Distance(sp.transform.position, threat.enemy.position);
            
            // Safety check: Don't pick point if direction is towards enemy
            Vector2 dirToPoint = (sp.transform.position - transform.position).normalized;
            Vector2 dirToEnemy = (threat.enemy.position - transform.position).normalized;
            float dot = Vector2.Dot(dirToPoint, dirToEnemy);
            
            if (dot > 0.5f) continue; // Skip if point is generally in direction of enemy (within 60 degrees)

            // Score: Further from enemy is better, Closer to me is better (less travel)
            float score = distToEnemy * 2.0f - distToMe;
            
            if (score > bestScore)
            {
                bestScore = score;
                best = sp;
            }
        }
        
        return best;
    }

    private void TickRecover()
    {
        // small idle recovery; stamina regenerates naturally
        if (stateTime >= recoverSeconds)
            Transition(BotState.AcquireQuestTarget);
    }

    // -------------------------
    // LOCOMOTION + NOISE
    // -------------------------

    private void TickStamina(float dt)
    {
        // Drain if sprinting and moving
        bool sprinting = (locomotion == LocomotionMode.Sprint);
        bool moving = mover != null && mover.IsMoving;

        if (sprinting && moving)
        {
            stamina -= sprintStaminaCostPerSec * dt;
            if (stamina <= 0.1f)
            {
                stamina = 0.1f;
                EndSprint();
            }
        }
        else
        {
            stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * dt);
        }

        // auto end sprint when burst ends
        if (sprinting && Time.time >= sprintUntil)
            EndSprint();
    }

    private void TickNoise(float dt)
    {
        if (EmitNoise == null || mover == null) return;

        // Footsteps while moving
        if (mover.IsMoving && Time.time >= nextFootstepTime)
        {
            float loudness = (locomotion == LocomotionMode.Sprint) ? sprintFootstepLoudness : walkFootstepLoudness;

            // Discipline reduces loudness slightly (more careful footfalls)
            loudness *= Mathf.Lerp(1.15f, 0.85f, noiseDiscipline);

            EmitNoiseSafe(loudness);

            float interval = (locomotion == LocomotionMode.Sprint) ? sprintFootstepInterval : walkFootstepInterval;
            // slight randomness so it doesn't look robotic
            interval *= Random.Range(0.9f, 1.15f);

            nextFootstepTime = Time.time + interval;
        }

        // Interaction pulses (small working noises)
        if (state == BotState.InteractQuest && interactionStarted && Time.time >= nextInteractPulseTime)
        {
            float loudness = interactPulseLoudness * Mathf.Lerp(1.0f, 0.6f, noiseDiscipline);
            EmitNoiseSafe(loudness);
            nextInteractPulseTime = Time.time + interactPulseInterval * Random.Range(0.9f, 1.2f);
        }
    }

    private void ApplyLocomotion(LocomotionMode mode)
    {
        locomotion = mode;
        if (mover == null || mover.controller == null) return;

        float mult = (mode == LocomotionMode.Sprint) ? sprintSpeedMultiplier : walkSpeedMultiplier;
        mover.controller.moveSpeed = Mathf.Max(0.1f, baseSpeed * mult);
    }

    private void ForceSprintBurst()
    {
        if (Time.time < nextSprintAllowedTime) return;
        if (stamina < Mathf.Min(1.2f, staminaMax * 0.25f)) return;

        ApplyLocomotion(LocomotionMode.Sprint);

        float burst = Random.Range(sprintMinBurst, sprintMaxBurst);
        sprintUntil = Time.time + burst;

        nextSprintAllowedTime = sprintUntil + Random.Range(sprintCooldownMin, sprintCooldownMax);

        // Sprint start = louder "burst" noise
        EmitNoiseSafe(0.65f * Mathf.Lerp(1.0f, 0.8f, noiseDiscipline));
    }

    private void EndSprint()
    {
        if (locomotion != LocomotionMode.Sprint) return;

        ApplyLocomotion(LocomotionMode.Walk);

        // stopping sprint = small noise
        EmitNoiseSafe(0.25f * Mathf.Lerp(1.0f, 0.7f, noiseDiscipline));
    }

    private void MaybeSprintForTravel(Vector2 target, bool preferQuiet)
    {
        // preferQuiet=true: quest travel -> sprint less unless undisciplined
        if (Time.time < nextSprintAllowedTime) { ApplyLocomotion(LocomotionMode.Walk); return; }

        float dist = Vector2.Distance(transform.position, target);
        if (dist < 5.5f) { ApplyLocomotion(LocomotionMode.Walk); return; }

        // Discipline strongly suppresses quest sprinting
        float baseProb = preferQuiet ? 0.35f : 0.65f;

        // More tolerant / more stressed -> more sprint
        float threatBoost = 0f;
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.Mid) threatBoost = 0.15f;
        if (threat.CurrentThreat == BotThreatModel.ThreatLevel.High) threatBoost = 0.35f;

        // disciplined bots sprint less (especially in preferQuiet)
        float disciplineFactor = Mathf.Lerp(1.2f, 0.35f, noiseDiscipline);
        float riskFactor = Mathf.Lerp(0.7f, 1.15f, riskTolerance);

        float p = Mathf.Clamp01((baseProb + threatBoost) * disciplineFactor * riskFactor);

        // Must have stamina
        if (stamina < 1.4f) p *= 0.2f;

        if (Random.value < p)
            ForceSprintBurst();
        else
            ApplyLocomotion(LocomotionMode.Walk);
    }

    private void EmitNoiseSafe(float loudness)
    {
        loudness = Mathf.Clamp01(loudness);
        
        // 1. Invoke local hook (if any external system is listening)
        if (EmitNoise != null)
            EmitNoise((Vector2)transform.position, loudness);

        // 2. ? FIX: Directly publish to Global Noise Bus so Enemy can hear!
        if (AITest.Core.NoiseBus.Instance != null)
        {
            // Convert loudness (0..1) to radius (e.g. 1.0 = 25m)
            float radius = loudness * 25.0f; 
            
            // Note: NoiseBus usually handles Sector calculation internally or accepts it
            // Assuming method name is 'EmitNoise' or 'PublishNoise'. 
            // Based on common patterns in this project, let's try 'EmitNoise'.
            // If compilation fails, we might need to check the exact name.
            // Looking at Perception.cs, it subscribes to OnNoise.
            // Let's use Reflection or assume 'EmitNoise' for now based on context.
            // Method is named 'Emit', signature: Emit(Vector2 pos, float radius, string sectorId, bool isGlobal)
            AITest.Core.NoiseBus.Instance.Emit((Vector2)transform.position, radius, "", false);
        }
    }

    // -------------------------
    // HELPERS
    // -------------------------

    private bool TryPickHideSpot(out HideSpot spot)
    {
        spot = null;
        float bestScore = float.NegativeInfinity;
        Vector2 p = transform.position;
        
        // If enemy is null, treat enemy pos as far away
        Vector2 enemyPos = threat.enemy ? (Vector2)threat.enemy.position : (p + Vector2.right * 100f);
        bool hasEnemy = threat.enemy != null;

        foreach (var hs in hideSpots)
        {
            if (hs == null) continue;
            if (hs.IsOccupied) continue;

            float distToSpot = Vector2.Distance(p, hs.transform.position);
            
            // Too far (don't run across map)
            if (distToSpot > 25.0f) continue;

            float distFromEnemy = Vector2.Distance(enemyPos, hs.transform.position);
            
            // SAFETY CHECK 1: Don't pick spot if it is CLOSER to enemy than to us (unless we are very close to it)
            if (hasEnemy && distFromEnemy < distToSpot && distToSpot > 3.0f) continue;
            
            // SAFETY CHECK 2: Don't run INTO the enemy
            if (hasEnemy)
            {
                // Vector from me to spot
                Vector2 toSpot = (hs.transform.position - transform.position).normalized;
                // Vector from me to enemy
                Vector2 toEnemy = (threat.enemy.position - transform.position).normalized;
                
                float dot = Vector2.Dot(toSpot, toEnemy);
                
                // If angle is small (running towards enemy), penalize heavily or reject
                if (dot > 0.6f && distToSpot > 2.0f) continue; // Running towards enemy -> REJECT
            }

            // SCORE
            // Base: Further from enemy is better, Closer to me is better
            float score = (distFromEnemy * 1.5f) - (distToSpot * 1.0f);
            
            // PRIORITY: Local room spots get MASSIVE boost
            if (distToSpot < 5.0f) score += 20.0f; // Strongly prefer local
            
            // VISIBILITY BONUS: If spot is behind a wall relative to enemy, boost it
            if (hasEnemy)
            {
                 // Simple raycast check from Enemy to Spot
                 // If hit wall, it's safer
                 var hit = Physics2D.Linecast(enemyPos, hs.transform.position, LayerMask.GetMask("Obstacles"));
                 if (hit.collider != null) score += 10.0f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                spot = hs;
            }
        }

        return spot != null;
    }

    private SafePoint PickNearestSafePoint()
    {
        SafePoint best = null;
        float bd = float.PositiveInfinity;
        Vector2 p = transform.position;

        foreach (var sp in safePoints)
        {
            if (sp == null) continue;
            float d = Vector2.Distance(p, sp.transform.position);
            if (d < bd)
            {
                bd = d;
                best = sp;
            }
        }
        return best;
    }

    private SafePoint PickFarthestSafePointFromEnemy()
    {
        if (threat.enemy == null)
            return PickNearestSafePoint();

        SafePoint best = null;
        float bestScore = float.NegativeInfinity;
        Vector2 enemyPos = threat.enemy.position;

        foreach (var sp in safePoints)
        {
            if (sp == null) continue;
            float distEnemy = Vector2.Distance((Vector2)sp.transform.position, enemyPos);
            float distBot = Vector2.Distance((Vector2)sp.transform.position, transform.position);
            float score = distEnemy - 0.25f * distBot;
            if (score > bestScore)
            {
                bestScore = score;
                best = sp;
            }
        }
        return best;
    }

    private void OnDrawGizmos()
    {
        if (!debugGizmos) return;

        Gizmos.color = Color.cyan;
        if (currentQuest != null) Gizmos.DrawWireSphere(currentQuest.transform.position, 0.25f);

        Gizmos.color = Color.magenta;
        if (currentHide != null) Gizmos.DrawWireSphere(currentHide.transform.position, 0.25f);

        Gizmos.color = Color.green;
        if (currentSafe != null) Gizmos.DrawWireSphere(currentSafe.transform.position, 0.25f);
    }
}
