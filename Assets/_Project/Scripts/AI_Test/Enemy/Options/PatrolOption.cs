using UnityEngine;
using System.Collections.Generic;
using AITest.Learning;

namespace AITest.Enemy
{
    /// <summary>
    /// Patrol Option - Heatmap-based dynamic patrol
    /// 
    /// PROMPT 9 COMPLETE:
    /// - Use TargetSelector for room selection
    /// - Use EnemyMover2D for navigation
    /// - Scan at each waypoint
    /// - Respect MinCommitTime/MaxDuration
    /// </summary>
    public class PatrolOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.Patrol;
        
        [Header("Patrol Settings")]
        [Tooltip("Number of rooms to visit per patrol cycle")]
        [Range(1, 5)] public int waypointsPerCycle = 3;
        
        [Tooltip("Scan duration at each waypoint (seconds)")]
        [Range(0.5f, 5f)] public float scanDuration = 2f;
        
        // Components
        private AIAgentMover mover;
        private TargetSelector targetSelector;
        
        // State
        private List<RoomTarget> patrolRoute;
        private int currentWaypointIndex;
        private bool scanning;
        private float scanStartTime;
        
        private void Awake()
        {
            // Increase timeout for patrol (needs time to visit rooms)
            maxDuration = 45f;
        }

        protected override void OnStart(EnemyContext ctx)
        {
            // Get components
            mover = GetComponent<AIAgentMover>();
            targetSelector = GetComponent<TargetSelector>();
            
            if (!mover || !targetSelector)
            {
                Log("Missing components!", "red");
                return;
            }
            
            // ? PROMPT 9: Select patrol route using perceptron
            patrolRoute = targetSelector.SelectPatrolRoute(ctx, waypointsPerCycle);
            
            if (patrolRoute == null || patrolRoute.Count == 0)
            {
                Log("No patrol route available!", "red");
                return;
            }
            
            currentWaypointIndex = 0;
            scanning = false;
            
            Log($"Patrol route: {string.Join(" ? ", patrolRoute.ConvertAll(r => r.roomId))}");
            
            // ? Move to first waypoint
            MoveToCurrentWaypoint(ctx);
        }

        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            if (patrolRoute == null || patrolRoute.Count == 0)
                return OptionStatus.Failed;
            
            var currentTarget = patrolRoute[currentWaypointIndex];
            
            // ? Check if scanning
            if (scanning)
            {
                float scanElapsed = Time.time - scanStartTime;
                
                if (scanElapsed >= scanDuration)
                {
                    // Scan complete
                    Log($"Scan complete at {currentTarget.roomId}");
                    
                    // Mark room as searched (cool down heatmap)
                    if (ctx.worldModel)
                    {
                        ctx.worldModel.MarkRoomSearched(currentTarget.roomId);
                    }
                    
                    currentWaypointIndex++;
                    
                    // ? Check if patrol cycle complete
                    if (currentWaypointIndex >= patrolRoute.Count)
                    {
                        Log("Patrol cycle complete!", "lime");
                        return OptionStatus.Succeeded;
                    }
                    
                    // Move to next waypoint
                    scanning = false;
                    MoveToCurrentWaypoint(ctx);
                }
            }
            else
            {
                // ? Check arrival
                if (mover.ReachedDestination)
                {
                    // Arrived at waypoint - start scanning
                    Log($"Arrived at {currentTarget.roomId}, scanning...", "yellow");
                    
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
            
            Log($"Patrol stopped ({currentWaypointIndex}/{patrolRoute?.Count ?? 0} waypoints visited)");
        }

        /// <summary>
        /// Move to current waypoint
        /// </summary>
        private void MoveToCurrentWaypoint(EnemyContext ctx)
        {
            if (currentWaypointIndex >= patrolRoute.Count)
                return;
            
            var target = patrolRoute[currentWaypointIndex];
            
            Log($"Moving to waypoint {currentWaypointIndex + 1}/{patrolRoute.Count}: {target.roomId}");
            
            // ? PROMPT 9: Use EnemyMover2D
            mover.SetDestination(target.position);
        }

        /// <summary>
        /// INTERRUPT RULE: HearNoise CAN interrupt Patrol
        /// </summary>
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            if (interruptType == InterruptType.HearNoise)
                return true; // Patrol can be interrupted by noise
            
            return base.CanBeInterruptedBy(interruptType);
        }

        /// <summary>
        /// Get description of the current route (for debug logs)
        /// </summary>
        public string GetRouteDescription()
        {
            if (patrolRoute == null || patrolRoute.Count == 0)
                return "None";
            
            return string.Join(" -> ", patrolRoute.ConvertAll(r => r.roomId));
        }
    }
}
