namespace AITest.Enemy
{
    /// <summary>
    /// IEnemyOption - Base interface for SMDP options
    /// 
    /// PROMPT 6: Option lifecycle
    /// - Start(): Initialize option
    /// - Tick(): Execute one frame (returns status)
    /// - Stop(): Cleanup (interrupt or complete)
    /// 
    /// INTERRUPT RULES:
    /// - SeePlayer: Interrupts everything (highest priority)
    /// - HearNoise: Interrupts Patrol, but not Sweep/HideSpotCheck
    /// - HeatUpdate: Does NOT interrupt (passive update)
    /// 
    /// COMMITMENT:
    /// - MinCommitTime: Minimum time before option can be interrupted
    /// - Exception: SeePlayer always interrupts (instant)
    /// </summary>
    public interface IEnemyOption
    {
        /// <summary>
        /// Mode identifier
        /// </summary>
        EnemyMode Mode { get; }

        /// <summary>
        /// Maximum duration (seconds) before forced timeout
        /// </summary>
        float MaxDuration { get; }

        /// <summary>
        /// Minimum commit time (seconds) - cannot interrupt before this
        /// Exception: SeePlayer always interrupts
        /// </summary>
        float MinCommitTime { get; }

        /// <summary>
        /// Initialize option execution
        /// </summary>
        /// <param name="ctx">Enemy context (worldModel, pathfinder, etc.)</param>
        void Initialize(EnemyContext ctx);

        /// <summary>
        /// Tick option (called every frame)
        /// </summary>
        /// <param name="ctx">Enemy context</param>
        /// <param name="dt">Delta time</param>
        /// <returns>Running/Succeeded/Failed</returns>
        OptionStatus Tick(EnemyContext ctx, float dt);

        /// <summary>
        /// Stop option (cleanup on interrupt or completion)
        /// </summary>
        /// <param name="ctx">Enemy context</param>
        void Stop(EnemyContext ctx);

        /// <summary>
        /// Get elapsed time since option started
        /// </summary>
        float ElapsedTime { get; }

        /// <summary>
        /// Can this option be interrupted by the given interrupt type?
        /// </summary>
        bool CanBeInterruptedBy(InterruptType interruptType);
    }

    /// <summary>
    /// Interrupt types (priority-based)
    /// </summary>
    public enum InterruptType
    {
        None = 0,           // No interrupt
        HeatUpdate = 1,     // Heatmap update (passive - no interrupt)
        HearNoise = 2,      // Noise heard (medium priority)
        SeePlayer = 3       // Player visible (highest priority - always interrupts)
    }
}
