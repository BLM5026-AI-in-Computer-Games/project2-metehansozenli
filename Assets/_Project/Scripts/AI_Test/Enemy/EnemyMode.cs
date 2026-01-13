namespace AITest.Enemy
{
    /// <summary>
    /// Enemy Mode - SMDP Options (8 behaviors)
    /// 
    /// PROMPT 6 + HEAT SYSTEM: High-level behavior modes
    /// - Patrol: Heatmap-based patrol
    /// - InvestigateLastHeard: Go to last clue (see/hear)
    /// - HeatSearchPeak: Go to heatmap peak
    /// - SweepArea: Systematic room sweep
    /// - HideSpotCheck: Check high-probability hide spots
    /// - HeatSweep: Follow hot route chain and sweep (NEW)
    /// - AmbushHotChoke: Ambush at high-traffic choke point (NEW)
    /// </summary>
    public enum EnemyMode
    {
        Patrol = 0,
        InvestigateLastHeard = 1,
        HeatSearchPeak = 2,
        SweepArea = 3,
        HideSpotCheck = 4,
        HeatSweep = 5,          // NEW: Room transition heat-based sweep
        AmbushHotChoke = 6      // NEW: Ambush at choke points
    }

    /// <summary>
    /// Option execution status
    /// </summary>
    public enum OptionStatus
    {
        Running = 0,    // Option still executing
        Succeeded = 1,  // Option completed successfully
        Failed = 2      // Option failed (timeout, no path, etc.)
    }
}
