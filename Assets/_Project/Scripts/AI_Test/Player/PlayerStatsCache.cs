using UnityEngine;

namespace AITest.Player
{
    /// <summary>
    /// Static cache for player stats, accessible from anywhere
    /// </summary>
    public static class PlayerStatsCache
    {
        private static PlayerStatsMonitor monitor;

        /// <summary>
        /// Current player style bucket (0-3)
        /// </summary>
        public static int CurrentBucket
        {
            get
            {
                if (monitor == null)
                {
                    monitor = Object.FindObjectOfType<PlayerStatsMonitor>();
                }

                return monitor != null ? monitor.PlayerStyleBucket : 0;
            }
        }

        /// <summary>
        /// ? YENÝ: Get monitor reference (for HUD display)
        /// </summary>
        public static PlayerStatsMonitor GetMonitor()
        {
            if (monitor == null)
            {
                monitor = Object.FindObjectOfType<PlayerStatsMonitor>();
            }
            return monitor;
        }

        /// <summary>
        /// Manually set the monitor reference (optional, for performance)
        /// </summary>
        public static void SetMonitor(PlayerStatsMonitor newMonitor)
        {
            monitor = newMonitor;
        }

        /// <summary>
        /// Clear the cached reference
        /// </summary>
        public static void Clear()
        {
            monitor = null;
        }
    }
}
