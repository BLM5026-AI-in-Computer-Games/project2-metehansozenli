using UnityEngine;
using System.Collections.Generic;
using AITest.Learning;

namespace AITest.Enemy
{
    /// <summary>
    /// Sweep Option - Systematic room search
    /// 
    /// PROMPT 9 COMPLETE:
    /// - Generate sweep points (center + corners + hide spots)
    /// - Visit each point sequentially
    /// - Scan at each point
    /// - Mark room searched after complete
    /// </summary>
    public class SweepOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.SweepArea;
        
        [Tooltip("Max number of hide spots to check per sweep")]
        [Range(1, 5)] public int maxHideSpotsToCheck = 3;
        
        [Tooltip("Also check room corners? (If false, only Center + Top Hide Spots)")]
        public bool checkCorners = false; // ? User requested probability based check, so disabling corners by default seems appropriate

        // Components
        private AIAgentMover mover;
        private TargetSelector targetSelector;
        
        [Header("Sweep Settings")]
        [Tooltip("Scan duration at each sweep point (seconds)")]
        [Range(0.2f, 3f)] public float scanDuration = 1f;

        [Tooltip("Use TargetSelector to choose target room")]
        public bool usePerceptronForRoomSelection = true;
        
        // Sweep state
        private string targetRoom;
        private List<Vector2> sweepPoints;
        private int currentPointIndex;
        private bool scanning;
        private float scanStartTime;
        
        private void Awake()
        {
            // Increase timeout for sweep (room traversal + scanning spots can take time)
            maxDuration = 25f;
        }

        protected override void OnStart(EnemyContext ctx)
        {
            // Initialize sweep points list to prevent null references
            sweepPoints = new List<Vector2>();
            currentPointIndex = 0;
            scanning = false;
            
            // Get components
            mover = GetComponent<AIAgentMover>();
            targetSelector = GetComponent<TargetSelector>();
            
            if (!mover)
            {
                Log("Missing AIAgentMover!", "red");
                return;
            }
            
            // ? PROMPT 9: Determine target room
            targetRoom = "None";
            
            if (usePerceptronForRoomSelection && targetSelector)
            {
                try
                {
                    // Use perceptron to select best room
                    var roomTarget = targetSelector.SelectPatrolRoom(ctx);
                    targetRoom = roomTarget?.roomId ?? "None";
                }
                catch (System.Exception ex)
                {
                    Log($"Error selecting patrol room: {ex.Message}", "red");
                    targetRoom = "None";
                }
            }
            else
            {
                // Use worldModel heuristic
                if (ctx.worldModel.HasSeenRecently)
                {
                    targetRoom = ctx.worldModel.LastSeenRoom;
                }
                else if (ctx.worldModel.HasHeardRecently)
                {
                    targetRoom = ctx.worldModel.LastHeardRoom;
                }
                else
                {
                    targetRoom = ctx.GetBestGuessRoom();
                }
                
                // ✅ FILTER: Skip junction rooms even in heuristic mode
                if (!string.IsNullOrEmpty(targetRoom) && ctx.Registry)
                {
                    var roomZone = ctx.Registry.GetRoom(targetRoom);
                    if (roomZone != null && roomZone.isJunction)
                    {
                        Log($"Skipping junction room {targetRoom}, using best guess instead", "yellow");
                        targetRoom = ctx.GetBestGuessRoom(); // Fallback to heatmap peak
                    }
                }
            }
            
            if (targetRoom == "None")
            {
                Log("No target room for sweep!", "red");
                sweepPoints = new List<Vector2>();
                return;
            }
            
            // ? Generate sweep points
            sweepPoints = GenerateSweepPoints(ctx, targetRoom);
            currentPointIndex = 0;
            scanning = false;
            
            Log($"Sweep started in room {targetRoom} ({sweepPoints.Count} points)");
            
            if (sweepPoints.Count > 0)
            {
                // ? Move to first sweep point
                mover.SetDestination(sweepPoints[0]);
            }
        }

        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            if (sweepPoints == null || sweepPoints.Count == 0)
            {
                Log("No sweep points! Failing.", "red");
                return OptionStatus.Failed;
            }
            
            if (mover == null)
            {
                Log("Mover is null! Failing.", "red");
                return OptionStatus.Failed;
            }
            
            // ? Check if scanning
            if (scanning)
            {
                float scanElapsed = Time.time - scanStartTime;
                
                if (scanElapsed >= scanDuration)
                {
                    // Scan complete at this point
                    Log($"Sweep point {currentPointIndex + 1}/{sweepPoints.Count} scanned");
                    
                    currentPointIndex++;
                    
                    // ? Check if sweep complete
                    if (currentPointIndex >= sweepPoints.Count)
                    {
                        Log("Sweep complete!", "lime");
                        
                        // Mark room as searched
                        if (ctx.worldModel)
                        {
                            ctx.worldModel.MarkRoomSearched(targetRoom);
                        }
                        
                        return OptionStatus.Succeeded;
                    }
                    
                    // Move to next point
                    scanning = false;
                    mover.SetDestination(sweepPoints[currentPointIndex]);
                }
            }
            else
            {
                // ? Check if at current sweep point
                Vector2 currentTarget = sweepPoints[currentPointIndex];
                
                if (mover.IsAtPosition(currentTarget, 2f))
                {
                    // Arrived at sweep point - start scanning
                    Log($"Arrived at sweep point {currentPointIndex + 1}/{sweepPoints.Count}, scanning...", "yellow");
                    
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
            
            int totalPoints = sweepPoints != null ? sweepPoints.Count : 0;
            Log($"Sweep stopped ({currentPointIndex}/{totalPoints} points visited)");
        }

        /// <summary>
        /// ? PROMPT 9: Generate sweep points for a room
        /// Fixed: Filter by Probability (don't check everything)
        /// </summary>
        private List<Vector2> GenerateSweepPoints(EnemyContext ctx, string roomId)
        {
            List<Vector2> points = new List<Vector2>();
            
            var room = ctx.GetRoom(roomId);
            if (!room)
            {
                Log($"Room {roomId} not found!", "red");
                return points;
            }
            
            // Get room bounds
            Bounds bounds = room.Bounds;
            Vector2 center = bounds.center;
            Vector2 size = bounds.size;
            
            // 1. Always check Center first (good vantage point)
            points.Add(center);
            
            // 2. Add Top Hide Spots (Probability based)
            var hideSpots = ctx.GetHideSpotsInRoom(roomId);
            if (hideSpots != null && hideSpots.Count > 0)
            {
                // Sort by Probability DESC
                hideSpots.Sort((a, b) => b.Probability.CompareTo(a.Probability));
                
                int count = Mathf.Min(hideSpots.Count, maxHideSpotsToCheck);
                for (int i = 0; i < count; i++)
                {
                    if (hideSpots[i])
                    {
                        points.Add(hideSpots[i].Position);
                        Log($"Selected HideSpot {hideSpots[i].spotId} (P={hideSpots[i].Probability:F2})");
                    }
                }
            }

            // 3. Corners (Only if enabled - usually redundant if we check spots)
            if (checkCorners)
            {
                float inset = 0.4f;
                points.Add(center + new Vector2(-size.x * inset, -size.y * inset)); // Bottom-left
                points.Add(center + new Vector2(size.x * inset, -size.y * inset));  // Bottom-right
                points.Add(center + new Vector2(size.x * inset, size.y * inset));   // Top-right
                points.Add(center + new Vector2(-size.x * inset, size.y * inset));  // Top-left
            }
            
            return points;
        }

        /// <summary>
        /// INTERRUPT RULE: Cannot be interrupted by HearNoise (systematic search)
        /// </summary>
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // Only SeePlayer can interrupt
            return base.CanBeInterruptedBy(interruptType);
        }

        /// <summary>
        /// Get description of the target room (for debug logs)
        /// </summary>
        public string GetTargetDescription()
        {
            return string.IsNullOrEmpty(targetRoom) ? "None" : targetRoom;
        }
    }
}
