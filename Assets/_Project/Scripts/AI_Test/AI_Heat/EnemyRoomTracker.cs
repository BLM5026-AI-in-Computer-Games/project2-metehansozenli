using UnityEngine;

namespace AITest.Heat
{
    /// <summary>
    /// Enemy Room Tracker - Tracks which room the enemy is currently in
    /// 
    /// Attach to: Enemy GameObject
    /// Used by: Heat-based options (HeatSweepOption, AmbushHotChokeOption)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyRoomTracker : MonoBehaviour
    {
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // State tracking
        private string currentRoomId = null;
        private float checkInterval = 0.5f;
        private float nextCheckTime = 0f;

        private void Start()
        {
            // Initial check
            CheckRoomAtPosition();
        }

        private void Update()
        {
            // Fallback: If unknown room, check periodically
            if (string.IsNullOrEmpty(currentRoomId) && Time.time >= nextCheckTime)
            {
                CheckRoomAtPosition();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        private void CheckRoomAtPosition()
        {
            if (AITest.World.WorldRegistry.Instance)
            {
                if (AITest.World.WorldRegistry.Instance.TryGetRoomAtPosition(transform.position, out string roomId))
                {
                     if (currentRoomId != roomId)
                     {
                         currentRoomId = roomId;
                         if (showDebugLogs) Debug.Log($"[EnemyRoomTracker] Force updated room: {currentRoomId}");
                     }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var roomTrigger = other.GetComponent<AITest.World.RoomZone>();
            if (roomTrigger == null)
                return;
            
            string newRoomId = roomTrigger.roomId;
            
            if (newRoomId != currentRoomId)
            {
                currentRoomId = newRoomId;
                
                if (showDebugLogs)
                    Debug.Log($"[EnemyRoomTracker] Enemy entered room: {currentRoomId}");
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            var roomTrigger = other.GetComponent<AITest.World.RoomZone>();
            if (roomTrigger == null)
                return;
            
            if (roomTrigger.roomId == currentRoomId)
            {
                if (showDebugLogs)
                    Debug.Log($"[EnemyRoomTracker] Enemy exited room: {currentRoomId}");
                
                currentRoomId = null;
            }
        }
        
        /// <summary>
        /// Get current room ID
        /// </summary>
        public string GetCurrentRoom()
        {
            return currentRoomId;
        }
        
        /// <summary>
        /// Check if enemy is currently in a room
        /// </summary>
        public bool IsInRoom()
        {
            return !string.IsNullOrEmpty(currentRoomId);
        }
    }
}
