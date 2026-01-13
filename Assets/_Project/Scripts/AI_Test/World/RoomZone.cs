using UnityEngine;
using System.Collections.Generic;
using AITest.Heat;

namespace AITest.World
{
    /// <summary>
    /// Room/Sector detection zone - Trigger-based room ID tracking
    /// 
    /// USAGE:
    /// 1. Create GameObject for each room (e.g., "Room_A", "Room_B")
    /// 2. Attach BoxCollider2D/PolygonCollider2D (set as Trigger)
    /// 3. Attach this script
    /// 4. Set roomId (e.g., "A", "B", "Kitchen", "Hallway")
    /// 5. WorldRegistry will auto-register on Awake
    /// 
    /// HEATMAP VISUALIZATION:
    /// - Automatically shows heat value in Scene View (Gizmos)
    /// - Uses TransitionHeatGraph for heat display
    /// - Toggle with showHeatmap checkbox
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomZone : MonoBehaviour
    {
        [Header("Room Identification")]
        [Tooltip("Unique room ID (e.g., A, B, Kitchen, Hallway)")]
        public string roomId = "A";
        
        [Tooltip("Human-readable room name (optional)")]
        public string roomName = "Room A";
        
        [Tooltip("✅ Is this a junction/intersection room? (won't be selected for patrol/sweep)")]
        public bool isJunction = false;
        
        [Header("Visual Debug")]
        [Tooltip("Room color in Scene view")]
        public Color debugColor = new Color(0f, 1f, 1f, 0.2f);
        
        [Tooltip("Show room label in Scene view")]
        public bool showLabel = true;
        
        [Header("Heatmap Visualization")]
        [Tooltip("Show heatmap overlay (blue=cold, red=hot)")]
        public bool showHeatmap = true;
        
        [Tooltip("Cold color (0 heat)")]
        public Color coldColor = new Color(0f, 0f, 1f, 0.3f); // Blue
        
        [Tooltip("Hot color (max heat)")]
        public Color hotColor = new Color(1f, 0f, 0f, 0.7f); // Red

        // Cached collider
        private Collider2D roomCollider;
        
        // Occupants tracking (optional)
        private HashSet<GameObject> occupants = new HashSet<GameObject>();
        
        public Collider2D RoomCollider => roomCollider;
        public Bounds Bounds => roomCollider ? roomCollider.bounds : new Bounds(transform.position, Vector3.one);
        public Vector2 Center => Bounds.center;
        public int OccupantCount => occupants.Count;

        private void Awake()
        {
            // Cache collider
            roomCollider = GetComponent<Collider2D>();
            
            if (!roomCollider)
            {
                Debug.LogError($"[RoomZone] {gameObject.name} - Collider2D not found!");
                return;
            }
            
            // Force trigger mode
            if (!roomCollider.isTrigger)
            {
                roomCollider.isTrigger = true;
                Debug.LogWarning($"[RoomZone] {roomId} - Collider was not set as Trigger! Fixed automatically.");
            }
            
            // Validate room ID
            if (string.IsNullOrWhiteSpace(roomId))
            {
                roomId = gameObject.name; // Fallback to GameObject name
                Debug.LogWarning($"[RoomZone] Room ID was empty! Using GameObject name: {roomId}");
            }
            
            // Auto-register with WorldRegistry
            if (WorldRegistry.Instance)
            {
                WorldRegistry.Instance.RegisterRoom(this);
            }
            else
            {
                Debug.LogWarning($"[RoomZone] {roomId} - WorldRegistry not found! Room will not be registered.");
            }
        }
        
        private void OnDestroy()
        {
            // Unregister from WorldRegistry
            if (WorldRegistry.Instance)
            {
                WorldRegistry.Instance.UnregisterRoom(this);
            }
        }

        /// <summary>
        /// Check if a position is inside this room
        /// </summary>
        public bool Contains(Vector2 worldPosition)
        {
            if (!roomCollider) return false;
            
            return roomCollider.OverlapPoint(worldPosition);
        }

        /// <summary>
        /// Get distance from position to room center
        /// </summary>
        public float DistanceTo(Vector2 worldPosition)
        {
            return Vector2.Distance(Center, worldPosition);
        }

        #region Trigger Callbacks (Occupant Tracking)

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Track occupants (Player, Enemy)
            if (other.CompareTag("Player") || other.CompareTag("Enemy"))
            {
                occupants.Add(other.gameObject);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.CompareTag("Enemy"))
            {
                occupants.Remove(other.gameObject);
                
                if (other.CompareTag("Player"))
                {
                    Debug.Log($"<color=cyan>[RoomZone] Player left room: {roomId}</color>");
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!roomCollider)
                roomCollider = GetComponent<Collider2D>();
            
            if (!roomCollider) return;
            
            // Draw room with default color (no heat color change)
            Gizmos.color = debugColor;
            
            if (roomCollider is BoxCollider2D box)
            {
                Vector2 center = (Vector2)transform.position + box.offset;
                Vector2 size = box.size;
                
                // Draw filled box
                Gizmos.DrawCube(center, size);
                
                // Draw outline
                Gizmos.color = new Color(debugColor.r, debugColor.g, debugColor.b, 1f);
                Gizmos.DrawWireCube(center, size);
            }
            else if (roomCollider is PolygonCollider2D poly)
            {
                // Draw polygon outline
                Gizmos.color = new Color(debugColor.r, debugColor.g, debugColor.b, 1f);
                
                for (int i = 0; i < poly.points.Length; i++)
                {
                    Vector2 p1 = (Vector2)transform.position + poly.points[i];
                    Vector2 p2 = (Vector2)transform.position + poly.points[(i + 1) % poly.points.Length];
                    Gizmos.DrawLine(p1, p2);
                }
            }
            else
            {
                // Generic bounds
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            }
            
            #if UNITY_EDITOR
            // Draw room label (always visible)
            if (showLabel)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 14;
                style.fontStyle = FontStyle.Bold;
                
                Vector3 labelPos = Center + Vector2.up * (Bounds.extents.y + 1f);
                
                // Heat percentage (below room ID)
                string heatText = "";
                if (Application.isPlaying)
                {
                    // ✅ Use TransitionHeatGraph node heat (only system)
                    if (TransitionHeatGraph.Instance)
                    {
                        float heat = TransitionHeatGraph.Instance.GetNodeHeat(roomId);
                        float percentage = (heat / 100f) * 100f; // TransitionHeatGraph uses 0-100 range
                        heatText = $"\nHeat: {percentage:F1}%";
                    }
                    else
                    {
                        heatText = "\nHeat: N/A";
                    }
                }
                
                UnityEditor.Handles.Label(labelPos, $"Room: {roomId}\n({roomName}){heatText}", style);
                
                // Occupant count
                if (Application.isPlaying && occupants.Count > 0)
                {
                    Vector3 occupantPos = Center + Vector2.down * (Bounds.extents.y * 0.5f);
                    UnityEditor.Handles.Label(occupantPos, $"👥 {occupants.Count}", style);
                }
            }
            #endif
        }

        #endregion
    }
}
