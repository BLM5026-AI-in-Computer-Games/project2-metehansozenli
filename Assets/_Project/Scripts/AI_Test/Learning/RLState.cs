using System;

namespace AITest.Learning
{
    /// <summary>
    /// Ayrýk RL state tanýmý (4 zaman kovasý + 4 player style kovasý)
    /// </summary>
    [Serializable]
    public struct RLState : IEquatable<RLState>
    {
        public string enemySectorId;       // A-H
        public string lastSeenSectorId;    // A-H veya "None"
        public string lastHeardSectorId;   // A-H veya "None"
        public int timeSinceContactBucket; // 0:0–5s, 1:5–15s, 2:15–30s, 3:30+s
        public int playerStyleBucket;      // 0–3

        public string ToKey()
        {
            return $"{enemySectorId}|{lastSeenSectorId}|{lastHeardSectorId}|{timeSinceContactBucket}|{playerStyleBucket}";
        }

        public static int GetTimeBucket(float t)
        {
            if (t < 5f) return 0;
            if (t < 15f) return 1;
            if (t < 30f) return 2;
            return 3;
        }

        public bool Equals(RLState other) => ToKey() == other.ToKey();
        public override int GetHashCode() => ToKey().GetHashCode();
    }

    public enum RLAction
    {
        GoToLastSeen = 0,
        GoToLastHeard = 1,
        SweepNearest3 = 2,
        AmbushBestPortal = 3,
        Patrol = 4
    }
}
