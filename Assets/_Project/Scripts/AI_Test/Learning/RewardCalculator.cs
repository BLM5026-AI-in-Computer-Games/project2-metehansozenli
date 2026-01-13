using UnityEngine;
using System.Collections.Generic;
using AITest.Enemy;
using AITest.Heat;
using AITest.World;

namespace AITest.Learning
{
    /// <summary>
    /// Reward Calculator - Reward shaping for Q-learning
    /// 
    /// PROMPT 10: Dense reward signal (✅ VALUES REDUCED 10x for Q-stability)
    /// - Player seen: +1 (was +10)
    /// - Distance reduction: +0.01 to +0.1 (was +0.1 to +1.0)
    /// - Unsearched room: +0.2 (was +2)
    /// - Action repetition: -0.1 (was -1)
    /// - Capture: +10 (was +50)
    /// - Timeout: -0.5 (was -5)
    /// </summary>
    public class RewardCalculator : MonoBehaviour
    {
        [Header("Reward Values")]
        [Tooltip("Reward for seeing player")]
        [Range(0.1f, 5f)] public float seePlayerReward = 1.0f; 
        
        [Tooltip("Reward for capturing player")]
        [Range(1f, 50f)] public float captureReward = 10f; // Handled by EpisodeManager
        
        [Tooltip("Reward for entering unsearched room")]
        [Range(0f, 1f)] public float unsearchedRoomReward = 0.2f;
        
        [Tooltip("Reward per meter of distance reduced (to heat peak/last heard)")]
        [Range(0f, 0.5f)] public float distanceReductionReward = 0.2f;  // ✅ STRONGER (0.01 -> 0.2)
        
        [Tooltip("Penalty for repeating same action")]
        [Range(0f, 1f)] public float actionRepetitionPenalty = 0.1f;
        
        [Tooltip("Penalty for timeout")]
        [Range(0f, 2f)] public float timeoutPenalty = 0.5f;
        
        [Tooltip("Penalty per second elapsed (efficiency)")]
        [Range(0f, 0.05f)] public float timePenaltyPerSecond = 0.005f; // ✅ Reduced 0.01 -> 0.005

        [Header("Debug")]
        public List<(string reason, float value)> LastRewardBreakdown = new List<(string, float)>();



        /// <summary>
        /// Get reward breakdown (for debugging) - Uses the ACTUAL calculated values
        /// </summary>
        public string GetRewardBreakdown(
            OptionStatus status,
            EnemyMode action,
            float duration,
            EnemyContext ctx)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("| ");
            foreach (var item in LastRewardBreakdown)
            {
                sb.Append($"{item.reason}: {item.value:+0.00;-0.00} | ");
            }
            return sb.ToString();
        }

        [Header("State Tracking")]
        private EnemyMode lastAction = EnemyMode.Patrol;
        private int sameActionCount = 0;
        private float lastDistanceToTarget = float.MaxValue; // Heuristic target

        /// <summary>
        /// ? PROMPT 10: Compute reward for option completion
        /// </summary>
        public float ComputeReward(
            OptionStatus status,
            EnemyMode action,
            float duration,
            EnemyContext ctx)
        {
            float reward = 0f;
            LastRewardBreakdown.Clear();
            
            // ? 1. Success/Failure base reward
            // ❌ REMOVED per User Request: No "participation award" for just finishing a task.
            /*
            if (status == OptionStatus.Succeeded)
            {
                float val = 0.5f; 
                reward += val;
                LastRewardBreakdown.Add(($"Task Complete ({action})", val));
            }
            */
            if (status == OptionStatus.Succeeded && action == EnemyMode.Patrol)
            {
                float val = 0.25f; // Completion reward to balance time cost
                reward += val;
                LastRewardBreakdown.Add(("Patrol Complete", val));
            }

            if (status == OptionStatus.Failed)
            {
                float val = -timeoutPenalty;
                reward += val;
                LastRewardBreakdown.Add(("Failure/Timeout", val));
            }
            
            // ? 2. Player visibility reward
            bool gainedInfo = false;
            
            if (ctx.CanSeePlayer())
            {
                float val = seePlayerReward;
                reward += val;
                LastRewardBreakdown.Add(("See Player", val));
                gainedInfo = true;
            }
            
            // ? 3a. Heuristic Distance reduction (Intention)
            // Rewards moving towards where it THINKS the player is (Heat/Sound)
            float currentDistance = GetDistanceToTarget(ctx);
            bool distanceImproved = false; // ✅ FIX: Declare variable
            
            if (lastDistanceToTarget < float.MaxValue)
            {
                float distanceReduced = lastDistanceToTarget - currentDistance;
                if (distanceReduced > 0f)
                {
                    float val = distanceReduced * distanceReductionReward;
                    reward += val;
                    // distanceImproved = true; // Still buggy because we declare it in prompt? Let's just fix the logic visually
                    distanceImproved = true;
                }
            }
            lastDistanceToTarget = currentDistance;

            // ? 3b. REAL Distance reduction -> REMOVED (Cheating)

            // ? 4. Unsearched room reward
            string currentRoom = ctx.GetCurrentRoom();
            if (currentRoom != "None" && TransitionHeatGraph.Instance)
            {
                float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(currentRoom);
                float heat01 = Mathf.Clamp01(nodeHeat / TransitionHeatGraph.Instance.maxHeatCap);
                if (heat01 > 0.5f)
                {
                    float val = unsearchedRoomReward;
                    reward += val;
                    LastRewardBreakdown.Add(("Hot room exploration", val));
                    gainedInfo = true;
                }
            }
            
            // ? 4b. Unique Room Visit Reward (Exploration)
            // Reward for visiting a room for the first time in this episode
            if (currentRoom != "None" && !visitedRooms.Contains(currentRoom))
            {
                float val = 0.15f; // Small but meaningful incentive to Patrol
                reward += val;
                visitedRooms.Add(currentRoom);
                LastRewardBreakdown.Add(("New Room Explored", val));
                gainedInfo = true;
            }
            
            // ? 5. Hide Spot Check Reward (Investigation)
            // Reward for clearing a potential hiding spot (Information Gain)
            if (action == EnemyMode.HideSpotCheck && status == OptionStatus.Succeeded)
            {
                float val = 0.4f; // Equivalent to ~40% of seeing a player
                reward += val;
                LastRewardBreakdown.Add(("Spot Cleared", val));
            }
            
            // ? 5. Action repetition penalty
            if (action == lastAction)
            {
                sameActionCount++;
                
                if (sameActionCount > 2)
                {
                    // Penalty increases with repetitions
                    float multiplier = (sameActionCount - 2);
                    float val = -(actionRepetitionPenalty * multiplier);
                    
                    // Strong discouragement for loop lock (>5 times)
                    if (sameActionCount > 5) val *= 2.5f; 

                    reward += val;
                    LastRewardBreakdown.Add(($"Repetition penalty (x{multiplier})", val));
                }
            }
            else
            {
                sameActionCount = 0;
            }
            
            // ? 6. Time efficiency penalty (Small constant drag)
            float timePen = -(duration * timePenaltyPerSecond);
            if (Mathf.Abs(timePen) > 0.01f)
            {
                reward += timePen;
                LastRewardBreakdown.Add(($"Time cost ({duration:F1}s)", timePen));
            }

            // ? 7. "No-Info" Penalty (NEW)
            // If we didn't see player, didn't improve distance, and didn't find hot room/new room
            // Penalty to discourage useless walking/sweeping
            if (!gainedInfo && !distanceImproved && (status == OptionStatus.Running || status == OptionStatus.Succeeded))
            {
                 float val = -0.15f; // Slightly increased penalty (-0.1 -> -0.15)
                 reward += val;
                 LastRewardBreakdown.Add(("No info gained", val));
            }
            
            // ✅ CRITICAL FIX: Update lastAction so repetition check works next time!
            lastAction = action;

            return reward;
        }

        /// <summary>
        /// Get distance to current target (heat peak or last heard)
        /// </summary>
        private float GetDistanceToTarget(EnemyContext ctx)
        {
            Vector2 targetPos;
            
            // Priority: LastSeen > LastHeard > HeatPeak
            if (ctx.worldModel.HasSeenRecently)
            {
                targetPos = ctx.worldModel.LastSeenPos;
            }
            else if (ctx.worldModel.HasHeardRecently)
            {
                targetPos = ctx.worldModel.LastHeardPos;
            }
            else
            {
                // Fallback: center of hottest room by node heat
                targetPos = Vector2.zero;
                if (TransitionHeatGraph.Instance)
                {
                    string peakRoom = TransitionHeatGraph.Instance.GetPeakRoom();
                    var room = WorldRegistry.Instance ? WorldRegistry.Instance.GetRoom(peakRoom) : null;
                    if (room != null)
                        targetPos = room.Center;
                }
            }
            
            if (targetPos == Vector2.zero)
                return float.MaxValue;
            
            return Vector2.Distance(ctx.Position, targetPos);
        }

        /// <summary>
        /// Reset state tracking
        /// </summary>
        public void Reset()
        {
            lastAction = EnemyMode.Patrol;
            sameActionCount = 0;
            lastDistanceToTarget = float.MaxValue;
            visitedRooms.Clear(); // ✅ Reset exploration memory
        }

        // Exploration memory
        private HashSet<string> visitedRooms = new HashSet<string>();

        /// <summary>
        /// Get reward breakdown (for debugging)
        /// </summary>

    }
}
