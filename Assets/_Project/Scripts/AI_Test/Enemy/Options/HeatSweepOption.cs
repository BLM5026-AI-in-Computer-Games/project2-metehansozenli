using UnityEngine;
using System.Collections.Generic;
using AITest.Heat;

namespace AITest.Enemy
{
    /// <summary>
    /// Heat Sweep Option - Follow hot route chain and sweep areas
    /// 
    /// Strategy:
    /// - From current room, generate hot chain (3-6 steps)
    /// - Move to final node in chain
    /// - Sweep that room when arrived
    /// 
    /// Completion:
    /// - Reached destination and swept
    /// - Timeout after N seconds
    /// - Player spotted (interrupt)
    /// </summary>
    public class HeatSweepOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.HeatSweep;
        
        [Header("Heat Sweep Settings")]
        [Tooltip("Hot chain length (number of rooms to follow)")]
        [Range(1, 6)]
        public int chainLength = 3;
        
        [Tooltip("Sweep duration at target room (seconds)")]
        [Range(1f, 10f)]
        public float sweepDuration = 3f;
        
        [Tooltip("Timeout if can't reach target (seconds)")]
        [Range(10f, 60f)]
        public float timeout = 30f;
        
        [Tooltip("Arrival threshold (meters)")]
        [Range(1f, 5f)]
        public float arrivalThreshold = 3f;
        
        // Components
        private AIAgentMover mover;
        private EnemyRoomTracker roomTracker;
        
        // State
        private string targetRoomId;
        private Vector2 targetPosition;
        private bool sweeping;
        private float sweepStartTime;
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
            
            // Get current room
            string currentRoom = roomTracker.GetCurrentRoom();
            
            if (string.IsNullOrEmpty(currentRoom))
            {
                // Fallback: use WorldRegistry to find nearest room
                currentRoom = ctx.GetCurrentRoom();
            }
            
            if (string.IsNullOrEmpty(currentRoom) || currentRoom == "None")
            {
                Log("Can't determine current room!", "orange");
                return;
            }
            
            // Generate hot chain
            var hotChain = TransitionHeatGraph.Instance.GetHotChainFrom(currentRoom, chainLength);
            
            if (hotChain.Count == 0)
            {
                Log($"No hot chain from room {currentRoom}", "orange");
                return;
            }
            
            // Target = last room in chain
            targetRoomId = hotChain[hotChain.Count - 1];
            
            // Get target position (room center)
            targetPosition = GetRoomPosition(targetRoomId);
            
            if (targetPosition == Vector2.zero)
            {
                Log($"Can't find position for room {targetRoomId}!", "red");
                return;
            }
            
            // Start moving
            mover.SetDestination(targetPosition);
            
            sweeping = false;
            optionStartTime = Time.time;
            
            Log($"Heat sweep started: {currentRoom} → [{string.Join(" → ", hotChain)}] (target: {targetRoomId})");
        }
        
        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            if (mover == null || string.IsNullOrEmpty(targetRoomId))
                return OptionStatus.Failed;
            
            // Timeout check
            if (Time.time - optionStartTime > timeout)
            {
                Log("Heat sweep timeout", "yellow");
                return OptionStatus.Failed;
            }
            
            if (!sweeping)
            {
                // Check if arrived at target
                float distToTarget = Vector2.Distance(ctx.Position, targetPosition);
                
                if (distToTarget < arrivalThreshold || mover.ReachedDestination)
                {
                    // Arrived - start sweeping
                    mover.Stop();
                    sweeping = true;
                    sweepStartTime = Time.time;
                    
                    Log($"Arrived at {targetRoomId}, sweeping...", "cyan");
                }
            }
            else
            {
                // Sweeping
                float sweepElapsed = Time.time - sweepStartTime;
                
                if (sweepElapsed >= sweepDuration)
                {
                    Log($"Heat sweep complete at {targetRoomId}", "lime");
                    return OptionStatus.Succeeded;
                }
            }
            
            return OptionStatus.Running;
        }
        
        protected override void OnStop(EnemyContext ctx)
        {
            if (mover)
                mover.Stop();
            
            Log($"Heat sweep stopped (sweeping={sweeping})");
        }
        
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // Can be interrupted by seeing player
            return interruptType == InterruptType.SeePlayer;
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
