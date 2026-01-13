using System;

namespace AITest.Learning
{
    /// <summary>
    /// Discrete RL State Key (PROMPT 5)
    /// 
    /// STATE SPACE SIZE:
    /// 2 * 2 * 2 * 4 * 4 * 3 * 3 * 3 * 2 * 2 * 3 * 6 = 497,664 states
    /// 
    /// BINS:
    /// - SeePlayer: {0, 1} (2 values)
    /// - HearRecently: {0, 1} (2 values)
    /// - ClueRecently: {0, 1} (2 values)
    /// - TimeSinceSeenBin: {0, 1, 2, 3} (0-2s, 2-6s, 6-15s, 15+s/none)
    /// - TimeSinceHeardBin: {0, 1, 2, 3}
    /// - HeatConfidence: {0, 1, 2} (low/med/high)
    /// - DistToHeatPeakBin: {0, 1, 2} (close/medium/far)
    /// - DistToLastHeardBin: {0, 1, 2}
    /// - InSameRoomAsHeatPeak: {0, 1}
    /// - NearHideSpots: {0, 1}
    /// - SearchProgressBin: {0, 1, 2} (early/mid/late)
    /// - LastAction: {0, 1, 2, 3, 4, 5} (BehaviorState)
    /// </summary>
    [Serializable]
    public struct RLStateKey : IEquatable<RLStateKey>
    {
        // ? PROMPT 5: Discrete bins
        public byte seePlayer;              // 0 or 1
        public byte hearRecently;           // 0 or 1
        public byte clueRecently;           // 0 or 1
        public byte timeSinceSeenBin;       // 0-3
        public byte timeSinceHeardBin;      // 0-3
        public byte heatConfidence;         // 0-2 (low/med/high)
        public byte distToHeatPeakBin;      // 0-2 (close/medium/far)
        public byte distToLastHeardBin;     // 0-2
        public byte inSameRoomAsHeatPeak;   // 0 or 1
        public byte nearHideSpots;          // 0 or 1
        public byte searchProgressBin;      // 0-2 (early/mid/late)
        public byte lastAction;             // 0-5 (BehaviorState)

        /// <summary>
        /// Convert to hashable string key
        /// Format: "s|h|c|ts|th|hc|dhp|dlh|sr|nh|sp|la"
        /// </summary>
        public string ToKey()
        {
            return $"{seePlayer}|{hearRecently}|{clueRecently}|{timeSinceSeenBin}|{timeSinceHeardBin}|{heatConfidence}|{distToHeatPeakBin}|{distToLastHeardBin}|{inSameRoomAsHeatPeak}|{nearHideSpots}|{searchProgressBin}|{lastAction}";
        }

        /// <summary>
        /// Pack into single int (compact storage)
        /// State space: 497,664 states (fits in int32: 2^20 = 1,048,576)
        /// </summary>
        public int ToPackedInt()
        {
            // Bit packing (20 bits total)
            int packed = 0;
            int shift = 0;
            
            packed |= (lastAction & 0x7) << shift; shift += 3;           // 3 bits (0-7, using 0-5)
            packed |= (searchProgressBin & 0x3) << shift; shift += 2;    // 2 bits
            packed |= (nearHideSpots & 0x1) << shift; shift += 1;        // 1 bit
            packed |= (inSameRoomAsHeatPeak & 0x1) << shift; shift += 1; // 1 bit
            packed |= (distToLastHeardBin & 0x3) << shift; shift += 2;   // 2 bits
            packed |= (distToHeatPeakBin & 0x3) << shift; shift += 2;    // 2 bits
            packed |= (heatConfidence & 0x3) << shift; shift += 2;       // 2 bits
            packed |= (timeSinceHeardBin & 0x3) << shift; shift += 2;    // 2 bits
            packed |= (timeSinceSeenBin & 0x3) << shift; shift += 2;     // 2 bits
            packed |= (clueRecently & 0x1) << shift; shift += 1;         // 1 bit
            packed |= (hearRecently & 0x1) << shift; shift += 1;         // 1 bit
            packed |= (seePlayer & 0x1) << shift;                        // 1 bit
            
            return packed;
        }

        /// <summary>
        /// Unpack from int
        /// </summary>
        public static RLStateKey FromPackedInt(int packed)
        {
            RLStateKey state = new RLStateKey();
            int shift = 0;
            
            state.lastAction = (byte)((packed >> shift) & 0x7); shift += 3;
            state.searchProgressBin = (byte)((packed >> shift) & 0x3); shift += 2;
            state.nearHideSpots = (byte)((packed >> shift) & 0x1); shift += 1;
            state.inSameRoomAsHeatPeak = (byte)((packed >> shift) & 0x1); shift += 1;
            state.distToLastHeardBin = (byte)((packed >> shift) & 0x3); shift += 2;
            state.distToHeatPeakBin = (byte)((packed >> shift) & 0x3); shift += 2;
            state.heatConfidence = (byte)((packed >> shift) & 0x3); shift += 2;
            state.timeSinceHeardBin = (byte)((packed >> shift) & 0x3); shift += 2;
            state.timeSinceSeenBin = (byte)((packed >> shift) & 0x3); shift += 2;
            state.clueRecently = (byte)((packed >> shift) & 0x1); shift += 1;
            state.hearRecently = (byte)((packed >> shift) & 0x1); shift += 1;
            state.seePlayer = (byte)((packed >> shift) & 0x1);
            
            return state;
        }

        // IEquatable implementation
        public bool Equals(RLStateKey other)
        {
            return ToKey() == other.ToKey();
        }

        public override bool Equals(object obj)
        {
            return obj is RLStateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ToKey().GetHashCode();
        }

        public static bool operator ==(RLStateKey left, RLStateKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RLStateKey left, RLStateKey right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Human-readable debug string
        /// </summary>
        public override string ToString()
        {
            return $"RLState[See:{seePlayer} Hear:{hearRecently} Clue:{clueRecently} " +
                   $"TSeen:{timeSinceSeenBin} THeard:{timeSinceHeardBin} " +
                   $"Heat:{heatConfidence} DHeat:{distToHeatPeakBin} " +
                   $"DHeard:{distToLastHeardBin} SameRoom:{inSameRoomAsHeatPeak} " +
                   $"NearHide:{nearHideSpots} Progress:{searchProgressBin} " +
                   $"LastAct:{lastAction}]";
        }
    }
}
