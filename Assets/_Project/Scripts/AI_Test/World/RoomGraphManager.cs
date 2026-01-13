using UnityEngine;

namespace AITest.World
{
    /// <summary>
    /// Room Graph Manager - MonoBehaviour wrapper for RoomGraph
    /// 
    /// PROMPT 16: Alternative to ScriptableObject
    /// - Holds RoomGraphSO reference
    /// - Singleton instance
    /// </summary>
    public class RoomGraphManager : MonoBehaviour
    {
        public static RoomGraphManager Instance { get; private set; }
        
        [Header("Room Graph")]
        [Tooltip("Room graph data (ScriptableObject)")]
        public RoomGraphSO roomGraph;
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        private void Awake()
        {
            // Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Build graph
            if (roomGraph != null)
            {
                roomGraph.BuildGraph();
                
                if (showDebugLogs)
                    Debug.Log("<color=cyan>[RoomGraphManager] Room graph initialized</color>");
            }
            else
            {
                Debug.LogWarning("[RoomGraphManager] No RoomGraphSO assigned!");
            }
        }
        
        /// <summary>
        /// Get room graph
        /// </summary>
        public RoomGraphSO GetGraph()
        {
            return roomGraph;
        }
    }
}
