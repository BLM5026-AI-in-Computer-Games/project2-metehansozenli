using UnityEngine;
using AITest.World;

namespace AITest.Learning
{
    /// <summary>
    /// Target types for perceptron scoring
    /// 
    /// Candidate targets:
    /// - RoomTarget: Patrol/search destinations
    /// - HideSpotTarget: Hide spot checking
    /// </summary>
    
    /// <summary>
    /// Room target (for patrol/search)
    /// </summary>
    public class RoomTarget
    {
        public string roomId;
        public Vector2 position;      // Room center
        public RoomZone roomZone;     // Reference
        public float score;           // Perceptron score
        public TargetFeatures features;

        public RoomTarget(string id, Vector2 pos, RoomZone zone)
        {
            roomId = id;
            position = pos;
            roomZone = zone;
        }

        public override string ToString()
        {
            return $"Room[{roomId}] @ {position} (score={score:F3})";
        }
    }

    /// <summary>
    /// Hide spot target (for hide spot checking)
    /// </summary>
    public class HideSpotTarget
    {
        public HideSpot hideSpot;     // Reference
        public Vector2 position;
        public float score;           // Perceptron score
        public TargetFeatures features;

        public HideSpotTarget(HideSpot spot)
        {
            hideSpot = spot;
            position = spot ? spot.Position : Vector2.zero;
        }

        public override string ToString()
        {
            return $"HideSpot @ {position} (prob={hideSpot?.Probability:F2}, score={score:F3})";
        }
    }
}
