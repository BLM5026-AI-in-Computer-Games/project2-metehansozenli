using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AITest.Enemy;
using AITest.World;
using AITest.Heat;

namespace AITest.Learning
{
    /// <summary>
    /// Feature Extractor - Compute normalized features for targets
    /// 
    /// PROMPT 8: Extract features from EnemyContext
    /// - Normalize to [0, 1]
    /// - Handle missing data gracefully
    /// </summary>
    public class FeatureExtractor
    {
        [Header("Normalization Parameters")]
        [Tooltip("Max distance for normalization (meters)")]
        public float maxDistance = 30f;
        
        [Tooltip("Max time since checked (seconds)")]
        public float maxTimeSinceChecked = 300f; // 5 minutes
        
        [Tooltip("Max hide spot density (spots per 100 sqm)")]
        public float maxHideSpotDensity = 10f;

        /// <summary>
        /// ? PROMPT 8: Extract features for Room target
        /// </summary>
        public TargetFeatures ExtractRoomFeatures(RoomTarget room, EnemyContext ctx)
        {
            TargetFeatures features = new TargetFeatures();
            
            // 1. Heat at target
            if (TransitionHeatGraph.Instance)
            {
                float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(room.roomId);
                features.heatAtTarget = Mathf.Clamp01(nodeHeat / TransitionHeatGraph.Instance.maxHeatCap);
            }
            else
            {
                features.heatAtTarget = 0f;
            }
            
            // 2. Distance to target (0=far, 1=close)
            float dist = Vector2.Distance(ctx.Position, room.position);
            features.distanceToTarget = 1f - Mathf.Clamp01(dist / maxDistance);
            
            // 3. Time since checked (inverse of heat - colder rooms haven't been visited recently)
            features.timeSinceChecked = 1f - features.heatAtTarget;
            
            // 4. Hide spot density
            if (room.roomZone)
            {
                var hideSpots = ctx.GetHideSpotsInRoom(room.roomId);
                float area = room.roomZone.Bounds.size.x * room.roomZone.Bounds.size.y;
                float density = (hideSpots.Count / Mathf.Max(area, 1f)) * 100f; // spots per 100 sqm
                features.hideSpotDensity = Mathf.Clamp01(density / maxHideSpotDensity);
            }
            else
            {
                features.hideSpotDensity = 0f;
            }
            
            // 5. Proximity to last heard
            if (ctx.worldModel && ctx.worldModel.HasHeardRecently)
            {
                float distToHeard = Vector2.Distance(room.position, ctx.worldModel.LastHeardPos);
                features.proximityToLastHeard = 1f - Mathf.Clamp01(distToHeard / maxDistance);
            }
            else
            {
                features.proximityToLastHeard = 0f;
            }
            
            // 6. Quest likelihood (placeholder - can be extended)
            features.questLikelihood = 0.5f; // Neutral
            
            return features;
        }

        /// <summary>
        /// ? PROMPT 8: Extract features for HideSpot target
        /// </summary>
        public TargetFeatures ExtractHideSpotFeatures(HideSpotTarget hideSpot, EnemyContext ctx)
        {
            TargetFeatures features = new TargetFeatures();
            
            if (!hideSpot.hideSpot)
            {
                return features; // All zeros
            }
            
            // 1. Heat at target (use room heat)
            string roomId = GetRoomAtPosition(hideSpot.position, ctx);
            if (TransitionHeatGraph.Instance)
            {
                float nodeHeat = TransitionHeatGraph.Instance.GetNodeHeat(roomId);
                features.heatAtTarget = Mathf.Clamp01(nodeHeat / TransitionHeatGraph.Instance.maxHeatCap);
            }
            else
            {
                features.heatAtTarget = 0f;
            }
            
            // 2. Distance to target (0=far, 1=close)
            float dist = Vector2.Distance(ctx.Position, hideSpot.position);
            features.distanceToTarget = 1f - Mathf.Clamp01(dist / maxDistance);
            
            // 3. Time since checked (0=recent, 1=old)
            float timeSinceChecked = hideSpot.hideSpot.TimeSinceLastCheck; // ? FIXED: Property name
            features.timeSinceChecked = Mathf.Clamp01(timeSinceChecked / maxTimeSinceChecked);
            
            // 4. Hide spot density (N/A for individual spots)
            features.hideSpotDensity = 0f;
            
            // 5. Proximity to last heard
            if (ctx.worldModel && ctx.worldModel.HasHeardRecently)
            {
                float distToHeard = Vector2.Distance(hideSpot.position, ctx.worldModel.LastHeardPos);
                features.proximityToLastHeard = 1f - Mathf.Clamp01(distToHeard / maxDistance);
            }
            else
            {
                features.proximityToLastHeard = 0f;
            }
            
            // 6. Intersection centrality (DEPRECATED - no longer using IntersectionPoints)
            features.intersectionCentrality = 0f;
            
            // 7. Quest likelihood (use Bayesian probability)
            features.questLikelihood = hideSpot.hideSpot.Probability;
            
            return features;
        }

        /// <summary>
        /// Helper: Get room ID at position
        /// </summary>
        private string GetRoomAtPosition(Vector2 pos, EnemyContext ctx)
        {
            if (ctx.Registry && ctx.Registry.TryGetRoomAtPosition(pos, out string roomId))
            {
                return roomId;
            }
            return "None";
        }
    }
}
