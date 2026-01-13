using UnityEngine;

namespace Project.Systems
{
    /// <summary>
    /// Heatmap visualization with Gizmos
    /// Shows player visit frequency in scene view
    /// </summary>
    public class HeatmapDrawer : MonoBehaviour
    {
        public HeatmapSystem sys;
        
        [Range(0f, 1f)] 
        public float alpha = 0.4f;
        
        [Header("Color Scheme")]
        [Tooltip("Yeþil-Sarý-Kýrmýzý gradyan (daha sezgisel)")]
        public bool useGreenRedGradient = true;

        void OnDrawGizmos()
        {
            if (!sys || sys.heat == null) return;
            
            for (int x = 0; x < sys.width; x++)
            {
                for (int y = 0; y < sys.height; y++)
                {
                    float v = sys.heat[x, y];
                    if (v <= 0f) continue;

                    float t = Mathf.Clamp01(v / Mathf.Max(0.0001f, sys.normalizeMax));
                    
                    Color c;
                    if (useGreenRedGradient)
                    {
                        // Yeþil ? Sarý ? Kýrmýzý (sezgisel)
                        if (t < 0.5f)
                            c = Color.Lerp(Color.green, Color.yellow, t * 2f);
                        else
                            c = Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
                    }
                    else
                    {
                        // Mavi ? Kýrmýzý (klasik)
                        c = Color.Lerp(Color.blue, Color.red, t);
                    }
                    
                    c.a = alpha;
                    Gizmos.color = c;

                    Vector3 p = sys.CellCenter(new Vector2Int(x, y));
                    Gizmos.DrawCube(p, Vector3.one * sys.cellSize * 0.9f);
                }
            }
        }
    }
}




