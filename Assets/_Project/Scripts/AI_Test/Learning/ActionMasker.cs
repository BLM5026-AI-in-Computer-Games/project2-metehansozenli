using UnityEngine;
using AITest.Enemy;
using System.Collections.Generic;

namespace AITest.Learning
{
    /// <summary>
    /// Action Masker - Filters invalid actions based on State
    /// UPDATED: Supports optimized 162-state architecture
    /// </summary>
    public class ActionMasker : MonoBehaviour
    {
        [Header("Debug")]
        public bool showDebugLogs = false;

        /// <summary>
        /// Get valid actions for the current state
        /// </summary>
        public List<EnemyMode> GetValidActions(SimpleRLStateKey state)
        {
            List<EnemyMode> validActions = new List<EnemyMode>();

            // Always valid (Fallback)
            validActions.Add(EnemyMode.Patrol);

            // --- 1. ALWAYS ALLOWED (Autonomy) ---
            // Allow checking room or spots anytime. If it's a waste of time, RL will learn negative reward.
            validActions.Add(EnemyMode.SweepArea);
            validActions.Add(EnemyMode.HideSpotCheck);

            // --- 2. PLAYER PRESENCE LOGIC ---
            // If Visible (0) -> Focus on Chase/Investigate
            if (state.playerPresence == 0) 
            {
                validActions.Add(EnemyMode.InvestigateLastHeard);
                // In visible state, we usually don't want to sweep/hide check, but let's allow Q-net to decide too?
                // No, when visible, acting dumb is fatal. Keep strict for Visible only.
                return new List<EnemyMode> { EnemyMode.InvestigateLastHeard, EnemyMode.Patrol }; 
            }
            
            // If Heard Recent (1)
            if (state.playerPresence == 1) 
            {
                validActions.Add(EnemyMode.InvestigateLastHeard);
            }

            // --- 3. HEAT LOGIC (Relaxed) ---
            // --- 3. HEAT LOGIC (Relaxed) ---
            // Allow Choke Ambush / Heat Sweep if nearby is Warm(1)
            if (state.heatNearby >= 1 || state.heatHere >= 1)
            {
                validActions.Add(EnemyMode.HeatSweep);     
                validActions.Add(EnemyMode.AmbushHotChoke); 
            }
            
            // --- 4. GLOBAL AWARENESS ---
            // HeatSearchPeak REMOVED (Redundant with HeatSweep)
            // validActions.Add(EnemyMode.HeatSearchPeak);
            
            // --- 5. PHASE LOGIC ---
            if (state.strategicPhase == 1) // Panic/EndGame
            {
               // Allow everything basically
               if (!validActions.Contains(EnemyMode.AmbushHotChoke)) validActions.Add(EnemyMode.AmbushHotChoke);
            }

            return validActions;
        }

        /// <summary>
        /// Get valid action INDICES (for Q-Table lookup)
        /// </summary>
        public List<int> GetValidActionIndices(SimpleRLStateKey state)
        {
            var actions = GetValidActions(state);
            List<int> indices = new List<int>();
            foreach (var act in actions)
            {
                indices.Add((int)act);
            }
            return indices;
        }
    }
}
