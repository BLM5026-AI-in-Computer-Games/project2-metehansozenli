using UnityEngine;

namespace AITest.Enemy
{
    /// <summary>
    /// Base Option - Common implementation for all options
    /// 
    /// PROMPT 6: Abstract base class
    /// - Handles timing (elapsed, timeout)
    /// - Implements interrupt rules
    /// - Provides utility methods
    /// 
    /// INTERRUPT PRIORITY TABLE:
    /// | Interrupt Type | Patrol | Investigate | HeatSearch | Sweep | HideSpot | Ambush |
    /// |----------------|--------|-------------|------------|-------|----------|--------|
    /// | SeePlayer      | ?     | ?          | ?         | ?    | ?       | ?     |
    /// | HearNoise      | ?     | ?          | ?         | ?    | ?       | ?     |
    /// | HeatUpdate     | ?     | ?          | ?         | ?    | ?       | ?     |
    /// </summary>
    public abstract class BaseOption : MonoBehaviour, IEnemyOption
    {
        [Header("Option Settings")]
        [Tooltip("Maximum duration (seconds) before timeout")]
        public float maxDuration = 15f;
        
        [Tooltip("Minimum commit time (seconds) - cannot interrupt before this")]
        public float minCommitTime = 2f;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Runtime tracking
        protected float startTime;
        protected bool isRunning;
        
        // IEnemyOption implementation
        public abstract EnemyMode Mode { get; }
        public virtual float MaxDuration => maxDuration;
        public virtual float MinCommitTime => minCommitTime;
        public float ElapsedTime => isRunning ? (Time.time - startTime) : 0f;
        
        /// <summary>
        /// Start option (template method)
        /// </summary>
        public void Initialize(EnemyContext ctx)
        {
            startTime = Time.time;
            isRunning = true; // ? CRITICAL: Must set running flag BEFORE OnStart!
            
            // Call derived implementation
            OnStart(ctx);
        }
        
        /// <summary>
        /// Tick option (template method)
        /// </summary>
        public OptionStatus Tick(EnemyContext ctx, float dt)
        {
            if (!isRunning)
                return OptionStatus.Failed;
            
            // Timeout check
            if (ElapsedTime > maxDuration)
            {
                Log($"{Mode} TIMEOUT ({ElapsedTime:F1}s)", "orange");
                return OptionStatus.Failed;
            }
            
            // Call derived class tick
            return OnTick(ctx, dt);
        }
        
        /// <summary>
        /// Stop option (template method)
        /// </summary>
        public void Stop(EnemyContext ctx)
        {
            if (!isRunning) return;
            
            isRunning = false;
            
            Log($"{Mode} STOPPED (elapsed={ElapsedTime:F1}s)", "yellow");
            
            // Call derived class cleanup
            OnStop(ctx);
        }
        
        /// <summary>
        /// PROMPT 6: Interrupt rules (priority table)
        /// </summary>
        public virtual bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // SeePlayer ALWAYS interrupts (highest priority)
            if (interruptType == InterruptType.SeePlayer)
                return true;
            
            // Cannot interrupt if within min commit time
            if (ElapsedTime < minCommitTime && interruptType != InterruptType.SeePlayer)
                return false;
            
            // HeatUpdate NEVER interrupts (passive)
            if (interruptType == InterruptType.HeatUpdate)
                return false;
            
            // HearNoise: Only interrupts Patrol (default = no)
            if (interruptType == InterruptType.HearNoise)
            {
                return Mode == EnemyMode.Patrol;
            }
            
            return false;
        }
        
        /// <summary>
        /// Derived class: Initialize option
        /// </summary>
        protected abstract void OnStart(EnemyContext ctx);
        
        /// <summary>
        /// Derived class: Execute one frame
        /// </summary>
        protected abstract OptionStatus OnTick(EnemyContext ctx, float dt);
        
        /// <summary>
        /// Derived class: Cleanup
        /// </summary>
        protected abstract void OnStop(EnemyContext ctx);
        
        /// <summary>
        /// Utility: Log message
        /// </summary>
        protected void Log(string message, string color = "cyan")
        {
            if (showDebugLogs)
                Debug.Log($"<color={color}>[{Mode}] {message}</color>");
        }
        
        /// <summary>
        /// Utility: Check if at position
        /// </summary>
        protected bool IsAtPosition(Vector2 current, Vector2 target, float threshold = 1f)
        {
            return Vector2.Distance(current, target) < threshold;
        }
        
        /// <summary>
        /// Utility: Get distance to target
        /// </summary>
        protected float DistanceTo(Vector2 current, Vector2 target)
        {
            return Vector2.Distance(current, target);
        }
    }
}
