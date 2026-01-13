using UnityEngine;

namespace AITest.Enemy
{
    /// <summary>
    /// Investigate Option - Go to last clue (see/hear)
    /// 
    /// PROMPT 9 COMPLETE:
    /// - Go to lastSeen/lastHeard position
    /// - Real-time chase if player visible
    /// - Scan after arrival
    /// - Mark room searched
    /// </summary>
    public class InvestigateOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.InvestigateLastHeard;
        
        [Header("Investigate Settings")]
        [Tooltip("Scan duration after arrival (seconds)")]
        [Range(0.5f, 5f)] public float scanDuration = 2f;
        
        [Tooltip("Chase distance threshold (meters) - if player closer, keep chasing")]
        [Range(1f, 5f)] public float chaseStopDistance = 2f;
        
        // Components
        private AIAgentMover mover;
        
        // Investigation state
        private Vector2 targetPosition;
        private string targetRoom;
        private bool isChasing;
        private bool scanning;
        private float scanStartTime;

        protected override void OnStart(EnemyContext ctx)
        {
            // Get components
            mover = GetComponent<AIAgentMover>();
            
            if (!mover)
            {
                Log("Missing AIAgentMover!", "red");
                return;
            }
            
            // ? PROMPT 9: Determine target (priority: see > hear > best guess)
            if (ctx.worldModel.HasSeenRecently)
            {
                targetPosition = ctx.worldModel.LastSeenPos;
                targetRoom = ctx.worldModel.LastSeenRoom;
                Log($"Investigating SEEN position @ {targetPosition} (room {targetRoom})", "lime");
            }
            else if (ctx.worldModel.HasHeardRecently)
            {
                targetPosition = ctx.worldModel.LastHeardPos;
                targetRoom = ctx.worldModel.LastHeardRoom;
                Log($"Investigating HEARD position @ {targetPosition} (room {targetRoom})", "yellow");
            }
            else
            {
                // No recent clue - use best guess
                targetPosition = ctx.GetBestGuessPosition();
                targetRoom = ctx.GetBestGuessRoom();
                Log($"Investigating GUESS position @ {targetPosition} (room {targetRoom})", "orange");
            }
            
            isChasing = ctx.CanSeePlayer();
            scanning = false;
            
            // ? Move to target
            mover.SetDestination(targetPosition);
        }

        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            // ? Real-time chase if player visible
            if (ctx.CanSeePlayer())
            {
                isChasing = true;
                Vector2 playerPos = ctx.GetPlayerPosition();
                
                // Update target to player position
                targetPosition = playerPos;
                
                Log($"CHASE mode! Player at {playerPos}", "red");
                
                // ? Update destination (frequent replan for chase)
                mover.SetDestination(playerPos);
                
                // Check if caught player
                if (mover.IsAtPosition(playerPos, chaseStopDistance))
                {
                    Log("CAUGHT PLAYER!", "lime");
                    return OptionStatus.Succeeded;
                }
                
                // Keep chasing
                return OptionStatus.Running;
            }
            
            // ? Not chasing - check if scanning
            if (scanning)
            {
                float scanElapsed = Time.time - scanStartTime;
                
                if (scanElapsed >= scanDuration)
                {
                    // Scan complete - mark room searched
                    Log($"Investigation complete (room {targetRoom} scanned)", "lime");
                    
                    if (ctx.worldModel && targetRoom != "None")
                    {
                        ctx.worldModel.MarkRoomSearched(targetRoom);
                    }
                    
                    return OptionStatus.Succeeded;
                }
            }
            else
            {
                // ? Check arrival at target
                if (mover.ReachedDestination)
                {
                    // Arrived - start scanning
                    Log($"Arrived at clue position, scanning...", "yellow");
                    
                    scanning = true;
                    scanStartTime = Time.time;
                    
                    // Stop movement during scan
                    mover.Stop();
                }
            }
            
            return OptionStatus.Running;
        }

        protected override void OnStop(EnemyContext ctx)
        {
            if (mover)
                mover.Stop();
            
            Log($"Investigate stopped (chase={isChasing}, scanning={scanning})");
        }

        /// <summary>
        /// INTERRUPT RULE: Cannot be interrupted by HearNoise (committed to investigation)
        /// </summary>
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // Only SeePlayer can interrupt (after min commit time)
            return base.CanBeInterruptedBy(interruptType);
        }
    }
}
