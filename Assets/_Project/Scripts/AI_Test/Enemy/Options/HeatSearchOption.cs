using UnityEngine;
using AITest.Learning;
using AITest.Heat; // ✅ NEW: For TransitionHeatGraph
using AITest.World; // ✅ FIX: For RoomZone

namespace AITest.Enemy
{
    /// <summary>
    /// Heat Search Option - Go to heatmap peak
    /// 
    /// PROMPT 9 COMPLETE:
    /// - Use TargetSelector for peak room selection
    /// - Dynamic peak update (if shifts significantly)
    /// - Scan after arrival
    /// - Mark room searched
    /// </summary>
    public class HeatSearchOption : BaseOption
    {
        public override EnemyMode Mode => EnemyMode.HeatSearchPeak;
        
        [Header("Heat Search Settings")]
        [Tooltip("Scan duration after arrival (seconds)")]
        [Range(0.5f, 5f)] public float scanDuration = 2f;
        
        [Tooltip("Heat shift threshold (multiplier) - replan if peak shifts by this much")]
        [Range(1.1f, 3f)] public float heatShiftThreshold = 1.5f;
        
        // Components
        private AIAgentMover mover;
        private TargetSelector targetSelector;
        
        // Search state
        private RoomTarget targetRoom;
        private float lastHeatValue;
        private bool scanning;
        private float scanStartTime;

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
            
            // Primary: perceptron seçim
            targetRoom = targetSelector.SelectPatrolRoom(ctx);
            float nodeHeat = targetRoom != null && TransitionHeatGraph.Instance
                ? TransitionHeatGraph.Instance.GetNodeHeat(targetRoom.roomId)
                : 0f;

            // Fallback: en sıcak oda (perceptron düşük ısı seçtiyse)
            if (targetRoom == null || nodeHeat < 0.1f)
            {
                var hottest = SelectHottestRoom(ctx);
                if (hottest != null)
                {
                    targetRoom = hottest;
                    nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(targetRoom.roomId);
                    Log("Perceptron hedefi soğuktu; en sıcak odaya geçildi", "yellow");
                }
            }

            if (targetRoom == null || nodeHeat < 0.1f)
            {
                Log($"No significant heat (peak heat={nodeHeat:F2})", "orange");
                return;
            }
            
            lastHeatValue = nodeHeat;
            scanning = false;
            
            Log($"Heat search started: {targetRoom.roomId} (heat={lastHeatValue:F2}) @ {targetRoom.position}");
            
            // ? Move to heat peak
            mover.SetDestination(targetRoom.position);
        }

        protected override OptionStatus OnTick(EnemyContext ctx, float dt)
        {
            if (targetRoom == null)
                return OptionStatus.Failed;
            
            // ✅ Check TransitionHeatGraph node heat (not HeatmapModel)
            float currentHeat = TransitionHeatGraph.Instance ? TransitionHeatGraph.Instance.GetNodeHeat(targetRoom.roomId) : 0f;
            
            if (currentHeat < 0.1f)
            {
                Log("Heat too low, aborting", "orange");
                return OptionStatus.Failed;
            }
            
            // ? Dynamic peak update (if not scanning)
            if (!scanning)
            {
                // Re-evaluate: önce perceptron, sonra sıcaklık fallback
                RoomTarget candidate = targetSelector.SelectPatrolRoom(ctx);
                float candidateHeat = candidate != null && TransitionHeatGraph.Instance
                    ? TransitionHeatGraph.Instance.GetNodeHeat(candidate.roomId)
                    : 0f;

                if (candidate == null || candidateHeat < 0.1f)
                {
                    candidate = SelectHottestRoom(ctx);
                    candidateHeat = candidate != null && TransitionHeatGraph.Instance
                        ? TransitionHeatGraph.Instance.GetNodeHeat(candidate.roomId)
                        : 0f;
                }

                if (candidate != null && candidate.roomId != targetRoom.roomId)
                {
                    if (candidateHeat > currentHeat * heatShiftThreshold)
                    {
                        Log($"Peak shifted: {targetRoom.roomId} → {candidate.roomId} (heat {currentHeat:F2} → {candidateHeat:F2})", "yellow");
                        targetRoom = candidate;
                        lastHeatValue = candidateHeat;
                        mover.SetDestination(targetRoom.position);
                    }
                }
            }
            
            // ? Check if scanning
            if (scanning)
            {
                float scanElapsed = Time.time - scanStartTime;
                
                if (scanElapsed >= scanDuration)
                {
                    // Scan complete - mark room searched
                    Log($"Heat search complete (room {targetRoom.roomId} scanned)", "lime");
                    
                    if (ctx.worldModel)
                    {
                        ctx.worldModel.MarkRoomSearched(targetRoom.roomId);
                    }
                    
                    return OptionStatus.Succeeded;
                }
            }
            else
            {
                // ? Check arrival
                if (mover.ReachedDestination)
                {
                    // Arrived at heat peak - start scanning
                    Log($"Arrived at heat peak: {targetRoom.roomId}", "yellow");
                    
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
            
            Log($"Heat search stopped (target={targetRoom?.roomId}, scanning={scanning})");
        }

        /// <summary>
        /// INTERRUPT RULE: Cannot be interrupted by HearNoise (focused on heat)
        /// </summary>
        public override bool CanBeInterruptedBy(InterruptType interruptType)
        {
            // Only SeePlayer can interrupt
            return base.CanBeInterruptedBy(interruptType);
        }

        /// <summary>
        /// En yüksek node heat'e sahip odayı döndürür (junction hariç). Yoksa null.
        /// </summary>
        private RoomTarget SelectHottestRoom(EnemyContext ctx)
        {
            if (ctx == null || ctx.Registry == null || TransitionHeatGraph.Instance == null)
                return null;

            var allRooms = ctx.Registry.GetAllRooms();
            if (allRooms == null || allRooms.Count == 0)
                return null;

            string bestId = null;
            float bestHeat = 0f;
            Vector2 bestPos = Vector2.zero;
            RoomZone bestZone = null;

            foreach (var room in allRooms)
            {
                if (room == null || room.isJunction) continue;
                float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(room.roomId);
                if (nodeHeat > bestHeat)
                {
                    bestHeat = nodeHeat;
                    bestId = room.roomId;
                    bestPos = room.Center;
                    bestZone = room;
                }
            }

            if (bestId == null)
                return null;

            var target = new RoomTarget(bestId, bestPos, bestZone);
            if (targetSelector && targetSelector.featureExtractor != null)
                target.features = targetSelector.featureExtractor.ExtractRoomFeatures(target, ctx);
            target.score = bestHeat; // açıklayıcı olsun
            return target;
        }
    }
}
