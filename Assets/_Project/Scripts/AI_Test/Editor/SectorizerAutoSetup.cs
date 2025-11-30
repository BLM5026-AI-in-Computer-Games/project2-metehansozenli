using UnityEngine;
using UnityEditor;
using AITest.Sector;

namespace AITest.Editor
{
    /// <summary>
    /// Sectorizer'a otomatik 6 sektör ekle
    /// Kullaným: Sectorizer seçili ? sað týk ? Auto-Setup 6 Sectors
    /// </summary>
    public class SectorizerAutoSetup
    {
        [MenuItem("CONTEXT/Sectorizer/Auto-Setup 6 Sectors (30x20 Grid)")]
        private static void AutoSetup6Sectors(MenuCommand command)
        {
            Sectorizer sectorizer = (Sectorizer)command.context;
            
            if (sectorizer.sectors == null || sectorizer.sectors.Length != 6)
            {
                sectorizer.sectors = new SectorData[6];
            }
            
            // Sektör A (Sol Üst)
            sectorizer.sectors[0] = new SectorData
            {
                id = "A",
                bounds = new Rect(-15, 0, 10, 10),
                portals = new Vector2[] 
                {
                    new Vector2(-10, 5),  // Sað
                    new Vector2(-10, 0)   // Alt
                },
                anchors = new Vector2[] { new Vector2(-10, 5) },
                sweepPoints = new Vector2[]
                {
                    new Vector2(-12, 8),
                    new Vector2(-8, 8),
                    new Vector2(-10, 2)
                }
            };
            
            // Sektör B (Orta Üst)
            sectorizer.sectors[1] = new SectorData
            {
                id = "B",
                bounds = new Rect(-5, 0, 10, 10),
                portals = new Vector2[]
                {
                    new Vector2(-5, 5),   // Sol
                    new Vector2(5, 5)     // Sað
                },
                anchors = new Vector2[] { new Vector2(0, 5) },
                sweepPoints = new Vector2[]
                {
                    new Vector2(-3, 8),
                    new Vector2(3, 8),
                    new Vector2(0, 2)
                }
            };
            
            // Sektör C (Sað Üst)
            sectorizer.sectors[2] = new SectorData
            {
                id = "C",
                bounds = new Rect(5, 0, 10, 10),
                portals = new Vector2[]
                {
                    new Vector2(5, 5),    // Sol
                    new Vector2(10, 0)    // Alt
                },
                anchors = new Vector2[] { new Vector2(10, 5) },
                sweepPoints = new Vector2[]
                {
                    new Vector2(8, 8),
                    new Vector2(12, 8),
                    new Vector2(10, 2)
                }
            };
            
            // Sektör D (Sol Alt)
            sectorizer.sectors[3] = new SectorData
            {
                id = "D",
                bounds = new Rect(-15, -10, 10, 10),
                portals = new Vector2[]
                {
                    new Vector2(-10, -5), // Sað
                    new Vector2(-10, 0)   // Üst
                },
                anchors = new Vector2[] { new Vector2(-10, -5) },
                sweepPoints = new Vector2[]
                {
                    new Vector2(-12, -2),
                    new Vector2(-8, -2),
                    new Vector2(-10, -8)
                }
            };
            
            // Sektör E (Orta Alt)
            sectorizer.sectors[4] = new SectorData
            {
                id = "E",
                bounds = new Rect(-5, -10, 10, 10),
                portals = new Vector2[]
                {
                    new Vector2(-5, -5),  // Sol
                    new Vector2(5, -5)    // Sað
                },
                anchors = new Vector2[] { new Vector2(0, -5) },
                sweepPoints = new Vector2[]
                {
                    new Vector2(-3, -2),
                    new Vector2(3, -2),
                    new Vector2(0, -8)
                }
            };
            
            // Sektör F (Sað Alt)
            sectorizer.sectors[5] = new SectorData
            {
                id = "F",
                bounds = new Rect(5, -10, 10, 10),
                portals = new Vector2[]
                {
                    new Vector2(5, -5),   // Sol
                    new Vector2(10, 0)    // Üst
                },
                anchors = new Vector2[] { new Vector2(10, -5) },
                sweepPoints = new Vector2[]
                {
                    new Vector2(8, -2),
                    new Vector2(12, -2),
                    new Vector2(10, -8)
                }
            };
            
            EditorUtility.SetDirty(sectorizer);
            Debug.Log("<color=lime>[SectorizerAutoSetup] 6 sectors configured! (30x20 grid)</color>");
        }
    }
}
