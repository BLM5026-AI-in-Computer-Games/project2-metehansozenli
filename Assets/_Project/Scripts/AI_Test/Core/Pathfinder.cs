using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AITest.Pathfinding
{
    /// <summary>
    /// A* pathfinding sistemi - BoxCollider2D desteği + duvarlardan uzak rotalar
    /// 4-yönlü komşuluk, Manhattan heuristic + wall clearance
    /// </summary>
    public class Pathfinder : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Grid hücre boyutu (metre)")]
        public float cellSize = 0.5f;
        
        [Tooltip("Hedefe varış yarıçapı")]
        [Range(0.1f, 2f)] public float arriveRadius = 0.6f;
        
        [Tooltip("Duvar layer mask")]
        public LayerMask wallMask;
        
        [Header("Wall Clearance")]
        [Tooltip("Duvarlardan minimum uzaklık (metre) - Path duvarlardan bu kadar uzak kalır")]
        [Range(0f, 2f)] public float wallClearance = 0.5f; // ⚡ 0.3f → 0.5f (daha uzak!)
        
        [Tooltip("Duvar buffer çarpanı - Duvara bitişik hücreleri bloke etmek için")]
        [Range(1f, 3f)] public float wallBufferMultiplier = 1.5f; // ⚡ YENİ!
        
        [Header("Grid Bounds")]
        [Tooltip("Grid sınırları (dünya koordinatları)")]
        public Rect gridBounds = new Rect(-15, -10, 30, 20);
        
        [Header("Debug")]
        public bool drawPath = true;
        public bool drawGrid = false;
        
        // Grid data
        private bool[,] walkable;
        private float[,] clearanceMap; // Duvara olan uzaklık
        private int gridWidth, gridHeight;
        private Vector2 gridOrigin;
        
        // Path cache
        private List<Vector2> currentPath;
        private Vector2? currentTarget;
        private int pathIndex = 0;
        
        // Public properties
        public bool HasPath => currentPath != null && currentPath.Count > 0 && pathIndex < currentPath.Count;
        
        public bool ReachedTarget
        {
            get
            {
                if (!currentTarget.HasValue) return false;
                float distToTarget = Vector2.Distance(transform.position, currentTarget.Value);
                return distToTarget < arriveRadius;
            }
        }
        
        public Vector2? CurrentTarget => currentTarget;
        
        private void Awake()
        {
            StartCoroutine(DelayedGridBuild());
        }
        
        private System.Collections.IEnumerator DelayedGridBuild()
        {
            yield return new WaitForEndOfFrame();
            BuildGrid();
            
            // ⚡ YENİ: Auto-diagnostic
            if (walkable != null)
            {
                RunDiagnostics();
            }
        }
        
        /// <summary>
        /// ⚡ YENİ: Automatic problem detection
        /// </summary>
        private void RunDiagnostics()
        {
            int blockedCount = 0;
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (!walkable[x, y]) blockedCount++;
                }
            }
            
            int totalCells = gridWidth * gridHeight;
            float blockedPercent = (blockedCount * 100f) / totalCells;
            
            // Problem #1: No walls detected
            if (blockedCount == 0)
            {
                Debug.LogError($"<color=red>[Pathfinder] ❌ PROBLEM: NO WALLS DETECTED!\n\n" +
                    $"🔧 FIX CHECKLIST:\n" +
                    $"1. Select '{gameObject.name}' in Hierarchy\n" +
                    $"2. Inspector → Pathfinder component → Wall Mask\n" +
                    $"3. Click dropdown → Select 'Wall' layer (Layer 8)\n" +
                    $"4. Check Wall GameObjects have Layer = 'Wall'\n" +
                    $"5. Enable 'Draw Grid' to visualize</color>", this);
            }
            // Problem #2: All cells blocked
            else if (blockedCount == totalCells)
            {
                Debug.LogError($"<color=red>[Pathfinder] ❌ PROBLEM: ALL CELLS BLOCKED! ({totalCells} cells)\n\n" +
                    $"🔧 FIX:\n" +
                    $"Wall Mask is detecting EVERYTHING!\n" +
                    $"1. Pathfinder → Wall Mask → 'Nothing' first\n" +
                    $"2. Then select ONLY 'Wall' layer</color>", this);
            }
            // Problem #3: Too few walls (< 5%)
            else if (blockedPercent < 5f)
            {
                Debug.LogWarning($"<color=yellow>[Pathfinder] ⚠️ WARNING: Very few walls detected!\n" +
                    $"Blocked: {blockedCount}/{totalCells} ({blockedPercent:F1}%)\n\n" +
                    $"Possible causes:\n" +
                    $"- Grid Bounds doesn't cover all walls\n" +
                    $"- Some walls don't have Wall layer\n" +
                    $"- Cell Size too large ({cellSize}m)</color>", this);
            }
            // Problem #4: Too many walls (> 60%)
            else if (blockedPercent > 60f)
            {
                Debug.LogWarning($"<color=yellow>[Pathfinder] ⚠️ WARNING: Too many walls!\n" +
                    $"Blocked: {blockedCount}/{totalCells} ({blockedPercent:F1}%)\n\n" +
                    $"Possible causes:\n" +
                    $"- Grid Bounds too small\n" +
                    $"- Wall Mask detecting wrong layers\n" +
                    $"- Cell Size too small ({cellSize}m)</color>", this);
            }
            // Success!
            else
            {
                Debug.Log($"<color=lime>[Pathfinder] ✅ DIAGNOSTICS PASSED\n" +
                    $"Grid: {gridWidth}x{gridHeight} = {totalCells} cells\n" +
                    $"Blocked: {blockedCount} ({blockedPercent:F1}%)\n" +
                    $"Walkable: {totalCells - blockedCount} ({100f - blockedPercent:F1}%)\n" +
                    $"Wall Buffer: {cellSize * 1.2f:F2}m</color>", this);
            }
        }
        
        /// <summary>
        /// BoxCollider2D'den grid üret + clearance map hesapla
        /// </summary>
        private void BuildGrid()
        {
            gridOrigin = gridBounds.min;
            gridWidth = Mathf.CeilToInt(gridBounds.width / cellSize);
            gridHeight = Mathf.CeilToInt(gridBounds.height / cellSize);
            
            walkable = new bool[gridWidth, gridHeight];
            clearanceMap = new float[gridWidth, gridHeight];
            
            int blockedCount = 0;
            
            if (wallMask.value == 0)
            {
                Debug.LogError("<color=red>[Pathfinder] ⚠️ Wall Mask is EMPTY!</color>");
                return;
            }
            
            // ⚡ PHASE 1: BoxCollider2D ile DOĞRU duvar tespiti
            Debug.Log($"<color=yellow>[Pathfinder] === STARTING GRID BUILD === </color>");
            Debug.Log($"<color=yellow>[Pathfinder] Wall Mask Value: {wallMask.value}</color>");
            Debug.Log($"<color=yellow>[Pathfinder] Grid Size: {gridWidth}x{gridHeight}</color>");
            Debug.Log($"<color=yellow>[Pathfinder] Cell Size: {cellSize}</color>");
            Debug.Log($"<color=yellow>[Pathfinder] Wall Clearance: {wallClearance}</color>");
            
            // ⚡ FIX: Missing for loops
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector2 worldPos = GridToWorld(x, y);
                    
                    // ⚡ FİX: 5-nokta kontrolü (merkez + 4 köşe) - Daha agresif tespit
                    bool isBlocked = false;
                    
                    // 1. Merkez kontrol
                    if (Physics2D.OverlapPoint(worldPos, wallMask))
                    {
                        isBlocked = true;
                    }
                    else
                    {
                        // 2. 4 köşe kontrol (cell'in %45'i kadar offset)
                        float offset = cellSize * 0.45f;
                        Vector2[] checkPoints = new Vector2[]
                        {
                            worldPos + new Vector2(offset, offset),      // Sağ üst
                            worldPos + new Vector2(-offset, offset),     // Sol üst
                            worldPos + new Vector2(offset, -offset),     // Sağ alt
                            worldPos + new Vector2(-offset, -offset),    // Sol alt
                        };
                        
                        foreach (var point in checkPoints)
                        {
                            if (Physics2D.OverlapPoint(point, wallMask))
                            {
                                isBlocked = true;
                                break;
                            }
                        }
                    }
                    
                    walkable[x, y] = !isBlocked;
                    if (isBlocked) blockedCount++;
                }
            }
            
            Debug.Log($"<color=lime>[Pathfinder] ✅ Grid built: {gridWidth}x{gridHeight} = {gridWidth * gridHeight} cells, {blockedCount} blocked ({(blockedCount * 100f / (gridWidth * gridHeight)):F1}%)</color>");
            
            if (blockedCount == 0)
            {
                Debug.LogError("<color=red>[Pathfinder] ⚠️⚠️⚠️ NO WALLS DETECTED! ⚠️⚠️⚠️</color>\n" +
                    "<b>Quick Fixes:</b>\n" +
                    "1. Pathfinder Inspector → Wall Mask → Select ONLY 'Wall' layer\n" +
                    "2. Wall GameObject → Inspector → Top-right Layer dropdown → Set to 'Wall'\n" +
                    "3. Wall GameObject MUST have BoxCollider2D component\n" +
                    "4. Grid Bounds must cover wall area\n" +
                    "5. Enable 'Draw Grid' checkbox to visualize");
                return;
            }
            else if (blockedCount == gridWidth * gridHeight)
            {
                Debug.LogError("<color=red>[Pathfinder] ⚠️⚠️⚠️ ALL CELLS BLOCKED! ⚠️⚠️⚠️</color>\n" +
                    "wallMask is detecting EVERYTHING!\n" +
                    "<b>Fix:</b>\n" +
                    "Pathfinder → Wall Mask → Click dropdown → Select 'Nothing' first → Then select ONLY 'Wall'");
                return;
            }
            
            // ⚡ PHASE 2: Clearance map hesapla + duvara bitişik hücreleri bloke et!
            CalculateClearanceMap();
            ApplyWallBuffer();
        }
        
        /// <summary>
        /// Her hücre için en yakın duvara olan mesafeyi hesapla
        /// </summary>
        private void CalculateClearanceMap()
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (!walkable[x, y])
                    {
                        clearanceMap[x, y] = 0f;
                        continue;
                    }
                    
                    // En yakın duvarı bul
                    float minDist = float.MaxValue;
                    
                    for (int dy = -5; dy <= 5; dy++)
                    {
                        for (int dx = -5; dx <= 5; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            
                            if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight)
                                continue;
                            
                            if (!walkable[nx, ny])
                            {
                                float dist = Mathf.Sqrt(dx * dx + dy * dy) * cellSize;
                                if (dist < minDist)
                                    minDist = dist;
                            }
                        }
                    }
                    
                    clearanceMap[x, y] = minDist;
                }
            }
            
            Debug.Log("<color=cyan>[Pathfinder] Clearance map calculated</color>");
        }
        
        /// <summary>
        /// ⚡ YENİ: Duvara çok yakın hücreleri tamamen bloke et
        /// </summary>
        private void ApplyWallBuffer()
        {
            int bufferedCount = 0;
            float bufferDistance = cellSize * 1.2f; // ⚡ FIX: Cell size'a göre dinamik buffer
            
            Debug.Log($"<color=yellow>[Pathfinder] Applying wall buffer with distance: {bufferDistance:F2}m</color>");
            
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (!walkable[x, y]) continue;
                    
                    // Eğer duvara çok yakınsa bu hücreyi bloke et
                    if (clearanceMap[x, y] < bufferDistance)
                    {
                        walkable[x, y] = false;
                        bufferedCount++;
                    }
                }
            }
            
            Debug.Log($"<color=yellow>[Pathfinder] Wall buffer applied: {bufferedCount} cells near walls blocked (buffer distance: {bufferDistance:F2}m)</color>");
        }
        
        /// <summary>
        /// Hedef değiştir ve path hesapla
        /// </summary>
        public void SetTarget(Vector2 worldTarget)
        {
            if (walkable == null)
            {
                Debug.LogWarning("[Pathfinder] Grid not ready yet!");
                return;
            }
            
            if (currentTarget.HasValue && Vector2.Distance(currentTarget.Value, worldTarget) < 0.1f)
                return;
            
            currentTarget = worldTarget;
            
            Vector2Int start = WorldToGrid(transform.position);
            Vector2Int goal = WorldToGrid(worldTarget);
            
            currentPath = FindPath(start, goal);
            pathIndex = 0;
        }
        
        /// <summary>
        /// A* algoritması + clearance penalty
        /// </summary>
        private List<Vector2> FindPath(Vector2Int start, Vector2Int goal)
        {
            if (!InBounds(start) || !InBounds(goal))
                return null;
            
            // Walkable check + fallback
            if (!walkable[start.x, start.y])
            {
                start = FindNearestWalkable(start);
                if (start.x == -1) return null;
            }
            
            if (!walkable[goal.x, goal.y])
            {
                goal = FindNearestWalkable(goal);
                if (goal.x == -1) return null;
            }
            
            // A* implementation
            var openSet = new List<Node>();
            var closedSet = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, float>();
            var fScore = new Dictionary<Vector2Int, float>();
            
            gScore[start] = 0;
            fScore[start] = Heuristic(start, goal);
            openSet.Add(new Node { pos = start, f = fScore[start] });
            
            while (openSet.Count > 0)
            {
                openSet.Sort((a, b) => a.f.CompareTo(b.f));
                var current = openSet[0].pos;
                openSet.RemoveAt(0);
                
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, goal);
                }
                
                closedSet.Add(current);
                
                foreach (var neighbor in GetNeighbors(current))
                {
                    if (closedSet.Contains(neighbor))
                        continue;
                    
                    // ⚡ FIX: Clearance penalty - cell size'a göre dinamik
                    float clearancePenalty = 0f;
                    float effectiveClearance = cellSize * 1.5f; // Cell size'ın 1.5 katı kadar mesafe
                    
                    if (clearanceMap[neighbor.x, neighbor.y] < effectiveClearance)
                    {
                        // Duvara ne kadar yakınsa o kadar ceza
                        float normalizedDist = clearanceMap[neighbor.x, neighbor.y] / effectiveClearance;
                        clearancePenalty = (1f - normalizedDist) * 15f; // Ağır ceza
                    }
                    
                    float tentativeG = gScore[current] + 1 + clearancePenalty;
                    
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);
                        
                        if (!openSet.Any(n => n.pos == neighbor))
                        {
                            openSet.Add(new Node { pos = neighbor, f = fScore[neighbor] });
                        }
                    }
                }
            }
            
            return null;
        }
        
        private Vector2Int FindNearestWalkable(Vector2Int blocked)
        {
            for (int radius = 1; radius <= 5; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        Vector2Int candidate = new Vector2Int(blocked.x + dx, blocked.y + dy);
                        if (InBounds(candidate) && walkable[candidate.x, candidate.y])
                        {
                            return candidate;
                        }
                    }
                }
            }
            
            return new Vector2Int(-1, -1);
        }
        
        private List<Vector2> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int goal)
        {
            var path = new List<Vector2>();
            var current = goal;
            
            while (cameFrom.ContainsKey(current))
            {
                path.Add(GridToWorld(current.x, current.y));
                current = cameFrom[current];
            }
            
            path.Reverse();
            
            if (currentTarget.HasValue)
            {
                path.Add(currentTarget.Value);
            }
            
            return path;
        }
        
        private float Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
        
        private List<Vector2Int> GetNeighbors(Vector2Int pos)
        {
            var neighbors = new List<Vector2Int>();
            
            Vector2Int[] dirs = new Vector2Int[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };
            
            foreach (var dir in dirs)
            {
                Vector2Int neighbor = pos + dir;
                if (InBounds(neighbor) && walkable[neighbor.x, neighbor.y])
                {
                    neighbors.Add(neighbor);
                }
            }
            
            return neighbors;
        }
        
        private bool InBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
        }
        
        private Vector2Int WorldToGrid(Vector2 worldPos)
        {
            Vector2 localPos = worldPos - gridOrigin;
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(localPos.x / cellSize), 0, gridWidth - 1),
                Mathf.Clamp(Mathf.FloorToInt(localPos.y / cellSize), 0, gridHeight - 1)
            );
        }
        
        private Vector2 GridToWorld(int x, int y)
        {
            return gridOrigin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
        }
        
        /// <summary>
        /// Bir sonraki waypoint'i al (EnemyController kullanacak)
        /// </summary>
        public Vector2? GetNextWaypoint()
        {
            if (currentPath == null || pathIndex >= currentPath.Count)
                return null;
            
            Vector2 waypoint = currentPath[pathIndex];
            float distToWaypoint = Vector2.Distance(transform.position, waypoint);
            
            if (distToWaypoint < arriveRadius)
            {
                pathIndex++;
                
                if (pathIndex < currentPath.Count)
                {
                    waypoint = currentPath[pathIndex];
                }
                else
                {
                    return null;
                }
            }
            
            return waypoint;
        }
        
        /// <summary>
        /// ⚡ YENİ: Force rebuild grid (for testing)
        /// </summary>
        [ContextMenu("Force Rebuild Grid")]
        public void ForceRebuildGrid()
        {
            Debug.Log("<color=yellow>[Pathfinder] Force rebuilding grid...</color>");
            BuildGrid();
            if (walkable != null)
            {
                RunDiagnostics();
            }
        }
        
        /// <summary>
        /// ⚡ YENİ: Test wall detection at mouse position
        /// </summary>
        [ContextMenu("Test Wall Detection")]
        public void TestWallDetection()
        {
            #if UNITY_EDITOR
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            
            bool detected = Physics2D.OverlapPoint(mousePos, wallMask);
            var collider = Physics2D.OverlapPoint(mousePos);
            
            Debug.Log($"<color=cyan>[Pathfinder] Wall Test at {mousePos}\n" +
                $"Wall Mask Value: {wallMask.value}\n" +
                $"Detected by wallMask: {detected}\n" +
                $"Any Collider: {(collider ? $"YES ({collider.gameObject.name}, Layer: {LayerMask.LayerToName(collider.gameObject.layer)})" : "NO")}</color>");
            #endif
        }
        
        private struct Node
        {
            public Vector2Int pos;
            public float f;
        }
        
        #region Gizmos
        private void OnDrawGizmos()
        {
            // Grid çiz
            if (drawGrid && walkable != null)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        // Clearance gösterimi
                        if (walkable[x, y])
                        {
                            float clearance = clearanceMap[x, y];
                            if (clearance < wallClearance)
                            {
                                // Duvara yakın - sarı/turuncu
                                float t = clearance / wallClearance;
                                Gizmos.color = new Color(1f, t, 0f, 0.3f);
                            }
                            else
                            {
                                // Güvenli - yeşil
                                Gizmos.color = new Color(0, 1, 0, 0.1f);
                            }
                        }
                        else
                        {
                            // Duvar - kırmızı
                            Gizmos.color = new Color(1, 0, 0, 0.5f);
                        }
                        
                        Vector2 worldPos = GridToWorld(x, y);
                        Gizmos.DrawCube(worldPos, Vector3.one * cellSize * 0.9f);
                    }
                }
            }
            
            // Path çiz
            if (drawPath && currentPath != null && currentPath.Count > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                }
                
                if (pathIndex < currentPath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(currentPath[pathIndex], arriveRadius);
                }
            }
        }
        #endregion
    }
}
