using UnityEngine;
using UnityEditor;

namespace AITest.Editor
{
    /// <summary>
    /// Scene View'da mouse ile sektör koordinatlarýný gösterir
    /// Kullaným: Tools ? AI Test ? Sector Coordinate Helper
    /// </summary>
    public class SectorCoordinateHelper : EditorWindow
    {
        private bool isActive = false;
        private Vector2 mouseWorldPos;
        private Vector2 startPos;
        private Vector2 endPos;
        private bool isDragging = false;
        
        [MenuItem("Tools/AI Test/Sector Coordinate Helper")]
        public static void ShowWindow()
        {
            GetWindow<SectorCoordinateHelper>("Sector Helper");
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Sector Coordinate Helper", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            isActive = GUILayout.Toggle(isActive, "Enable Helper (F2)");
            
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F2)
            {
                isActive = !isActive;
                Event.current.Use();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("Instructions:", EditorStyles.boldLabel);
            GUILayout.Label("1. Enable helper");
            GUILayout.Label("2. Click & drag in Scene View");
            GUILayout.Label("3. Release to get coordinates");
            
            GUILayout.Space(10);
            
            if (isDragging || (startPos != Vector2.zero && endPos != Vector2.zero))
            {
                GUILayout.Label("Current Selection:", EditorStyles.boldLabel);
                
                Vector2 min = new Vector2(Mathf.Min(startPos.x, endPos.x), Mathf.Min(startPos.y, endPos.y));
                Vector2 max = new Vector2(Mathf.Max(startPos.x, endPos.x), Mathf.Max(startPos.y, endPos.y));
                Vector2 size = max - min;
                
                GUILayout.Label($"Start: ({startPos.x:F1}, {startPos.y:F1})");
                GUILayout.Label($"End: ({endPos.x:F1}, {endPos.y:F1})");
                GUILayout.Space(5);
                
                // Rect koordinatlarý
                EditorGUILayout.HelpBox(
                    $"Rect Coordinates:\n" +
                    $"X: {min.x:F1}\n" +
                    $"Y: {min.y:F1}\n" +
                    $"Width: {size.x:F1}\n" +
                    $"Height: {size.y:F1}\n\n" +
                    $"Center: ({(min.x + size.x/2):F1}, {(min.y + size.y/2):F1})",
                    MessageType.Info
                );
                
                if (GUILayout.Button("Copy to Clipboard"))
                {
                    string rectData = $"X={min.x:F1}, Y={min.y:F1}, W={size.x:F1}, H={size.y:F1}";
                    GUIUtility.systemCopyBuffer = rectData;
                    Debug.Log($"Copied: {rectData}");
                }
                
                GUILayout.Space(5);
                
                // Portal suggestions (köþeler + ortalar)
                GUILayout.Label("Suggested Portal Positions:", EditorStyles.boldLabel);
                Vector2[] portals = new Vector2[]
                {
                    new Vector2(min.x, min.y + size.y/2), // Sol orta
                    new Vector2(max.x, min.y + size.y/2), // Sað orta
                    new Vector2(min.x + size.x/2, min.y), // Alt orta
                    new Vector2(min.x + size.x/2, max.y)  // Üst orta
                };
                
                for (int i = 0; i < portals.Length; i++)
                {
                    string[] labels = { "Left", "Right", "Bottom", "Top" };
                    GUILayout.Label($"  {labels[i]}: ({portals[i].x:F1}, {portals[i].y:F1})");
                }
                
                if (GUILayout.Button("Copy Portal Array"))
                {
                    string portalData = $"new Vector2({portals[0].x:F1}f, {portals[0].y:F1}f), " +
                                       $"new Vector2({portals[1].x:F1}f, {portals[1].y:F1}f)";
                    GUIUtility.systemCopyBuffer = portalData;
                    Debug.Log($"Copied portals: {portalData}");
                }
                
                GUILayout.Space(5);
                
                // Anchor (merkez)
                Vector2 center = min + size * 0.5f;
                GUILayout.Label("Suggested Anchor (Center):", EditorStyles.boldLabel);
                GUILayout.Label($"  ({center.x:F1}, {center.y:F1})");
                
                if (GUILayout.Button("Copy Anchor"))
                {
                    string anchorData = $"new Vector2({center.x:F1}f, {center.y:F1}f)";
                    GUIUtility.systemCopyBuffer = anchorData;
                    Debug.Log($"Copied anchor: {anchorData}");
                }
                
                GUILayout.Space(10);
                
                if (GUILayout.Button("Reset Selection"))
                {
                    startPos = Vector2.zero;
                    endPos = Vector2.zero;
                    isDragging = false;
                }
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive) return;
            
            Event e = Event.current;
            
            // Mouse world position
            Vector3 mousePos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            mouseWorldPos = new Vector2(mousePos.x, mousePos.y);
            
            // Handle mouse events
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                startPos = mouseWorldPos;
                endPos = mouseWorldPos;
                isDragging = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && isDragging)
            {
                endPos = mouseWorldPos;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isDragging)
            {
                endPos = mouseWorldPos;
                isDragging = false;
                e.Use();
            }
            
            // Draw UI
            Handles.BeginGUI();
            
            GUILayout.BeginArea(new Rect(10, 10, 250, 100));
            GUILayout.Label("Sector Helper (F2 to toggle)", EditorStyles.helpBox);
            GUILayout.Label($"Mouse: ({mouseWorldPos.x:F1}, {mouseWorldPos.y:F1})");
            GUILayout.EndArea();
            
            Handles.EndGUI();
            
            // Draw selection rect
            if (isDragging || (startPos != Vector2.zero && endPos != Vector2.zero))
            {
                Vector2 min = new Vector2(Mathf.Min(startPos.x, endPos.x), Mathf.Min(startPos.y, endPos.y));
                Vector2 max = new Vector2(Mathf.Max(startPos.x, endPos.x), Mathf.Max(startPos.y, endPos.y));
                Vector2 size = max - min;
                
                // Rect çiz
                Handles.color = new Color(0, 1, 1, 0.3f);
                Vector3[] corners = new Vector3[]
                {
                    new Vector3(min.x, min.y, 0),
                    new Vector3(max.x, min.y, 0),
                    new Vector3(max.x, max.y, 0),
                    new Vector3(min.x, max.y, 0),
                    new Vector3(min.x, min.y, 0)
                };
                Handles.DrawPolyLine(corners);
                
                // Fill
                Handles.DrawSolidRectangleWithOutline(
                    corners,
                    new Color(0, 1, 1, 0.1f),
                    new Color(0, 1, 1, 0.5f)
                );
                
                // Portal positions (köþe + orta)
                Handles.color = Color.red;
                Vector2[] portals = new Vector2[]
                {
                    new Vector2(min.x, min.y + size.y/2), // Sol
                    new Vector2(max.x, min.y + size.y/2), // Sað
                    new Vector2(min.x + size.x/2, min.y), // Alt
                    new Vector2(min.x + size.x/2, max.y)  // Üst
                };
                
                foreach (var portal in portals)
                {
                    Handles.DrawWireDisc(portal, Vector3.forward, 0.5f);
                }
                
                // Anchor (merkez)
                Handles.color = Color.green;
                Vector2 center = min + size * 0.5f;
                Handles.DrawWireDisc(center, Vector3.forward, 0.4f);
                
                // Labels
                Handles.Label(new Vector3(min.x, max.y, 0) + Vector3.up * 0.5f, 
                             $"({min.x:F1}, {min.y:F1}) - W:{size.x:F1} H:{size.y:F1}");
            }
            
            // Crosshair at mouse
            Handles.color = Color.yellow;
            Handles.DrawLine(mouseWorldPos + Vector2.left * 0.5f, mouseWorldPos + Vector2.right * 0.5f);
            Handles.DrawLine(mouseWorldPos + Vector2.down * 0.5f, mouseWorldPos + Vector2.up * 0.5f);
            
            sceneView.Repaint();
        }
    }
}
