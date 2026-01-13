using UnityEngine;
using System.Collections.Generic;
using AITest.Learning;
using AITest.Enemy;
// using AITest.World; // ❌ REMOVED: No longer using IntersectionPoints

namespace AITest.Learning
{
    /// <summary>
    /// Action Masker - Filter invalid actions for stable learning
    /// 
    /// PROMPT 16: Critical for Q-learning stability
    /// - InvestigateLastHeard invalid if HearRecently==0
    /// - HeatSearchPeak invalid if HeatConfidence==0
    /// - HideSpotCheck invalid if NearHideSpots==0
    /// - HeatSweep invalid if HeatConfidence < 1
    /// - AmbushHotChoke invalid if HeatConfidence < 2 (uses RoomTracker/TransitionHeatGraph)
    /// - Patrol always valid (fallback)
    /// 
    /// ❌ CutoffAmbush REMOVED: No longer using IntersectionPoints
    /// </summary>
    public class ActionMasker : MonoBehaviour
    {
        [Header("Masking Rules")]
        [Tooltip("Enable action masking")]
        public bool enableMasking = true;
        
        [Tooltip("Always keep Patrol valid (fallback)")]
        public bool patrolAlwaysValid = true;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Mask reasons (for debug)
        private Dictionary<EnemyMode, string> maskReasons = new Dictionary<EnemyMode, string>();
        
        /// <summary>
        /// ? PROMPT 16: Get valid actions for current state
        /// </summary>
        public List<EnemyMode> GetValidActions(RLStateKey state)
        {
            maskReasons.Clear();
            
            if (!enableMasking)
            {
                // No masking - all actions valid (7 total - NO IntersectionPoint actions)
                return new List<EnemyMode>
                {
                    EnemyMode.Patrol,
                    EnemyMode.InvestigateLastHeard,
                    EnemyMode.HeatSearchPeak,
                    EnemyMode.SweepArea,
                    EnemyMode.HideSpotCheck,
                    // EnemyMode.CutoffAmbush,  // ❌ REMOVED: IntersectionPoint dependency
                    EnemyMode.HeatSweep,        // ✅ RoomTracker-based
                    EnemyMode.AmbushHotChoke    // ✅ RoomTracker-based (uses TransitionHeatGraph)
                };
            }
            
            var validActions = new List<EnemyMode>();
            
            // ? Patrol (always valid if configured)
            if (patrolAlwaysValid || IsPatrolValid(state))
            {
                validActions.Add(EnemyMode.Patrol);
            }
            else
            {
                maskReasons[EnemyMode.Patrol] = "Patrol masked (custom rule)";
            }
            
            // ? InvestigateLastHeard (requires recent audio cue)
            if (IsInvestigateValid(state))
            {
                validActions.Add(EnemyMode.InvestigateLastHeard);
            }
            else
            {
                maskReasons[EnemyMode.InvestigateLastHeard] = "No recent audio cue (hearRecently=0)";
            }
            
            // ? HeatSearchPeak (requires heat confidence)
            if (IsHeatSearchValid(state))
            {
                validActions.Add(EnemyMode.HeatSearchPeak);
            }
            else
            {
                maskReasons[EnemyMode.HeatSearchPeak] = "Low heat confidence (heatConfidence=0)";
            }
            
            // ? SweepArea (always valid - can sweep current room)
            validActions.Add(EnemyMode.SweepArea);
            
            // ? HideSpotCheck (requires nearby hide spots)
            if (IsHideSpotCheckValid(state))
            {
                validActions.Add(EnemyMode.HideSpotCheck);
            }
            else
            {
                maskReasons[EnemyMode.HideSpotCheck] = "No nearby hide spots (nearHideSpots=0)";
            }
            
            // ❌ CutoffAmbush REMOVED: IntersectionPoint dependency, use AmbushHotChoke instead
            
            // ✅ HeatSweep (requires medium/high heat confidence)
            if (IsHeatSweepValid(state))
            {
                validActions.Add(EnemyMode.HeatSweep);
            }
            else
            {
                maskReasons[EnemyMode.HeatSweep] = "Low heat confidence (heatConfidence < 1)";
            }
            
            // ✅ AmbushHotChoke (requires high heat confidence - uses RoomTracker)
            if (IsAmbushHotChokeValid(state))
            {
                validActions.Add(EnemyMode.AmbushHotChoke);
            }
            else
            {
                maskReasons[EnemyMode.AmbushHotChoke] = "Low heat confidence (requires heatConfidence >= 2)";
            }
            
            // ? Fallback: Ensure at least one action is valid
            if (validActions.Count == 0)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[ActionMasker] No valid actions! Forcing Patrol.");
                
                validActions.Add(EnemyMode.Patrol);
            }
            
            if (showDebugLogs && maskReasons.Count > 0)
            {
                Debug.Log("<color=yellow>[ActionMasker] Masked actions:</color>");
                foreach (var kvp in maskReasons)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value}");
                }
            }
            
            return validActions;
        }
        
        /// <summary>
        /// Check if Patrol is valid
        /// </summary>
        private bool IsPatrolValid(RLStateKey state)
        {
            // Patrol always valid (configurable)
            return patrolAlwaysValid;
        }
        
        /// <summary>
        /// Check if InvestigateLastHeard is valid
        /// </summary>
        private bool IsInvestigateValid(RLStateKey state)
        {
            // Requires recent hear OR see
            return state.hearRecently == 1 || state.seePlayer == 1;
        }
        
        /// <summary>
        /// Check if HeatSearchPeak is valid
        /// </summary>
        private bool IsHeatSearchValid(RLStateKey state)
        {
            // Requires medium/high heat confidence
            return state.heatConfidence >= 1; // Medium (1) or High (2)
        }
        
        /// <summary>
        /// Check if HideSpotCheck is valid
        /// </summary>
        private bool IsHideSpotCheckValid(RLStateKey state)
        {
            // Requires nearby hide spots
            return state.nearHideSpots == 1;
        }
        
        // ❌ IsCutoffAmbushValid REMOVED: No longer using IntersectionPoints
        
        /// <summary>
        /// ✅ Check if HeatSweep is valid
        /// </summary>
        private bool IsHeatSweepValid(RLStateKey state)
        {
            // Requires medium/high heat confidence (same as HeatSearchPeak)
            return state.heatConfidence >= 1; // Medium (1) or High (2)
        }
        
        /// <summary>
        /// ✅ Check if AmbushHotChoke is valid (RoomTracker-based)
        /// </summary>
        private bool IsAmbushHotChokeValid(RLStateKey state)
        {
            // ✅ Uses TransitionHeatGraph (RoomTracker), NOT IntersectionPoints
            // Requires high heat confidence only
            return state.heatConfidence >= 2; // Must be HIGH heat (2)
        }
        
        /// <summary>
        /// Get mask reason for action (debug)
        /// </summary>
        public string GetMaskReason(EnemyMode action)
        {
            if (maskReasons.ContainsKey(action))
                return maskReasons[action];
            
            return "Valid";
        }
        
        /// <summary>
        /// Get all mask reasons (debug)
        /// </summary>
        public Dictionary<EnemyMode, string> GetAllMaskReasons()
        {
            return new Dictionary<EnemyMode, string>(maskReasons);
        }

        /// <summary>
        /// ✅ OVERLOAD: Get valid actions using SimpleRLStateKey (for SimpleStateExtractor)
        /// Adapts heatNearbyBucket to heat confidence levels
        /// </summary>
        public List<EnemyMode> GetValidActions(SimpleRLStateKey state)
        {
            maskReasons.Clear();
            
            if (!enableMasking)
            {
                // No masking - all actions valid
                return new List<EnemyMode>
                {
                    EnemyMode.Patrol,
                    EnemyMode.InvestigateLastHeard,
                    EnemyMode.HeatSearchPeak,
                    EnemyMode.SweepArea,
                    EnemyMode.HideSpotCheck,
                    EnemyMode.HeatSweep,
                    EnemyMode.AmbushHotChoke
                };
            }
            
            var validActions = new List<EnemyMode>();
            
            // ? Patrol (always valid)
            if (patrolAlwaysValid)
            {
                validActions.Add(EnemyMode.Patrol);
            }
            
            // ? InvestigateLastHeard (if recently heard)
            if (state.timeSinceContactBucket <= 1) // 0=recent, 1=old, 2=very old
            {
                validActions.Add(EnemyMode.InvestigateLastHeard);
            }
            
            // ? HeatSearchPeak (if heat nearby is warm or hot)
            if (state.heatNearbyBucket >= 1) // 0=cold, 1=warm, 2=hot
            {
                validActions.Add(EnemyMode.HeatSearchPeak);
            }
            
            // ? SweepArea (always valid)
            validActions.Add(EnemyMode.SweepArea);
            
            // ? HideSpotCheck (if nearby hide spots or close)
            if (state.distanceBucket <= 1) // Close to medium distance
            {
                validActions.Add(EnemyMode.HideSpotCheck);
            }
            
            // ? HeatSweep (if heat nearby is warm or hot)
            if (state.heatNearbyBucket >= 1)
            {
                validActions.Add(EnemyMode.HeatSweep);
            }
            
            // ? AmbushHotChoke (if heat nearby is warm or hot - >= 1, not just 2)
            if (state.heatNearbyBucket >= 1)
            {
                validActions.Add(EnemyMode.AmbushHotChoke);
            }
            else
            {
                maskReasons[EnemyMode.AmbushHotChoke] = "Low heat nearby (requires heatNearbyBucket >= 1)";
            }
            
            // ? Fallback: Ensure at least one action
            if (validActions.Count == 0)
            {
                validActions.Add(EnemyMode.Patrol);
            }
            
            return validActions;
        }
    }
}
