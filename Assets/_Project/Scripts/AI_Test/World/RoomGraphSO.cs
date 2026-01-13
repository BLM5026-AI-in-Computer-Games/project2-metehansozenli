using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AITest.World
{
    /// <summary>
    /// Room Graph - Room adjacency data structure
    /// 
    /// PROMPT 16: Define room connections
    /// - Adjacency: RoomId ? List<RoomId> neighbors
    /// - Methods: GetNeighbors, GetRoomsWithinDepth
    /// - Configurable via ScriptableObject or MonoBehaviour
    /// </summary>
    [CreateAssetMenu(fileName = "RoomGraph", menuName = "AI/Room Graph")]
    public class RoomGraphSO : ScriptableObject
    {
        [System.Serializable]
        public class RoomEdge
        {
            public string roomA;
            public string roomB;
            
            [Tooltip("Is this a one-way connection? (A ? B only)")]
            public bool isOneWay = false;
        }
        
        [Header("Room Connections")]
        [Tooltip("List of room edges (connections)")]
        public List<RoomEdge> edges = new List<RoomEdge>();
        
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Internal adjacency list
        private Dictionary<string, HashSet<string>> adjacencyList;
        private bool isBuilt = false;
        
        /// <summary>
        /// Build adjacency list from edges
        /// </summary>
        public void BuildGraph()
        {
            adjacencyList = new Dictionary<string, HashSet<string>>();
            
            foreach (var edge in edges)
            {
                // Add A ? B
                if (!adjacencyList.ContainsKey(edge.roomA))
                    adjacencyList[edge.roomA] = new HashSet<string>();
                
                adjacencyList[edge.roomA].Add(edge.roomB);
                
                // Add B ? A (if bidirectional)
                if (!edge.isOneWay)
                {
                    if (!adjacencyList.ContainsKey(edge.roomB))
                        adjacencyList[edge.roomB] = new HashSet<string>();
                    
                    adjacencyList[edge.roomB].Add(edge.roomA);
                }
            }
            
            isBuilt = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[RoomGraph] Built graph with {adjacencyList.Count} rooms</color>");
                
                foreach (var kvp in adjacencyList)
                {
                    Debug.Log($"  {kvp.Key} ? [{string.Join(", ", kvp.Value)}]");
                }
            }
        }
        
        /// <summary>
        /// ? PROMPT 16: Get neighbors of a room
        /// </summary>
        public IReadOnlyList<string> GetNeighbors(string roomId)
        {
            if (!isBuilt)
                BuildGraph();
            
            if (adjacencyList.ContainsKey(roomId))
            {
                return adjacencyList[roomId].ToList();
            }
            
            return new List<string>(); // No neighbors
        }
        
        /// <summary>
        /// ? PROMPT 16: Get all rooms within depth from start
        /// BFS traversal
        /// </summary>
        public IEnumerable<string> GetRoomsWithinDepth(string start, int depth)
        {
            if (!isBuilt)
                BuildGraph();
            
            if (depth < 0)
                yield break;
            
            if (!adjacencyList.ContainsKey(start))
                yield break;
            
            var visited = new HashSet<string>();
            var queue = new Queue<(string room, int dist)>();
            
            queue.Enqueue((start, 0));
            visited.Add(start);
            
            while (queue.Count > 0)
            {
                var (room, dist) = queue.Dequeue();
                
                yield return room;
                
                if (dist < depth)
                {
                    foreach (var neighbor in GetNeighbors(room))
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue((neighbor, dist + 1));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if two rooms are connected
        /// </summary>
        public bool AreConnected(string roomA, string roomB)
        {
            if (!isBuilt)
                BuildGraph();
            
            if (adjacencyList.ContainsKey(roomA))
            {
                return adjacencyList[roomA].Contains(roomB);
            }
            
            return false;
        }
        
        /// <summary>
        /// Get shortest path between two rooms (BFS)
        /// </summary>
        public List<string> GetShortestPath(string start, string goal)
        {
            if (!isBuilt)
                BuildGraph();
            
            if (start == goal)
                return new List<string> { start };
            
            var visited = new HashSet<string>();
            var queue = new Queue<List<string>>();
            
            queue.Enqueue(new List<string> { start });
            visited.Add(start);
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var current = path[path.Count - 1];
                
                foreach (var neighbor in GetNeighbors(current))
                {
                    if (visited.Contains(neighbor))
                        continue;
                    
                    var newPath = new List<string>(path) { neighbor };
                    
                    if (neighbor == goal)
                        return newPath;
                    
                    visited.Add(neighbor);
                    queue.Enqueue(newPath);
                }
            }
            
            return new List<string>(); // No path found
        }
        
        /// <summary>
        /// Get all rooms in graph
        /// </summary>
        public IEnumerable<string> GetAllRooms()
        {
            if (!isBuilt)
                BuildGraph();
            
            return adjacencyList.Keys;
        }
        
        /// <summary>
        /// Validate graph (check for orphaned rooms)
        /// </summary>
        [ContextMenu("Validate Graph")]
        public void ValidateGraph()
        {
            BuildGraph();
            
            Debug.Log("<color=cyan>===== ROOM GRAPH VALIDATION =====</color>");
            Debug.Log($"Total rooms: {adjacencyList.Count}");
            Debug.Log($"Total edges: {edges.Count}");
            
            // Check for orphaned rooms
            foreach (var kvp in adjacencyList)
            {
                if (kvp.Value.Count == 0)
                {
                    Debug.LogWarning($"[RoomGraph] Orphaned room: {kvp.Key} (no neighbors)");
                }
            }
            
            Debug.Log("<color=lime>Validation complete!</color>");
        }
    }
}
