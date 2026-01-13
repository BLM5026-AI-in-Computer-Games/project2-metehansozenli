using UnityEngine;
using System.Collections.Generic;
using AITest.Learning;

namespace AITest.Enemy
{
    /// <summary>
    /// Hide Spot Check Option - Check high-probability hide spots
    /// 
    /// PROMPT 9 COMPLETE:
    /// - Use TargetSelector to pick top-K hide spots
    /// - Visit each spot, check for duration
    /// - Update Bayesian probabilities
    /// - Optional: Train perceptron with feedback
    /// </summary>
    public class HideSpotCheckOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.HideSpotCheck;
        
        [Header("Hide Spot Settings")]
        [Tooltip("Number of hide spots to check per option")]
        [Range(1, 5)] public int spotsToCheck = 3;
        
        [Tooltip("Check duration at each spot (seconds)")]
        [Range(0.5f, 5f)] public float checkDuration = 1.5f;
        
        [Tooltip("Train perceptron with feedback")]
        public bool enableLearning = false;
        
        // Components
        private AIAgentMover mover;
        private TargetSelector targetSelector;
        
        // Check state
        private List<HideSpotTarget> targetSpots;
        private int currentSpotIndex;
        private bool checking;
        private float checkStartTime;

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
            
            // Get current room or best guess room
            string targetRoom = ctx.GetCurrentRoom();
            
            if (targetRoom == "None")
            {
                targetRoom = ctx.GetBestGuessRoom();
            }
            
            if (targetRoom == "None")
            {
                Log("No target room for hide spot check!", "red");
                targetSpots = new List<HideSpotTarget>();
                return;
            }
            
            // ? PROMPT 9: Select top-K hide spots using perceptron
            targetSpots = targetSelector.SelectHideSpots(ctx, targetRoom, spotsToCheck);
            
            if (targetSpots.Count == 0)
            {
                Log($"No hide spots in room {targetRoom}!", "orange");
                return;
            }
            
            currentSpotIndex = 0;
            checking = false;
            
            Log($"Hide spot check started in {targetRoom} ({targetSpots.Count} spots, top prob={targetSpots[0].hideSpot.Probability:P0})");
            
            // ? Move to first hide spot
            mover.SetDestination(targetSpots[0].position);
        }

        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            // ? NULL CHECK: Prevent NullReferenceException
            if (targetSpots == null || targetSpots.Count == 0)
            {
                Log("No hide spots to check! Failing.", "red");
                return OptionStatus.Failed;
            }
            
            // ? INDEX CHECK: Prevent out of range
            if (currentSpotIndex >= targetSpots.Count)
            {
                Log("Spot index out of range! Failing.", "red");
                return OptionStatus.Failed;
            }
            
            // ? MOVER CHECK: Prevent null mover
            if (!mover)
            {
                Log("Mover is null! Failing.", "red");
                return OptionStatus.Failed;
            }
            
            var currentSpot = targetSpots[currentSpotIndex];
            
            // ? SPOT CHECK: Prevent null spot
            if (currentSpot == null || currentSpot.hideSpot == null)
            {
                Log($"Hide spot {currentSpotIndex} is null! Skipping.", "orange");
                currentSpotIndex++;
                
                if (currentSpotIndex >= targetSpots.Count)
                    return OptionStatus.Succeeded;
                
                return OptionStatus.Running;
            }
            
            // ? Check if checking
            if (checking)
            {
                float checkElapsed = Time.time - checkStartTime;
                
                if (checkElapsed >= checkDuration)
                {
                    // ? INTERACT with hide spot (like player pressing E)
                    bool playerFound = currentSpot.hideSpot.Interact();
                    
                    Log($"Hide spot {currentSpotIndex + 1}/{targetSpots.Count} checked (found={playerFound}, prob={currentSpot.hideSpot.Probability:P0})");
                    
                    // ? PROMPT 9: Optional learning
                    if (enableLearning && targetSelector)
                    {
                        float feedback = playerFound ? 1.0f : 0.0f;
                        targetSelector.TrainHideSpotScorer(currentSpot, feedback);
                    }
                    
                    if (playerFound)
                    {
                        // Found player! Success!
                        Log("PLAYER FOUND in hide spot! (Bot became visible)", "lime");
                        return OptionStatus.Succeeded;
                    }
                    
                    currentSpotIndex++;
                    
                    // ? Check if all spots checked
                    if (currentSpotIndex >= targetSpots.Count)
                    {
                        Log("All hide spots checked (player not found)", "yellow");
                        return OptionStatus.Succeeded;
                    }
                    
                    // Move to next spot
                    checking = false;
                    
                    // ? Check next spot exists
                    if (currentSpotIndex < targetSpots.Count && targetSpots[currentSpotIndex] != null)
                    {
                        mover.SetDestination(targetSpots[currentSpotIndex].position);
                    }
                }
            }
            else
            {
                // ? Check if at hide spot
                if (mover.IsAtPosition(currentSpot.position, 2f))
                {
                    // Arrived at hide spot - INTERACT to check!
                    Log($"Arrived at hide spot {currentSpotIndex + 1}/{targetSpots.Count}, INTERACTING...", "yellow");
                    
                    // ? INTERACT ACTION - Trigger animation/visual feedback
                    TriggerInteractAction(currentSpot.hideSpot);
                    
                    checking = true;
                    checkStartTime = Time.time;
                    
                    // Stop movement during check
                    mover.Stop();
                }
            }
            
            return OptionStatus.Running;
        }

        protected override void OnStop(EnemyContext ctx)
        {
            if (mover)
                mover.Stop();
            
            Log($"Hide spot check stopped ({currentSpotIndex}/{targetSpots?.Count ?? 0} spots checked)");
        }
        
        /// <summary>
        /// Trigger interact action on hide spot (visual feedback, animation, etc.)
        /// </summary>
        private void TriggerInteractAction(AITest.World.HideSpot spot)
        {
            // ? Log interact action
            Debug.Log($"<color=orange>[HideSpotCheck] ? INTERACT with {spot.spotId}</color>");
            
            // ? TODO: Add animator trigger if you have one
            // var animator = GetComponent<Animator>();
            // if (animator) animator.SetTrigger("Interact");
            
            // ? TODO: Add visual effect (particle, sound, etc.)
            // Instantiate(interactEffectPrefab, spot.Position, Quaternion.identity);
            
            // ? Face the hide spot
            Vector2 direction = (spot.Position - (Vector2)transform.position).normalized;
            if (direction.magnitude > 0.1f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle - 90f); // Adjust based on sprite orientation
            }
        }

        /// <summary>
        /// INTERRUPT RULE: Cannot be interrupted by HearNoise (systematic check)
        /// </summary>
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // Only SeePlayer can interrupt
            return base.CanBeInterruptedBy(interruptType);
        }
    }
}
