using UnityEngine;

namespace Project.Systems
{
    [ExecuteAlways] // EDIT MODDA DA ÇALIÞSIN
    public class HeatmapSystem : MonoBehaviour
    {
        public static HeatmapSystem I;

        [Header("Grid")]
        public int width = 64, height = 64;
        public float cellSize = 1f;

        [Header("Dynamics (Play Mode)")]
        [Tooltip("Isý azalma hýzý (0=kalýcý, 0.01=çok yavaþ, 0.2=hýzlý)")]
        [Range(0f, 1f)] public float decayPerSecond = 0.02f; // ÇOK YAVAS (5dk için)
        [Tooltip("Normalizemax - yüksek = daha fazla birikim")]
        [Range(1f, 100f)] public float normalizeMax = 30f; // YÜKSEK (kalýcýlýk için)
        [Tooltip("Hide heat için ayrý çürüme hýzý")] public float hideDecayPerSecond = 0.015f;
        [Tooltip("Hide heat normalizasyonu")] public float hideNormalizeMax = 30f;

        [Header("Gizmos (Edit & Play)")]
        public bool drawGridLines = true;
        public bool drawCenters = true;
        public bool drawHeat = true;
        [Tooltip("Hide heat'i mor tonda çiz")] public bool drawHideHeat = false;
        public Color gridColor = new Color(1, 1, 1, 0.10f);
        public Color centersColor = new Color(0f, 1f, 0f, 0.30f);
        public float centerRadius = 0.05f;

        [HideInInspector] public float[,] heat;
        [HideInInspector] public float[,] hideHeat;

        void Awake() { I = this; EnsureBuffer(); }
        void OnEnable() { I = this; EnsureBuffer(); }
        void OnValidate() { EnsureBuffer(); } // EDIT modda inspector deðiþince de tamponu hazýrla

        void EnsureBuffer()
        {
            if (width <= 0) width = 1;
            if (height <= 0) height = 1;
            if (cellSize <= 0f) cellSize = 0.1f;
            if (heat == null || heat.GetLength(0) != width || heat.GetLength(1) != height)
                heat = new float[width, height];
            if (hideHeat == null || hideHeat.GetLength(0) != width || hideHeat.GetLength(1) != height)
                hideHeat = new float[width, height];
        }

        void Update()
        {
            if (!Application.isPlaying) return; // decay sadece Play modda
            if (decayPerSecond > 0f)
            {
                float dec = decayPerSecond * Time.deltaTime;
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        heat[x, y] = Mathf.Max(0f, heat[x, y] - dec);
            }
            if (hideDecayPerSecond > 0f)
            {
                float dec2 = hideDecayPerSecond * Time.deltaTime;
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        hideHeat[x, y] = Mathf.Max(0f, hideHeat[x, y] - dec2);
            }
        }

        public void AddHeat(Vector2 worldPos, float amount = 1f)
        {
            var c = WorldToCell(worldPos);
            if (!InBounds(c)) return;
            heat[c.x, c.y] += amount;
        }

        public void AddHideHeat(Vector2 worldPos, float amount = 1f)
        {
            var c = WorldToCell(worldPos);
            if (!InBounds(c)) return;
            hideHeat[c.x, c.y] += amount;
        }

        public float GetHeat(Vector2 worldPos)
        {
            var c = WorldToCell(worldPos);
            return InBounds(c) ? heat[c.x, c.y] : 0f;
        }

        public float GetHeat01(Vector2 worldPos) =>
            Mathf.Clamp01(GetHeat(worldPos) / Mathf.Max(0.0001f, normalizeMax));

        public float GetHideHeat(Vector2 worldPos)
        {
            var c = WorldToCell(worldPos);
            return InBounds(c) ? hideHeat[c.x, c.y] : 0f;
        }

        public float GetHideHeat01(Vector2 worldPos) =>
            Mathf.Clamp01(GetHideHeat(worldPos) / Mathf.Max(0.0001f, hideNormalizeMax));

        public Vector2Int WorldToCell(Vector2 w)
        {
            Vector2 o = transform.position; // SOL-ALT OFFSET
            return new Vector2Int(
                Mathf.FloorToInt((w.x - o.x) / cellSize),
                Mathf.FloorToInt((w.y - o.y) / cellSize)
            );
        }

        public Vector2 CellCenter(Vector2Int c)
        {
            Vector2 o = transform.position;
            return new Vector2(
                o.x + (c.x + 0.5f) * cellSize,
                o.y + (c.y + 0.5f) * cellSize
            );
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

        // === GÝZMOS: EDIT & PLAY ===
        void OnDrawGizmos()
        {
            // GRID ÇÝZGÝLERÝ
            if (drawGridLines)
            {
                Gizmos.color = gridColor;
                Vector3 o = transform.position;
                for (int x = 0; x <= width; x++)
                {
                    Vector3 a = o + new Vector3(x * cellSize, 0f, 0f);
                    Vector3 b = o + new Vector3(x * cellSize, height * cellSize, 0f);
                    Gizmos.DrawLine(a, b);
                }
                for (int y = 0; y <= height; y++)
                {
                    Vector3 a = o + new Vector3(0f, y * cellSize, 0f);
                    Vector3 b = o + new Vector3(width * cellSize, y * cellSize, 0f);
                    Gizmos.DrawLine(a, b);
                }
            }

            // HÜCRE MERKEZLERÝ
            if (drawCenters)
            {
                Gizmos.color = centersColor;
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        Vector3 c = (Vector3)CellCenter(new Vector2Int(x, y));
                        Gizmos.DrawSphere(c, centerRadius);
                    }
            }

            // ISI RENKLERÝ (varsa)
            if (drawHeat && heat != null)
            {
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        float v = heat[x, y];
                        if (v <= 0f) continue;
                        float t = Mathf.Clamp01(v / Mathf.Max(0.0001f, normalizeMax));
                        Color c = Color.Lerp(Color.blue, Color.red, t);
                        c.a = 0.40f;
                        Gizmos.color = c;

                        Vector3 p = (Vector3)CellCenter(new Vector2Int(x, y));
                        Gizmos.DrawCube(p, Vector3.one * cellSize * 0.9f);
                    }
            }

            // HIDE HEAT ÇÝZ
            if (drawHideHeat && hideHeat != null)
            {
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        float v = hideHeat[x, y];
                        if (v <= 0f) continue;
                        float t = Mathf.Clamp01(v / Mathf.Max(0.0001f, hideNormalizeMax));
                        Color c = Color.Lerp(new Color(0.5f, 0f, 0.8f), Color.magenta, t);
                        c.a = 0.35f;
                        Gizmos.color = c;
                        Vector3 p = (Vector3)CellCenter(new Vector2Int(x, y));
                        Gizmos.DrawCube(p, Vector3.one * cellSize * 0.7f);
                    }
            }
        }
    }
}
