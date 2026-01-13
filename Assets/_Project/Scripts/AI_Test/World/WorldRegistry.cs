using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AITest.World
{
    /// <summary>
    /// World Registry - Singleton manager for rooms and hide spots
    /// 
    /// USAGE:
    /// 1. Create empty GameObject: "WorldRegistry"
    /// 2. Attach this script
    /// 3. RoomZone/HideSpot components auto-register on Awake
    /// 4. Access via WorldRegistry.Instance
    /// 
    /// QUERIES:
    /// - GetRoom(roomId) / GetRoomAtPosition(pos)
    /// - GetHideSpotsInRoom(roomId)
    /// - TryGetRoomAtPosition(pos, out roomId)
    /// </summary>
    public class WorldRegistry : MonoBehaviour
    {
        // Singleton instance
        private static WorldRegistry instance;
        public static WorldRegistry Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<WorldRegistry>();
                    
                    if (instance == null)
                    {
                        Debug.LogWarning("[WorldRegistry] No WorldRegistry found in scene. Creating one...");
                        GameObject go = new GameObject("WorldRegistry");
                        instance = go.AddComponent<WorldRegistry>();
                    }
                }
                return instance;
            }
        }

        [Header("Registry Data (Runtime)")]
        [Tooltip("All registered rooms")]
        [SerializeField] private List<RoomZone> rooms = new List<RoomZone>();
        
        [Tooltip("All registered hide spots")]
        [SerializeField] private List<HideSpot> hideSpots = new List<HideSpot>();

        [Header("Debug")]
        [Tooltip("Show registry stats in Inspector")]
        public bool showDebugInfo = true;

        // Fast lookup dictionaries
        private Dictionary<string, RoomZone> roomDict = new Dictionary<string, RoomZone>();
        private Dictionary<string, List<HideSpot>> hideSpotsPerRoom = new Dictionary<string, List<HideSpot>>();

        // Public properties
        public int RoomCount => rooms.Count;
        public int HideSpotCount => hideSpots.Count;

        private void Awake()
        {
            // Singleton enforcement
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[WorldRegistry] Duplicate instance detected! Destroying {gameObject.name}");
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            
            // Don't destroy on scene load (optional)
            // DontDestroyOnLoad(gameObject);
            
            Debug.Log("<color=lime>[WorldRegistry] ? Initialized - waiting for auto-registration...</color>");
        }

        private void Start()
        {
            // Log registry stats after all Awake calls
            LogRegistryStats();
        }

        #region Registration (Auto-called by components)

        /// <summary>
        /// Register a room (called by RoomZone.Awake)
        /// </summary>
        public void RegisterRoom(RoomZone room)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.roomId))
            {
                Debug.LogWarning("[WorldRegistry] Attempted to register null or invalid room!");
                return;
            }
            
            if (roomDict.ContainsKey(room.roomId))
            {
                Debug.LogWarning($"[WorldRegistry] Duplicate room ID: {room.roomId}! Overwriting...");
                roomDict[room.roomId] = room;
            }
            else
            {
                rooms.Add(room);
                roomDict[room.roomId] = room;
                Debug.Log($"<color=cyan>[WorldRegistry] Room registered: {room.roomId}</color>");
            }
        }

        /// <summary>
        /// Unregister a room (called by RoomZone.OnDestroy)
        /// </summary>
        public void UnregisterRoom(RoomZone room)
        {
            if (room == null) return;
            
            rooms.Remove(room);
            roomDict.Remove(room.roomId);
        }

        /// <summary>
        /// Register a hide spot (called by HideSpot.Awake)
        /// </summary>
        public void RegisterHideSpot(HideSpot spot)
        {
            if (spot == null || string.IsNullOrWhiteSpace(spot.roomId))
            {
                Debug.LogWarning("[WorldRegistry] Attempted to register null or invalid hide spot!");
                return;
            }
            
            hideSpots.Add(spot);
            
            // Group by room ID
            if (!hideSpotsPerRoom.ContainsKey(spot.roomId))
            {
                hideSpotsPerRoom[spot.roomId] = new List<HideSpot>();
            }
            
            hideSpotsPerRoom[spot.roomId].Add(spot);
            
            Debug.Log($"<color=green>[WorldRegistry] HideSpot registered: {spot.roomId}-{spot.spotIndex}</color>");
        }

        /// <summary>
        /// Unregister a hide spot (called by HideSpot.OnDestroy)
        /// </summary>
        public void UnregisterHideSpot(HideSpot spot)
        {
            if (spot == null) return;
            
            hideSpots.Remove(spot);
            
            if (hideSpotsPerRoom.ContainsKey(spot.roomId))
            {
                hideSpotsPerRoom[spot.roomId].Remove(spot);
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get room by ID
        /// </summary>
        public RoomZone GetRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return null;
            
            roomDict.TryGetValue(roomId, out RoomZone room);
            return room;
        }

        /// <summary>
        /// Get room at a specific world position
        /// </summary>
        public RoomZone GetRoomAtPosition(Vector2 position)
        {
            foreach (var room in rooms)
            {
                if (room && room.Contains(position))
                    return room;
            }
            
            return null;
        }

        /// <summary>
        /// Try get room at position (out parameter pattern)
        /// </summary>
        public bool TryGetRoomAtPosition(Vector2 position, out string roomId)
        {
            RoomZone room = GetRoomAtPosition(position);
            
            if (room != null)
            {
                roomId = room.roomId;
                return true;
            }
            
            roomId = null;
            return false;
        }

        /// <summary>
        /// Get all hide spots in a room
        /// </summary>
        public List<HideSpot> GetHideSpotsInRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return new List<HideSpot>();
            
            if (hideSpotsPerRoom.TryGetValue(roomId, out List<HideSpot> spots))
            {
                return new List<HideSpot>(spots); // Return copy
            }
            
            return new List<HideSpot>();
        }

        /// <summary>
        /// Get hide spots sorted by probability (high to low)
        /// </summary>
        public List<HideSpot> GetHideSpotsByProbability(string roomId)
        {
            List<HideSpot> spots = GetHideSpotsInRoom(roomId);
            
            // Sort by probability (descending)
            spots.Sort((a, b) => b.Probability.CompareTo(a.Probability));
            
            return spots;
        }

        /// <summary>
        /// Get unchecked hide spots in room (not checked in last N seconds)
        /// </summary>
        public List<HideSpot> GetUncheckedHideSpots(string roomId, float minTimeSinceCheck = 30f)
        {
            List<HideSpot> spots = GetHideSpotsInRoom(roomId);
            
            return spots.Where(spot => spot.TimeSinceLastCheck > minTimeSinceCheck).ToList();
        }



        #endregion

        #region Debug & Utility

        /// <summary>
        /// Log registry statistics
        /// </summary>
        private void LogRegistryStats()
        {
            Debug.Log($"<color=lime>[WorldRegistry] ?? Registry Stats:</color>");
            Debug.Log($"  � Rooms: {RoomCount}");
            Debug.Log($"  � Hide Spots: {HideSpotCount}");
            
            if (RoomCount > 0 && rooms != null)
            {
                Debug.Log($"  � Room IDs: {string.Join(", ", rooms.Where(r => r != null).Select(r => r.roomId))}");
            }
        }

        /// <summary>
        /// Get all rooms
        /// </summary>
        public List<RoomZone> GetAllRooms()
        {
            return new List<RoomZone>(rooms);
        }

        /// <summary>
        /// Get all hide spots
        /// </summary>
        public List<HideSpot> GetAllHideSpots()
        {
            return new List<HideSpot>(hideSpots);
        }
        
        /// <summary>
        /// Check if player is hiding in any hide spot
        /// </summary>
        /// <param name="playerPos">Player position</param>
        /// <returns>True if player is hiding in an UNCHECKED spot</returns>
        public bool IsPlayerHiding(Vector2 playerPos)
        {
            foreach (var spot in hideSpots)
            {
                if (spot == null) continue;
                
                // Player is hiding if they're in spot AND enemy hasn't checked it
                if (spot.IsPlayerHiding && !spot.HasBeenChecked)
                {
                    // Verify player is actually at this spot
                    float dist = Vector2.Distance(playerPos, spot.Position);
                    if (dist < 2f) // Within spot radius
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Reset all hide spot learning data
        /// </summary>
        public void ResetAllHideSpotLearning()
        {
            foreach (var spot in hideSpots)
            {
                if (spot)
                    spot.ResetLearning();
            }
            
            Debug.Log("<color=yellow>[WorldRegistry] ?? All hide spot learning data reset!</color>");
        }

        #endregion

        #region Editor Utility

        #if UNITY_EDITOR
        [ContextMenu("Refresh Registry")]
        private void RefreshRegistry()
        {
            // Clear existing data
            rooms.Clear();
            hideSpots.Clear();
            roomDict.Clear();
            hideSpotsPerRoom.Clear();
            
            // Find and register all components
            RoomZone[] foundRooms = FindObjectsByType<RoomZone>(FindObjectsSortMode.None);
            foreach (var room in foundRooms)
            {
                RegisterRoom(room);
            }
            
            HideSpot[] foundSpots = FindObjectsByType<HideSpot>(FindObjectsSortMode.None);
            foreach (var spot in foundSpots)
            {
                RegisterHideSpot(spot);
            }
            
            Debug.Log("<color=lime>[WorldRegistry] ? Registry refreshed manually!</color>");
            LogRegistryStats();
        }

        [ContextMenu("Reset All Hide Spot Learning")]
        private void ResetHideSpotLearningEditor()
        {
            ResetAllHideSpotLearning();
        }
        #endif

        #endregion
    }
}
