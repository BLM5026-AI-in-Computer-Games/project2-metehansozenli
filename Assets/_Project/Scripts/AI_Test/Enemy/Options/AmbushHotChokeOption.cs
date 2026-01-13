using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AITest.Heat;

namespace AITest.Enemy
{
    /// <summary>
    /// Ambush Hot Choke Option - Move to high-traffic choke point and wait
    /// 
    /// Strategy:
    /// - Query top K choke points from heat graph
    /// - Select best one based on distance + heat score
    /// - Move there and wait 2-4 seconds
    /// 
    /// Completion:
    /// - Wait duration elapsed
    /// - Player spotted (interrupt)
    /// - Timeout
    /// </summary>
    public class AmbushHotChokeOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.AmbushHotChoke;
        
        [Header("Ambush Settings")]
        [Tooltip("Number of top choke points to consider")]
        [Range(1, 5)]
        public int topChokeCount = 3;
        
        [Tooltip("Wait duration at choke point (min)")]
        [Range(1f, 5f)]
        public float waitDurationMin = 2f;
        
        [Tooltip("Wait duration at choke point (max)")]
        [Range(2f, 10f)]
        public float waitDurationMax = 4f;
        
        [Tooltip("Distance weight (prefer closer chokes)")]
        [Range(0f, 1f)]
        public float distanceWeight = 0.3f;
        
        [Tooltip("Heat weight (prefer hotter chokes)")]
        [Range(0f, 1f)]
        public float heatWeight = 0.7f;
        
        [Tooltip("Timeout if can't reach choke point (seconds)")]
        [Range(10f, 60f)]
        public float timeout = 30f;
        
        [Tooltip("Arrival threshold (meters)")]
        [Range(1f, 5f)]
        public float arrivalThreshold = 2f;
        
        // Components
        private AIAgentMover mover;
        private EnemyRoomTracker roomTracker;
        
        // State
        private string targetChokeRoomId;
        private Vector2 targetPosition;
        private bool waiting;
        private float waitStartTime;
        private float waitDuration;
        private float optionStartTime;
        
        protected override void OnStart(EnemyContext ctx)
        {
            // Get components
            mover = GetComponent<AIAgentMover>();
            roomTracker = GetComponent<EnemyRoomTracker>();
            
            if (!mover || !roomTracker)
            {
                Log("Missing components (AIAgentMover or EnemyRoomTracker)!", "red");
                return;
            }
            
            if (TransitionHeatGraph.Instance == null)
            {
                Log("TransitionHeatGraph not found in scene!", "red");
                return;
            }
            
            // Get top choke points
            var topChokes = TransitionHeatGraph.Instance.GetTopChokePoints(topChokeCount);
            
            if (topChokes.Count == 0)
            {
                Log("No choke points available!", "orange");
                return;
            }
            
            // Select best choke based on distance + heat
            targetChokeRoomId = SelectBestChoke(topChokes, ctx.Position);
            
            if (string.IsNullOrEmpty(targetChokeRoomId))
            {
                Log("Failed to select choke point!", "red");
                return;
            }
            
            // Get target position
            targetPosition = GetRoomPosition(targetChokeRoomId);
            
            if (targetPosition == Vector2.zero)
            {
                Log($"Can't find position for choke room {targetChokeRoomId}!", "red");
                return;
            }
            
            // Start moving
            mover.SetDestination(targetPosition);
            
            waiting = false;
            waitDuration = Random.Range(waitDurationMin, waitDurationMax);
            optionStartTime = Time.time;
            
            float chokeScore = TransitionHeatGraph.Instance.GetChokeScore(targetChokeRoomId);
            Log($"Ambush started: Moving to choke point {targetChokeRoomId} (score: {chokeScore:F1})");
        }
        
        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            if (mover == null || string.IsNullOrEmpty(targetChokeRoomId))
                return OptionStatus.Failed;
            
            // Timeout check
            if (Time.time - optionStartTime > timeout)
            {
                Log("Ambush timeout", "yellow");
                return OptionStatus.Failed;
            }
            
            if (!waiting)
            {
                // Check if arrived at choke point
                float distToTarget = Vector2.Distance(ctx.Position, targetPosition);
                
                if (distToTarget < arrivalThreshold || mover.ReachedDestination)
                {
                    // Arrived - start waiting
                    mover.Stop();
                    waiting = true;
                    waitStartTime = Time.time;
                    
                    Log($"Arrived at choke {targetChokeRoomId}, waiting {waitDuration:F1}s...", "cyan");
                }
            }
            else
            {
                // Waiting at choke point
                float waitElapsed = Time.time - waitStartTime;
                
                if (waitElapsed >= waitDuration)
                {
                    Log($"Ambush complete at {targetChokeRoomId}", "lime");
                    return OptionStatus.Succeeded;
                }
            }
            
            return OptionStatus.Running;
        }
        
        protected override void OnStop(EnemyContext ctx)
        {
            if (mover)
                mover.Stop();
            
            Log($"Ambush stopped (waiting={waiting})");
        }
        
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // Can be interrupted by seeing player
            return interruptType == InterruptType.SeePlayer;
        }
        
        private string SelectBestChoke(List<string> chokes, Vector2 enemyPosition)
        {
            if (chokes.Count == 0)
                return null;
            
            string bestChoke = null;
            float bestScore = float.NegativeInfinity;
            
            foreach (var choke in chokes)
            {
                Vector2 chokePos = GetRoomPosition(choke);
                if (chokePos == Vector2.zero)
                    continue;
                
                float distance = Vector2.Distance(enemyPosition, chokePos);
                float heatScore = TransitionHeatGraph.Instance.GetChokeScore(choke);
                
                // Normalize and weight
                float distScore = Mathf.Exp(-distance / 10f); // Closer = higher score
                float combinedScore = (distScore * distanceWeight) + (heatScore * heatWeight);
                
                if (combinedScore > bestScore)
                {
                    bestScore = combinedScore;
                    bestChoke = choke;
                }
            }
            
            return bestChoke;
        }
        
        private Vector2 GetRoomPosition(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
                return Vector2.zero;
            
            var room = AITest.World.WorldRegistry.Instance?.GetRoom(roomId);
            if (room != null)
            {
                return room.Center;
            }
            
            return Vector2.zero;
        }
    }
}
